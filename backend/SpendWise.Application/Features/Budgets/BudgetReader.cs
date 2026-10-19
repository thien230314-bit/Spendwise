using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.Budgets;

// Lớp này dùng một phép tính chung cho hạn mức gốc, rollover và chi tiêu thực tế.
public sealed class BudgetReader(ISpendWiseDbContext db)
{
    public sealed record Calculation(
        decimal BaseLimitAmount,
        decimal RolloverAmount,
        decimal EffectiveLimitAmount,
        decimal SpentAmount);

    public async Task<IReadOnlyList<BudgetDto>> ReadAsync(
        IQueryable<Budget> budgets,
        CancellationToken cancellationToken)
    {
        var items = await budgets
            .AsNoTracking()
            .Include(budget => budget.Category)
            .ToListAsync(cancellationToken);
        var result = new List<BudgetDto>(items.Count);
        foreach (var budget in items)
        {
            result.Add(await ToDtoAsync(budget, cancellationToken));
        }

        return result;
    }

    public async Task<Calculation> CalculateAsync(
        Guid ownerUserId,
        Guid categoryId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var targetPeriod = PeriodNumber(year, month);
        var candidates = await db.Budgets
            .AsNoTracking()
            .Where(budget => budget.OwnerUserId == ownerUserId
                && budget.CategoryId == categoryId
                && budget.Year * 12 + budget.Month <= targetPeriod)
            .OrderByDescending(budget => budget.Year)
            .ThenByDescending(budget => budget.Month)
            .ToListAsync(cancellationToken);
        var byPeriod = candidates.ToDictionary(
            budget => PeriodNumber(budget.Year, budget.Month));
        if (!byPeriod.TryGetValue(targetPeriod, out var target))
        {
            throw new InvalidOperationException("Budget calculation requires an existing budget.");
        }

        // Đi ngược đến đầu chuỗi liên tiếp mà tháng trước cho phép chuyển phần dư.
        var chain = new List<Budget> { target };
        var cursor = targetPeriod;
        while (byPeriod.TryGetValue(cursor - 1, out var previous) && previous.Rollover)
        {
            chain.Add(previous);
            cursor--;
        }
        chain.Reverse();

        decimal carry = 0;
        Calculation? targetCalculation = null;
        foreach (var budget in chain)
        {
            var (startUtc, endUtc) = VietnamBusinessTime.MonthUtcRange(budget.Year, budget.Month);
            var spent = await db.FinancialTransactions
                .AsNoTracking()
                .Where(transaction => transaction.OwnerUserId == ownerUserId
                    && transaction.CategoryId == categoryId
                    && transaction.Type == FinancialTransactionType.Expense
                    && transaction.Status == FinancialTransactionStatus.Completed
                    && transaction.OccurredAt >= startUtc
                    && transaction.OccurredAt < endUtc)
                .SumAsync(transaction => (decimal?)transaction.Amount, cancellationToken)
                ?? 0;
            var effectiveLimit = budget.LimitAmount + carry;
            var calculation = new Calculation(budget.LimitAmount, carry, effectiveLimit, spent);
            if (PeriodNumber(budget.Year, budget.Month) == targetPeriod)
            {
                targetCalculation = calculation;
            }

            // Chỉ số dư dương của tháng bật rollover mới được chuyển sang tháng kế tiếp.
            carry = budget.Rollover ? Math.Max(0, effectiveLimit - spent) : 0;
        }

        return targetCalculation!;
    }

    public async Task<BudgetDto> ToDtoAsync(
        Budget budget,
        CancellationToken cancellationToken)
    {
        var calculation = await CalculateAsync(
            budget.OwnerUserId,
            budget.CategoryId,
            budget.Year,
            budget.Month,
            cancellationToken);
        var categoryName = budget.Category?.Name
            ?? await db.Categories.Where(category => category.Id == budget.CategoryId)
                .Select(category => category.Name)
                .SingleAsync(cancellationToken);
        return CreateDto(
            budget.Id,
            budget.CategoryId,
            categoryName,
            budget.Year,
            budget.Month,
            budget.AlertsEnabled,
            budget.Rollover,
            calculation);
    }

    internal static BudgetDto CreateDto(
        Guid id,
        Guid categoryId,
        string categoryName,
        int year,
        int month,
        bool alertsEnabled,
        bool rollover,
        Calculation calculation)
    {
        var percentage = calculation.EffectiveLimitAmount == 0
            ? 0
            : Math.Round(calculation.SpentAmount / calculation.EffectiveLimitAmount * 100, 2);
        var alertLevel = percentage >= 100
            ? "OverBudget"
            : percentage >= 90
                ? "Warning"
                : percentage >= 70
                    ? "Attention"
                    : "Normal";

        return new BudgetDto(
            id,
            categoryId,
            categoryName,
            year,
            month,
            calculation.BaseLimitAmount,
            calculation.SpentAmount,
            calculation.EffectiveLimitAmount - calculation.SpentAmount,
            percentage,
            alertLevel,
            alertsEnabled,
            rollover);
    }

    private static int PeriodNumber(int year, int month) => year * 12 + month;
}
