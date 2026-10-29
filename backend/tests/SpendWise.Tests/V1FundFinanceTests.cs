using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.V1Groups;
using SpendWise.Application.Notifications;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FundFinanceTests
{
    [Fact]
    public async Task Missing_treasury_ledger_entry_is_not_mistaken_for_reconciled_contributions()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var owner = await f.AddUserAsync("Owner");
        var s = V1FundMembershipTests.Service(f); var fund = await s.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var wallet = await f.AddWalletAsync(owner.Id, "Wallet", true, balance: 100);
        await s.ContributeAsync(owner.Id, fund.Id, new(wallet.Id, 20, "integrity", TargetedCorrectnessFixture.SecurityPin), default);
        var account = await f.Db.V1FundTreasuries.Select(t => t.LedgerAccountId).SingleAsync();
        await f.Db.LedgerEntries.Where(e => e.LedgerAccountId == account).ExecuteDeleteAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.FinanceAsync(owner.Id, fund.Id, default));
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("frozen")]
    [InlineData("insufficient")]
    [InlineData("mismatch")]
    public async Task Invalid_financial_state_does_not_change_wallet_or_create_ledger(string failure)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        var wallet = await f.AddWalletAsync(owner.Id, "Wallet", true, balance: failure == "insufficient" ? 10 : 100);
        var config = await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(100, TargetedCorrectnessFixture.SecurityPin), default);
        if (failure == "frozen") wallet.Status = SpendWise.Domain.Finance.WalletStatus.Frozen;
        if (failure == "mismatch") wallet.AvailableBalance = 99;
        await f.Db.SaveChangesAsync();
        var original = wallet.AvailableBalance;
        await Assert.ThrowsAnyAsync<AppException>(() => s.ContributeAsync(owner.Id, fund.Id,
            new(wallet.Id, 20, "rejected", TargetedCorrectnessFixture.SecurityPin, failure == "revision" ? config.Revision + 1 : config.Revision), default));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(original, await f.Db.Wallets.Select(w => w.AvailableBalance).SingleAsync());
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync()); Assert.Empty(await f.Db.V1FundContributions.ToListAsync());
    }

    [Fact]
    public async Task New_Free_fund_advertises_contribution_without_creating_a_treasury_on_read()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var state = await s.FinanceAsync(owner.Id, fund.Id, default);
        Assert.True(state.ConfigurationCurrent); Assert.True(state.ContributionsEnabled); Assert.Equal(0, state.BalanceVnd);
        Assert.Empty(await f.Db.V1FundTreasuries.ToListAsync()); Assert.Empty(await f.Db.LedgerAccounts.ToListAsync());
    }

    [Fact]
    public async Task Notification_failure_after_posting_rolls_back_wallet_ledger_and_business_history()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var wallet = await f.AddWalletAsync(owner.Id, "Wallet", true, balance: 100);
        var failing = new V1FundService(f.Db, f.Clock, f.Security, new FailingNotifications(), new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock), new SpendWise.Application.Features.Banking.ActiveBankGuard(f.Db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.ContributeAsync(owner.Id, fund.Id,
            new(wallet.Id, 20, "rollback", TargetedCorrectnessFixture.SecurityPin), default));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(100, await f.Db.Wallets.Select(w => w.AvailableBalance).SingleAsync());
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync()); Assert.Empty(await f.Db.LedgerEntries.ToListAsync());
        Assert.Empty(await f.Db.V1FundContributions.ToListAsync()); Assert.Empty(await f.Db.FinancialTransactions.ToListAsync());
        Assert.Empty(await f.Db.V1FundTreasuries.ToListAsync());
        Assert.Equal("Created", Assert.Single(await f.Db.V1FundActivities.ToListAsync()).Event);
    }

    private sealed class FailingNotifications : INotificationService
    {
        public Task<NotificationDto> CreateAsync(Guid id, string type, string title, string message, object? data, CancellationToken ct) => throw new InvalidOperationException("Synthetic notification failure");
        public Task<IReadOnlyList<NotificationDto>> CreateManyAsync(IReadOnlyCollection<CreateNotificationRequest> requests, CancellationToken ct) => throw new InvalidOperationException("Synthetic notification failure");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Invalid_or_excess_contribution_never_posts(long amount)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        var wallet = await f.AddWalletAsync(owner.Id, "Wallet", true, balance: 200);
        var state = await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(100, TargetedCorrectnessFixture.SecurityPin), default);
        await Assert.ThrowsAnyAsync<AppException>(() => s.ContributeAsync(owner.Id, fund.Id,
            new(wallet.Id, amount, "invalid", TargetedCorrectnessFixture.SecurityPin, state.Revision), default));
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
        Assert.Equal(200, await f.Db.Wallets.Select(w => w.AvailableBalance).SingleAsync());
    }

    [Fact]
    public async Task Membership_change_blocks_old_revision_and_reconfirmation_preserves_paid_history()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id); var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        var wallet = await f.AddWalletAsync(owner.Id, "Wallet", true, balance: 200);
        var old = await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(100, TargetedCorrectnessFixture.SecurityPin), default);
        var request = new V1ContributeRequest(wallet.Id, 80, "paid", TargetedCorrectnessFixture.SecurityPin, old.Revision);
        var posted = await s.ContributeAsync(owner.Id, fund.Id, request, default);
        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default); await s.AcceptAsync(member.Id, invite.Id, default);
        Assert.False((await s.FinanceAsync(owner.Id, fund.Id, default)).ContributionsEnabled);
        await Assert.ThrowsAsync<ConflictException>(() => s.ContributeAsync(owner.Id, fund.Id, request with { AmountVnd = 1, IdempotencyKey = "stale" }, default));
        var next = await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(100, TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal(old.Revision + 1, next.Revision); Assert.Equal(100, next.Obligations.Sum(o => o.RequiredVnd));
        Assert.Equal(30, next.Obligations.Single(o => o.UserId == owner.Id).LockedExcessVnd);
        Assert.Equal(80, next.BalanceVnd); Assert.Equal("FINANCE_BLOCKED", next.SpendingState);
        Assert.Equal(posted.Id, (await s.ContributeAsync(owner.Id, fund.Id, request, default)).Id);
        Assert.Single(await f.Db.V1FundContributions.ToListAsync());
        Assert.Equal(3, await f.Db.V1FundObligations.CountAsync());
        Assert.Equal(next.Revision, (await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(100, TargetedCorrectnessFixture.SecurityPin), default)).Revision);
    }

    [Fact]
    public async Task Free_contribution_requires_own_wallet_pin_and_exact_idempotent_intent()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var other = await f.AddUserAsync("Other"); var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var wallet = await f.AddWalletAsync(owner.Id, "Wallet", true, balance: 100);
        var foreign = await f.AddWalletAsync(other.Id, "Foreign", true, balance: 100);
        var request = new V1ContributeRequest(wallet.Id, 20, "once", TargetedCorrectnessFixture.SecurityPin);
        await Assert.ThrowsAsync<NotFoundException>(() => s.ContributeAsync(owner.Id, fund.Id, request with { WalletId = foreign.Id }, default));
        await Assert.ThrowsAnyAsync<AppException>(() => s.ContributeAsync(owner.Id, fund.Id, request with { SecurityPin = "999999" }, default));
        var first = await s.ContributeAsync(owner.Id, fund.Id, request, default);
        Assert.Equal(first.Id, Assert.Single(await s.ContributionsAsync(owner.Id, fund.Id, 1, 20, default)).Id);
        Assert.Equal(first.Id, (await s.ActivitiesAsync(owner.Id, fund.Id, 1, 20, default)).Single(a => a.Event == "ContributionPosted").ReferenceId);
        Assert.True((await s.ContributeAsync(owner.Id, fund.Id, request, default)).Replayed);
        await Assert.ThrowsAsync<ConflictException>(() => s.ContributeAsync(owner.Id, fund.Id, request with { AmountVnd = 21 }, default));
        await Assert.ThrowsAsync<NotFoundException>(() => s.FinanceAsync(other.Id, fund.Id, default));
        Assert.Single(await f.Db.V1FundContributions.ToListAsync());
        Assert.Equal(80, await f.Db.Wallets.Where(w => w.Id == wallet.Id).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Equal(first.LedgerTransactionId, await f.Db.FinancialTransactions.Select(t => t.LedgerTransactionId).SingleAsync());
    }

    [Fact]
    public async Task Finance_reader_rejects_unreconciled_treasury_instead_of_advertising_contributions()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(100, TargetedCorrectnessFixture.SecurityPin), default);
        var treasury = await f.Db.V1FundTreasuries.SingleAsync();
        await f.Db.LedgerAccounts.Where(a => a.Id == treasury.LedgerAccountId).ExecuteUpdateAsync(p => p.SetProperty(a => a.Balance, 1m));
        await Assert.ThrowsAsync<ConflictException>(() => s.FinanceAsync(owner.Id, fund.Id, default));
    }
}
