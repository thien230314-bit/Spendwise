using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Budgets;
using SpendWise.Application.Features.Budgets.Queries.GetBudgetById;
using SpendWise.Application.Features.Dashboard;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Planning;
using Xunit;

namespace SpendWise.Tests;

public sealed class TargetedCalendarAndRolloverTests
{
    [Fact]
    public void Vietnam_month_boundary_maps_to_the_expected_utc_instant()
    {
        // 00:30 ngày 01/10 tại Việt Nam vẫn là 17:30 ngày 30/09 theo UTC.
        var utc = new DateTime(2026, 9, 30, 17, 30, 0, DateTimeKind.Utc);

        var local = VietnamBusinessTime.ToLocal(utc);
        var range = VietnamBusinessTime.MonthUtcRange(2026, 10);

        Assert.Equal(new DateTime(2026, 10, 1, 0, 30, 0), local);
        Assert.Equal(new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc), range.StartUtc);
        Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), range.EndUtc);
    }

    [Fact]
    public async Task Budget_and_dashboard_classify_boundary_transactions_by_vietnam_month()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Vietnam calendar user");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", true, balance: 1_000_000);
        var category = await fixture.Db.Categories.SingleAsync(item => item.Key == "categories.food");
        var budget = new Budget
        {
            OwnerUserId = user.Id,
            CategoryId = category.Id,
            Year = 2026,
            Month = 10,
            LimitAmount = 1_000_000
        };
        fixture.Db.Budgets.Add(budget);
        fixture.Db.FinancialTransactions.AddRange(
            Expense(user.Id, wallet.Id, category.Id, 100_000,
                new DateTime(2026, 9, 30, 17, 30, 0, DateTimeKind.Utc)),
            Expense(user.Id, wallet.Id, category.Id, 50_000,
                new DateTime(2026, 9, 30, 16, 30, 0, DateTimeKind.Utc)));
        await fixture.Db.SaveChangesAsync();
        var clock = new FixedClock(new DateTime(2026, 9, 30, 18, 0, 0, DateTimeKind.Utc));
        var reader = new BudgetReader(fixture.Db);

        var budgetDto = await new GetBudgetByIdQueryHandler(fixture.Db, reader)
            .Handle(new(user.Id, budget.Id), default);
        var dashboard = await new GetDashboardQueryHandler(fixture.Db, clock, reader)
            .Handle(new(user.Id), default);

        Assert.Equal(100_000, budgetDto.SpentAmount);
        Assert.Equal(100_000, dashboard.ExpenseThisMonth);
        Assert.Equal(100_000, dashboard.SixMonthCashFlow.Single(item =>
            item.Year == 2026 && item.Month == 10).Expense);
        Assert.Equal(50_000, dashboard.SixMonthCashFlow.Single(item =>
            item.Year == 2026 && item.Month == 9).Expense);
    }

    [Fact]
    public async Task Budget_without_previous_month_has_no_rollover()
    {
        await using var fixture = await CreateBudgetFixtureAsync();
        var current = await AddBudgetAsync(fixture, 2026, 10, 1_000, false);

        var reader = new BudgetReader(fixture.Db);
        var calculation = await reader.CalculateAsync(
            fixture.UserId, fixture.CategoryId, current.Year, current.Month, default);
        var dto = await reader.ToDtoAsync(current, default);

        Assert.Equal(0, calculation.RolloverAmount);
        Assert.Equal(1_000, calculation.EffectiveLimitAmount);
        Assert.Equal(1_000, dto.RemainingAmount);
    }

    [Theory]
    [InlineData(true, 400, 600)]
    [InlineData(true, 1_200, 0)]
    [InlineData(false, 400, 0)]
    public async Task Budget_carries_only_positive_unused_balance_when_previous_month_enables_rollover(
        bool previousRollover,
        decimal previousSpent,
        decimal expectedCarry)
    {
        await using var fixture = await CreateBudgetFixtureAsync();
        await AddBudgetAsync(fixture, 2026, 9, 1_000, previousRollover);
        var current = await AddBudgetAsync(fixture, 2026, 10, 1_000, false);
        await AddExpenseAsync(fixture, 2026, 9, previousSpent);

        var reader = new BudgetReader(fixture.Db);
        var calculation = await reader.CalculateAsync(
            fixture.UserId, fixture.CategoryId, current.Year, current.Month, default);
        var dto = await reader.ToDtoAsync(current, default);

        Assert.Equal(expectedCarry, calculation.RolloverAmount);
        Assert.Equal(1_000 + expectedCarry, calculation.EffectiveLimitAmount);
        Assert.Equal(1_000 + expectedCarry, dto.RemainingAmount);
    }

    [Fact]
    public async Task Budget_rollover_accumulates_across_consecutive_months()
    {
        await using var fixture = await CreateBudgetFixtureAsync();
        await AddBudgetAsync(fixture, 2026, 8, 1_000, true);
        await AddBudgetAsync(fixture, 2026, 9, 1_000, true);
        var october = await AddBudgetAsync(fixture, 2026, 10, 1_000, false);
        await AddExpenseAsync(fixture, 2026, 8, 200);
        await AddExpenseAsync(fixture, 2026, 9, 300);

        var reader = new BudgetReader(fixture.Db);
        var calculation = await reader.CalculateAsync(
            fixture.UserId, fixture.CategoryId, october.Year, october.Month, default);
        var dto = await reader.ToDtoAsync(october, default);

        Assert.Equal(1_500, calculation.RolloverAmount);
        Assert.Equal(2_500, calculation.EffectiveLimitAmount);
        Assert.Equal(2_500, dto.RemainingAmount);
    }

    private static async Task<BudgetFixture> CreateBudgetFixtureAsync()
    {
        var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Rollover user");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", true, balance: 10_000);
        var category = await fixture.Db.Categories.SingleAsync(item => item.Key == "categories.food");
        return new BudgetFixture(fixture, user.Id, wallet.Id, category.Id);
    }

    private static async Task<Budget> AddBudgetAsync(
        BudgetFixture fixture,
        int year,
        int month,
        decimal limit,
        bool rollover)
    {
        var budget = new Budget
        {
            OwnerUserId = fixture.UserId,
            CategoryId = fixture.CategoryId,
            Year = year,
            Month = month,
            LimitAmount = limit,
            Rollover = rollover
        };
        fixture.Db.Budgets.Add(budget);
        await fixture.Db.SaveChangesAsync();
        return budget;
    }

    private static async Task AddExpenseAsync(BudgetFixture fixture, int year, int month, decimal amount)
    {
        var local = new DateTime(year, month, 15, 12, 0, 0);
        fixture.Db.FinancialTransactions.Add(Expense(
            fixture.UserId,
            fixture.WalletId,
            fixture.CategoryId,
            amount,
            VietnamBusinessTime.ToUtc(local)));
        await fixture.Db.SaveChangesAsync();
    }

    private static FinancialTransaction Expense(
        Guid userId,
        Guid walletId,
        Guid categoryId,
        decimal amount,
        DateTime occurredAt) => new()
        {
            OwnerUserId = userId,
            WalletId = walletId,
            CategoryId = categoryId,
            LedgerTransaction = new LedgerTransaction
            {
                Reference = $"TEST-{Guid.NewGuid():N}"[..32],
                BusinessType = "TargetedReportTest",
                BusinessId = Guid.NewGuid(),
                Description = "Report fixture",
                Status = LedgerTransactionStatus.Completed,
                CompletedAt = occurredAt
            },
            Type = FinancialTransactionType.Expense,
            Status = FinancialTransactionStatus.Completed,
            Amount = amount,
            Description = "Boundary test",
            OccurredAt = occurredAt
        };

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class BudgetFixture(
        TargetedCorrectnessFixture inner,
        Guid userId,
        Guid walletId,
        Guid categoryId) : IAsyncDisposable
    {
        public SpendWise.Infrastructure.Persistence.SpendWiseDbContext Db => inner.Db;
        public Guid UserId { get; } = userId;
        public Guid WalletId { get; } = walletId;
        public Guid CategoryId { get; } = categoryId;
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
