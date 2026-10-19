using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Budgets;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.Dashboard;

public sealed record DashboardRecentTransactionDto(Guid Id, string Type, decimal Amount, string Description, DateTime OccurredAt);
public sealed record DashboardCategoryDto(Guid? CategoryId, string CategoryName, decimal Amount);
public sealed record DashboardCashFlowDto(int Year, int Month, decimal Income, decimal Expense);
public sealed record DashboardAttentionDto(string Type, string Severity, string Title, Guid RelatedId, DateTime? DueAt);
public sealed record DashboardDto(
    decimal TotalBalance,
    decimal TotalWalletsBalance,
    decimal TotalTrackedAssets,
    decimal TotalGoalsReserved,
    decimal IncomeThisMonth,
    decimal ExpenseThisMonth,
    decimal BudgetRemaining,
    decimal CashFlowDifference,
    decimal GroupFundBalance,
    decimal UserPendingFundObligations,
    decimal OthersOweUser,
    decimal UserOwesOthers,
    IReadOnlyList<DashboardRecentTransactionDto> RecentTransactions,
    IReadOnlyList<DashboardCategoryDto> CategoryDistribution,
    IReadOnlyList<DashboardCashFlowDto> SixMonthCashFlow,
    IReadOnlyList<DashboardAttentionDto> AttentionItems);

public sealed record GetDashboardQuery(Guid UserId) : IRequest<DashboardDto>;

