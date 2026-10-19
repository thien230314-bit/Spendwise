using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AI;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Budgets;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.AiAssistant;

// Lớp này xây dựng ngữ cảnh tài chính đã kiểm soát trước khi gọi AI provider.

public sealed class AiFinancialContextBuilder(
    ISpendWiseDbContext db,
    IClock clock,
    BudgetReader budgetReader) : IAiDataTools
{
    public async Task<AiFinancialContext> BuildContextAsync(
        Guid userId,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        // Tổng hợp dữ liệu tài chính mà người dùng được phép xem cho prompt AI.
        var localNow = VietnamBusinessTime.ToLocal(clock.UtcNow);
        var (start, end) = VietnamBusinessTime.MonthUtcRange(localNow.Year, localNow.Month);

        var balance = await db.Wallets
            .Where(wallet => wallet.OwnerUserId == userId
                && wallet.Status != WalletStatus.Closed)
            .SumAsync(wallet => (decimal?)wallet.AvailableBalance, cancellationToken)
            ?? 0;
        var expenses = await db.FinancialTransactions
            .Where(transaction => transaction.OwnerUserId == userId
                && transaction.Type == FinancialTransactionType.Expense
                && transaction.Status == FinancialTransactionStatus.Completed
                && transaction.OccurredAt >= start
                && transaction.OccurredAt < end)
            .GroupBy(transaction => transaction.Category != null
                ? transaction.Category.Name
                : "Other")
            .Select(group => new
            {
                Category = group.Key,
                Amount = group.Sum(transaction => transaction.Amount)
            })
            .ToListAsync(cancellationToken);
        var budgets = await db.Budgets
            .Include(budget => budget.Category)
            .Where(budget => budget.OwnerUserId == userId
                && budget.Year == localNow.Year
                && budget.Month == localNow.Month)
            .ToListAsync(cancellationToken);
        var budgetStatus = new List<string>(budgets.Count);
        foreach (var budget in budgets)
        {
            var calculation = await budgetReader.CalculateAsync(
                budget.OwnerUserId,
                budget.CategoryId,
                budget.Year,
                budget.Month,
                cancellationToken);
            budgetStatus.Add($"{budget.Category.Name}: effective limit {calculation.EffectiveLimitAmount:N0} VND");
        }
        var goals = await db.Goals
            .Where(goal => goal.OwnerUserId == userId)
            .Select(goal => $"{goal.Name}: {goal.CurrentAllocatedAmount}/{goal.TargetAmount} VND ({goal.Status})")
            .ToListAsync(cancellationToken);
        string? fund = null;
        if (groupId is not null)
        {
            var isActiveMember = await db.GroupMembers.AnyAsync(
                member => member.GroupId == groupId
                    && member.UserId == userId
                    && member.Group.Status == GroupStatus.Active,
                cancellationToken);
            if (!isActiveMember)
            {
                throw new ForbiddenException(
                    "group_membership_required",
                    "AI group access requires active membership.");
            }

            var sharedFund = await db.SharedFunds
                .AsNoTracking()
                .SingleAsync(item => item.GroupId == groupId, cancellationToken);
            fund = $"Shared fund: {sharedFund.Balance:N0} {sharedFund.Currency}";
        }

        return new AiFinancialContext(
            balance,
            expenses.Sum(expense => expense.Amount),
            expenses.ToDictionary(expense => expense.Category, expense => expense.Amount),
            budgetStatus,
            goals,
            fund);
    }
}
