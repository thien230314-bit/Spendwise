using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Application.Groups;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups;

public sealed class WalletFundAccounting(
    ISpendWiseDbContext db,
    GroupAccess access,
    ILedgerService ledger,
    FinancialSecurityGuard security,
    IClock clock)
{
    public async Task<GroupContributionDto> ContributeAsync(
        Guid userId,
        Guid groupId,
        Guid? walletId,
        decimal amount,
        string? purpose,
        string? idempotencyKey,
        string? securityPin,
        Guid? requestRecipientId,
        CancellationToken cancellationToken)
    {
        if (walletId is null || walletId == Guid.Empty)
            throw new AppException("wallet_required", "Select a personal wallet.");
        InputBoundary.PositiveMoney(amount);
        var key = InputBoundary.RequiredText(idempotencyKey, 100, "Idempotency key");
        var normalizedPurpose = InputBoundary.OptionalText(purpose, 500, "Purpose") ?? "Group fund contribution";
        await security.VerifyAsync(userId, securityPin, cancellationToken);

        return await db.ExecuteInTransactionAsync(async token =>
        {
            await GroupFinancialConcurrency.SerializeAsync(db, groupId, token);
            await access.RequireMemberAsync(userId, groupId, token);
            await access.EnsureMutableAsync(groupId, token);
            var replay = await db.GroupContributions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.UserId == userId && item.IdempotencyKey == key, token);
            if (replay is not null)
            {
                EnsureReplay(replay, groupId, walletId.Value, amount, normalizedPurpose, requestRecipientId);
                return Map(replay, true);
            }

            var wallet = await db.Wallets.Include(item => item.LedgerAccount)
                .SingleOrDefaultAsync(item => item.Id == walletId && item.OwnerUserId == userId, token)
                ?? throw new NotFoundException("wallet_not_found", "Selected wallet was not found.");
            if (wallet.Status != WalletStatus.Active)
                throw new ConflictException("wallet_unavailable", "Selected wallet is not active.");

            var fund = await LoadCompatibleFundAsync(groupId, token);
            var contribution = new GroupContribution
            {
                GroupId = groupId,
                UserId = userId,
                WalletId = wallet.Id,
                Amount = amount,
                Purpose = normalizedPurpose,
                IdempotencyKey = key,
                ContributionRequestRecipientId = requestRecipientId
            };
            var posting = await ledger.PostAsync(wallet.LedgerAccount.Id, fund.LedgerAccountId, amount, "VND",
                "GroupContribution", contribution.Id, $"group-contribution:{userId:N}", key,
                normalizedPurpose, token);

            contribution.LedgerTransactionId = posting.LedgerTransactionId;
            fund.Balance += amount;
            var share = await db.GroupFundShares.SingleOrDefaultAsync(
                item => item.GroupId == groupId && item.UserId == userId, token);
            if (share is null)
            {
                share = new GroupFundShare { GroupId = groupId, UserId = userId };
                db.GroupFundShares.Add(share);
            }
            share.RemainingAmount += amount;
            var userName = await db.Users.Where(item => item.Id == userId).Select(item => item.FullName).SingleAsync(token);
            db.GroupContributions.Add(contribution);
            db.FundTransactions.Add(new FundTransaction
            {
                GroupId = groupId,
                Type = FundTransactionType.Contribution,
                Amount = amount,
                UserId = userId,
                UserNameSnapshot = userName,
                Description = normalizedPurpose,
                Date = clock.UtcNow,
                ReferenceCode = posting.Reference,
                WalletId = wallet.Id,
                LedgerTransactionId = posting.LedgerTransactionId
            });
            db.GroupActivities.Add(new GroupActivity
            {
                GroupId = groupId,
                ActorUserId = userId,
                Type = GroupActivityType.FundContribution,
                DataJson = JsonSerializer.Serialize(new { contribution.Id, contribution.Amount, contribution.WalletId })
            });
            await db.SaveChangesAsync(token);
            return Map(contribution, false);
        }, cancellationToken);
    }

    public async Task<SharedFund> LoadCompatibleFundAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var fund = await db.SharedFunds.Include(item => item.LedgerAccount)
            .SingleAsync(item => item.GroupId == groupId, cancellationToken);
        var shares = await db.GroupFundShares.Where(item => item.GroupId == groupId)
            .SumAsync(item => (decimal?)item.RemainingAmount, cancellationToken) ?? 0;
        var unresolvedLegacy = await db.BankDirectPayments.AsNoTracking().AnyAsync(item => item.GroupId == groupId
            && item.ContextType != BankDirectPaymentContextType.GroupRefund
            && (item.Status == BankDirectPaymentStatus.Pending
                || item.Status == BankDirectPaymentStatus.AwaitingReconciliation), cancellationToken);
        if (unresolvedLegacy || fund.Balance != fund.LedgerAccount.Balance || fund.Balance != shares)
            throw new ConflictException("legacy_group_fund_incompatible",
                "This group fund requires demo-data cleanup before wallet-funded accounting can be used.");
        return fund;
    }

    private static void EnsureReplay(GroupContribution item, Guid groupId, Guid walletId, decimal amount,
        string purpose, Guid? requestRecipientId)
    {
        if (item.GroupId != groupId || item.WalletId != walletId || item.Amount != amount
            || item.Purpose != purpose || item.ContributionRequestRecipientId != requestRecipientId)
            throw new ConflictException("idempotency_conflict",
                "The idempotency key was used with different contribution details.");
    }

    private static GroupContributionDto Map(GroupContribution item, bool replayed) => new(item.Id, item.GroupId,
        item.UserId, item.WalletId, item.Amount, item.Purpose, item.LedgerTransactionId, item.CreatedAt, replayed);
}
