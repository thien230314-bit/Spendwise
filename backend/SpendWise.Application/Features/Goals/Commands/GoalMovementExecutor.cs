using SpendWise.Application.Features.Banking;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Application.Features;
using SpendWise.Application.Planning;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.Goals.Commands;

public sealed class GoalMovementExecutor(
    ISpendWiseDbContext db,
    ILedgerService ledger,
    IClock clock,
    FinancialSecurityGuard security,
    IActiveBankGuard? activeBankGuard = null)
{
    public async Task<GoalDto> MoveAsync(
        Guid userId,
        Guid goalId,
        GoalMovementRequest request,
        bool contribute,
        CancellationToken cancellationToken)
    {
        if (activeBankGuard is not null) await activeBankGuard.EnsureActiveBankAsync(userId, cancellationToken);
        await security.VerifyAsync(userId, request.SecurityPin, cancellationToken);
        // Mở transaction cho toàn bộ luồng đóng góp hoặc rút tiền Goal.
        return await db.ExecuteInTransactionAsync(
            token => MoveInTransactionAsync(userId, goalId, request, contribute, token),
            cancellationToken);
    }

    private async Task<GoalDto> MoveInTransactionAsync(
        Guid userId,
        Guid goalId,
        GoalMovementRequest request,
        bool contribute,
        CancellationToken cancellationToken)
    {
        // Kiểm tra replay, quyền sở hữu và posting trước khi ghi GoalMovement.
        InputBoundary.PositiveMoney(request.Amount);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || request.IdempotencyKey.Length > 100)
        {
            throw new AppException(
                "invalid_goal_movement",
                "Positive amount and an idempotency key of at most 100 characters are required.");
        }
        var normalizedKey = request.IdempotencyKey.Trim();
        var movementType = contribute
            ? GoalMovementType.Contribution
            : GoalMovementType.Withdrawal;
        var transactionType = contribute
            ? FinancialTransactionType.GoalContribution
            : FinancialTransactionType.GoalWithdrawal;

        await SerializeGoalAsync(db, userId, goalId, cancellationToken);

        // Cùng loại thao tác và idempotency key chỉ được ghi nhận một lần.
        var existing = await db.GoalMovements
            .AsNoTracking()
            .SingleOrDefaultAsync(
                movement => movement.GoalId == goalId
                    && movement.Type == movementType
                    && movement.IdempotencyKey == normalizedKey,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.WalletId != request.WalletId || existing.Amount != request.Amount)
            {
                throw new ConflictException(
                    "idempotency_conflict",
                    "The idempotency key was already used with different goal movement details.");
            }

            var replayedGoal = await db.Goals
                .AsNoTracking()
                .SingleAsync(
                    goal => goal.Id == goalId && goal.OwnerUserId == userId,
                    cancellationToken);
            return GoalMappings.ToDto(replayedGoal);
        }

        var goal = await db.Goals
            .Include(candidate => candidate.LedgerAccount)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == goalId && candidate.OwnerUserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("goal_not_found", "Goal not found.");
        var wallet = await db.Wallets
            .Include(candidate => candidate.LedgerAccount)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.WalletId
                    && candidate.OwnerUserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");
        if (!contribute && goal.CurrentAllocatedAmount < request.Amount)
        {
            throw new ConflictException(
                "insufficient_goal_balance",
                "Goal has insufficient allocated funds.");
        }

        var movement = new GoalMovement
        {
            GoalId = goal.Id,
            WalletId = wallet.Id,
            Type = movementType,
            Amount = request.Amount,
            WalletNameSnapshot = wallet.Name,
            IdempotencyKey = normalizedKey
        };

        // Đóng góp không phải Expense; rút mục tiêu không phải Income.
        var posting = contribute
            ? await ledger.PostAsync(
                wallet.LedgerAccount.Id,
                goal.LedgerAccountId,
                request.Amount,
                "VND",
                transactionType.ToString(),
                movement.Id,
                $"goal:{goal.Id:N}",
                normalizedKey,
                goal.Name,
                cancellationToken)
            : await ledger.PostAsync(
                goal.LedgerAccountId,
                wallet.LedgerAccount.Id,
                request.Amount,
                "VND",
                transactionType.ToString(),
                movement.Id,
                $"goal:{goal.Id:N}",
                normalizedKey,
                goal.Name,
                cancellationToken);

        movement.LedgerTransactionId = posting.LedgerTransactionId;
        goal.CurrentAllocatedAmount += contribute ? request.Amount : -request.Amount;
        if (goal.CurrentAllocatedAmount >= goal.TargetAmount)
        {
            goal.Status = GoalStatus.Completed;
        }
        else if (goal.Status == GoalStatus.Completed)
        {
            goal.Status = GoalStatus.Active;
        }

        db.GoalMovements.Add(movement);
        db.FinancialTransactions.Add(new FinancialTransaction
        {
            OwnerUserId = userId,
            WalletId = wallet.Id,
            LedgerTransactionId = posting.LedgerTransactionId,
            RelatedBusinessId = goal.Id,
            Type = transactionType,
            Amount = request.Amount,
            Description = goal.Name,
            OccurredAt = clock.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return GoalMappings.ToDto(goal);
    }

    internal static async Task SerializeGoalAsync(
        ISpendWiseDbContext db, Guid userId, Guid goalId, CancellationToken token)
    {
        if (db is DbContext context && context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            // Added goals are not yet queryable; only admit a new goal tracked for this owner.
            var addedOwnedGoal = context.ChangeTracker.Entries<Goal>().Any(entry =>
                entry.State == EntityState.Added && entry.Entity.Id == goalId && entry.Entity.OwnerUserId == userId);
            if (!addedOwnedGoal && !await db.Goals.AsNoTracking()
                .AnyAsync(goal => goal.Id == goalId && goal.OwnerUserId == userId, token))
                throw new NotFoundException("goal_not_found", "Goal not found.");
            return;
        }

        // The caller's transaction holds the goal root through replay, posting and business writes.
        var affectedRows = await db.Goals.Where(goal => goal.Id == goalId && goal.OwnerUserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(goal => goal.UpdatedAt, goal => goal.UpdatedAt), token);
        if (affectedRows == 0)
            throw new NotFoundException("goal_not_found", "Goal not found.");

        // Identity resolution does not refresh an already tracked goal after admission.
        // Preserve pending caller writes (including initial allocation during goal creation).
        if (db is not DbContext tracked) return;
        foreach (var entry in tracked.ChangeTracker.Entries<Goal>()
            .Where(entry => entry.State == EntityState.Unchanged
                && entry.Entity.Id == goalId && entry.Entity.OwnerUserId == userId).ToList())
            await entry.ReloadAsync(token);
    }
}
