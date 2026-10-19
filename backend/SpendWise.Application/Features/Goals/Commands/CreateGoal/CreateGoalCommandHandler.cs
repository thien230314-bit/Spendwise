using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Goals.Commands;
using SpendWise.Application.Planning;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.Goals.Commands.CreateGoal;

// Tạo Goal và khoản phân bổ ban đầu trong cùng một transaction.
public sealed class CreateGoalCommandHandler(
    ISpendWiseDbContext db,
    FinancialSecurityGuard security,
    GoalMovementExecutor movementExecutor,
    IClock clock) : IRequestHandler<CreateGoalCommand, GoalDto>
{
    public async Task<GoalDto> Handle(CreateGoalCommand command, CancellationToken cancellationToken)
    {
        // Mutation tự xác minh PIN, không dựa vào lần verify trước từ frontend.
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        var goalName = InputBoundary.RequiredText(command.Request.Name, 120, "Goal name");
        InputBoundary.PositiveMoney(command.Request.TargetAmount, "Target amount");
        InputBoundary.FutureDate(command.Request.TargetDate, clock.UtcNow, "Target date");
        if (command.Request.InitialAmount < 0 || command.Request.InitialAmount > command.Request.TargetAmount)
        {
            throw new AppException("invalid_initial_amount", "Initial amount must be between zero and target amount.");
        }

        var categoryName = await ResolveCategoryAsync(command.UserId, command.Request.CategoryId, cancellationToken);
        var icon = InputBoundary.OptionalText(command.Request.IconName, 80, "Icon name");
        var color = ValidateColor(command.Request.ColorHex);

        return await db.ExecuteInTransactionAsync(async token =>
        {
            var account = new LedgerAccount
            {
                Code = $"GOAL:{Guid.NewGuid():N}",
                Name = goalName,
                Type = LedgerAccountType.GoalReserve,
                OwnerUserId = command.UserId
            };
            db.LedgerAccounts.Add(account);
            var goal = new Goal
            {
                OwnerUserId = command.UserId,
                Name = goalName,
                TargetAmount = command.Request.TargetAmount,
                TargetDate = command.Request.TargetDate?.ToUniversalTime(),
                CategoryId = command.Request.CategoryId,
                CategorySnapshot = categoryName,
                IconName = icon,
                ColorHex = color,
                LedgerAccount = account,
                LedgerAccountId = account.Id
            };
            db.Goals.Add(goal);
            await db.SaveChangesAsync(token);

            if (command.Request.InitialAmount == 0)
            {
                return GoalMappings.ToDto(goal);
            }

            if (command.Request.WalletId is null || string.IsNullOrWhiteSpace(command.Request.IdempotencyKey))
            {
                throw new AppException("initial_goal_source_required", "Wallet and idempotency key are required for an initial allocation.");
            }

            // Dùng posting Goal hiện có nên InitialAmount không bị ghi thành Expense.
            return await movementExecutor.MoveAsync(command.UserId, goal.Id,
                new GoalMovementRequest(command.Request.WalletId.Value, command.Request.InitialAmount,
                    command.Request.IdempotencyKey, command.Request.SecurityPin), true, token);
        }, cancellationToken);
    }

    private async Task<string?> ResolveCategoryAsync(Guid userId, Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null) return null;
        var category = await db.Categories.AsNoTracking().SingleOrDefaultAsync(item => item.Id == categoryId
            && item.IsActive && (item.IsSystem || item.OwnerUserId == userId), cancellationToken)
            ?? throw new AppException("invalid_category", "Goal category is invalid.");
        return category.Name;
    }

    private static string? ValidateColor(string? value)
    {
        var color = InputBoundary.OptionalText(value, 20, "Color");
        if (!string.IsNullOrEmpty(color)
            && !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
        {
            throw new AppException("invalid_color", "Color must use #RRGGBB format.");
        }
        return color;
    }
}