public sealed class GetDashboardQueryHandler(ISpendWiseDbContext db, IClock clock, BudgetReader budgetReader)
    : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    public async Task<DashboardDto> Handle(GetDashboardQuery query, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var localNow = VietnamBusinessTime.ToLocal(now);
        var localMonthStart = new DateTime(localNow.Year, localNow.Month, 1);
        var monthStart = VietnamBusinessTime.ToUtc(localMonthStart);
        var monthEnd = VietnamBusinessTime.ToUtc(localMonthStart.AddMonths(1));
        var sixMonthLocalStart = localMonthStart.AddMonths(-5);
        var sixMonthStart = VietnamBusinessTime.ToUtc(sixMonthLocalStart);

        // Wallet và Goal là hai phần khác nhau của tài sản được theo dõi.
        var walletsBalance = await db.Wallets.AsNoTracking()
            .Where(wallet => wallet.OwnerUserId == query.UserId && wallet.Status != WalletStatus.Closed)
            .SumAsync(wallet => (decimal?)wallet.AvailableBalance, cancellationToken) ?? 0;
        var goalsReserved = await db.Goals.AsNoTracking()
            .Where(goal => goal.OwnerUserId == query.UserId)
            .SumAsync(goal => (decimal?)goal.CurrentAllocatedAmount, cancellationToken) ?? 0;

        var completed = db.FinancialTransactions.AsNoTracking()
            .Where(transaction => transaction.OwnerUserId == query.UserId
                && transaction.Status == FinancialTransactionStatus.Completed);
        var income = await completed.Where(transaction => transaction.OccurredAt >= monthStart
                && transaction.OccurredAt < monthEnd && transaction.Type == FinancialTransactionType.Income)
            .SumAsync(transaction => (decimal?)transaction.Amount, cancellationToken) ?? 0;
        var expense = await completed.Where(transaction => transaction.OccurredAt >= monthStart
                && transaction.OccurredAt < monthEnd && transaction.Type == FinancialTransactionType.Expense)
            .SumAsync(transaction => (decimal?)transaction.Amount, cancellationToken) ?? 0;

        var budgets = await db.Budgets.AsNoTracking()
            .Where(budget => budget.OwnerUserId == query.UserId
                && budget.Year == localNow.Year && budget.Month == localNow.Month)
            .ToListAsync(cancellationToken);
        var spentByCategory = await completed.Where(transaction => transaction.OccurredAt >= monthStart
                && transaction.OccurredAt < monthEnd && transaction.Type == FinancialTransactionType.Expense
                && transaction.CategoryId.HasValue)
            .GroupBy(transaction => transaction.CategoryId!.Value)
            .Select(group => new { Key = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToDictionaryAsync(item => item.Key, item => item.Amount, cancellationToken);
        var budgetCalculations = new Dictionary<Guid, BudgetReader.Calculation>();
        foreach (var budget in budgets)
        {
            budgetCalculations[budget.Id] = await budgetReader.CalculateAsync(
                budget.OwnerUserId, budget.CategoryId, budget.Year, budget.Month, cancellationToken);
        }
        var budgetRemaining = budgets.Sum(budget => Math.Max(
            0,
            budgetCalculations[budget.Id].EffectiveLimitAmount
                - budgetCalculations[budget.Id].SpentAmount));

        var groupIds = await db.GroupMembers.AsNoTracking()
            .Where(member => member.UserId == query.UserId && member.Group.Status != GroupStatus.Closed)
            .Select(member => member.GroupId)
            .ToListAsync(cancellationToken);
        var groupFundBalance = await db.SharedFunds.AsNoTracking()
            .Where(fund => groupIds.Contains(fund.GroupId))
            .SumAsync(fund => (decimal?)fund.Balance, cancellationToken) ?? 0;
        var requestObligations = await (from recipient in db.FundRequestRecipients.AsNoTracking()
                                        join request in db.FundRequests.AsNoTracking()
                                            on recipient.FundRequestId equals request.Id
                                        where recipient.UserId == query.UserId
                                            && recipient.Status == FundRequestRecipientStatus.Pending
                                        select (decimal?)request.AmountPerMember)
            .SumAsync(cancellationToken) ?? 0;
        var roundObligations = await db.GroupFundRoundMembers.AsNoTracking()
            .Where(member => member.UserId == query.UserId && member.Status != GroupFundRoundMemberStatus.Paid)
            .SumAsync(member => (decimal?)(member.RequiredAmount - member.PaidAmount), cancellationToken) ?? 0;

        var recent = await completed.OrderByDescending(transaction => transaction.OccurredAt)
            .Take(10)
            .Select(transaction => new DashboardRecentTransactionDto(transaction.Id, transaction.Type.ToString(),
                transaction.Amount, transaction.Description, transaction.OccurredAt))
            .ToListAsync(cancellationToken);
        var categoryNames = await db.Categories.AsNoTracking()
            .Where(category => spentByCategory.Keys.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        var categories = spentByCategory
            .Select(item => new DashboardCategoryDto(item.Key,
                categoryNames.GetValueOrDefault(item.Key, "Other"), item.Value))
            .OrderByDescending(item => item.Amount)
            .ToList();

        var cashRows = await completed.Where(transaction => transaction.OccurredAt >= sixMonthStart
                && transaction.OccurredAt < monthEnd
                && (transaction.Type == FinancialTransactionType.Income || transaction.Type == FinancialTransactionType.Expense))
            .Select(transaction => new { transaction.OccurredAt, transaction.Type, transaction.Amount })
            .ToListAsync(cancellationToken);
        var localCashRows = cashRows
            .Select(row => new { Local = VietnamBusinessTime.ToLocal(row.OccurredAt), row.Type, row.Amount })
            .GroupBy(row => new { row.Local.Year, row.Local.Month, row.Type })
            .Select(group => new { group.Key.Year, group.Key.Month, group.Key.Type, Amount = group.Sum(item => item.Amount) })
            .ToList();
        var cashFlow = Enumerable.Range(0, 6).Select(offset => sixMonthLocalStart.AddMonths(offset))
            .Select(month => new DashboardCashFlowDto(month.Year, month.Month,
                localCashRows.Where(row => row.Year == month.Year && row.Month == month.Month && row.Type == FinancialTransactionType.Income).Sum(row => row.Amount),
                localCashRows.Where(row => row.Year == month.Year && row.Month == month.Month && row.Type == FinancialTransactionType.Expense).Sum(row => row.Amount)))
            .ToList();

        // Attention luôn tính từ trạng thái nghiệp vụ, không phụ thuộc Notification đã đọc.
        var attention = new List<DashboardAttentionDto>();
        foreach (var budget in budgets)
        {
            var calculation = budgetCalculations[budget.Id];
            var used = calculation.SpentAmount;
            if (used >= calculation.EffectiveLimitAmount * .9m)
            {
                attention.Add(new DashboardAttentionDto("budget", used >= calculation.EffectiveLimitAmount ? "critical" : "warning",
                    used >= calculation.EffectiveLimitAmount ? "Budget exceeded" : "Budget reached 90%", budget.Id, null));
            }
        }

        var occurrences = await db.RecurringOccurrences.AsNoTracking()
            .Where(occurrence => occurrence.RecurringTransaction.OwnerUserId == query.UserId
                && occurrence.Status == RecurringOccurrenceStatus.Due)
            .OrderBy(occurrence => occurrence.ScheduledFor)
            .Take(20)
            .Select(occurrence => new { occurrence.Id, occurrence.ScheduledFor, occurrence.RecurringTransaction.Description })
            .ToListAsync(cancellationToken);
        attention.AddRange(occurrences.Select(occurrence => new DashboardAttentionDto("recurring",
            occurrence.ScheduledFor < now ? "overdue" : "due", occurrence.Description, occurrence.Id, occurrence.ScheduledFor)));

        return new DashboardDto(walletsBalance + goalsReserved, walletsBalance, walletsBalance + goalsReserved,
            goalsReserved, income, expense, budgetRemaining, income - expense, groupFundBalance,
            requestObligations + roundObligations, 0, 0, recent, categories, cashFlow, attention);
    }
}
