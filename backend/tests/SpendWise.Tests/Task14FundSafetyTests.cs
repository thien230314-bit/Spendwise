using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class Task14FundSafetyTests
{
    private const string Pin = TargetedCorrectnessFixture.SecurityPin;

    [Fact]
    public async Task Free_fund_accepts_contributions_after_withdrawal_and_dissolves_exact_remaining_balance()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(new Clock());
        var (service, owner, fund) = await FundAsync(f, "Free");
        var wallet = await f.Db.Wallets.AsNoTracking().SingleAsync(x => x.OwnerUserId == owner);
        var withdrawal = await service.CreateFundRequestAsync(owner, fund,
            new("Withdrawal", 50, owner, "Approved withdrawal", "withdraw-before-contribution", Pin), default);
        Assert.Equal("Completed", (await service.ReviewFundRequestAsync(owner, withdrawal.Id, true, "Approve withdrawal", Pin, default)).State);
        Assert.Equal(150, await TreasuryBalanceAsync(f, fund));
        var input = new V1ContributeRequest(wallet.Id, 100, "contribute-after-withdrawal", Pin);

        var contribution = await service.ContributeAsync(owner, fund, input, default);

        Assert.Equal(250, await TreasuryBalanceAsync(f, fund));
        Assert.Equal(300, await f.Db.V1FundContributions.Where(x => x.FundId == fund).SumAsync(x => x.AmountVnd));
        Assert.Equal(50, await f.Db.FinancialTransactions.Where(x => x.RelatedBusinessId == fund && x.Type == FinancialTransactionType.Withdrawal).SumAsync(x => x.Amount));
        var after = await SnapshotAsync(f);
        var replay = await service.ContributeAsync(owner, fund, input, default);
        Assert.True(replay.Replayed);
        Assert.Equal(contribution.LedgerTransactionId, replay.LedgerTransactionId);
        Assert.Equal(after, await SnapshotAsync(f));

        var dissolved = await service.DissolveFundAsync(owner, fund, new(Pin, "dissolve-after-contribution"), default);
        Assert.Equal(250, dissolved.TotalRefundedVnd);
        Assert.Equal(0, dissolved.FinalBalanceVnd);
        Assert.Equal(0, await TreasuryBalanceAsync(f, fund));
        Assert.Equal(1000, await f.Db.Wallets.AsNoTracking().Where(x => x.Id == wallet.Id).Select(x => x.AvailableBalance).SingleAsync());
        var postings = await f.Db.LedgerTransactions.AsNoTracking().Include(x => x.Entries).ToListAsync();
        Assert.Equal(4, postings.Count);
        Assert.All(postings, posting => Assert.Equal(
            posting.Entries.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount),
            posting.Entries.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount)));
    }

    [Fact]
    public async Task Fixed_cumulative_spending_is_group_wide_and_second_twenty_waits_24_hours()
    {
        var clock = new Clock();
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var member = await f.AddUserAsync("Member");
        var (service, owner, fund) = await FundAsync(f, "Fixed", member.Id);
        await service.SetSpendingPermissionAsync(owner, fund, member.Id, true, Pin, default);
        var recipient = await f.AddUserAsync("Recipient");
        var wallet = await f.AddWalletAsync(recipient.Id, "Recipient", true);

        var first = await service.CreateFundRequestAsync(owner, fund,
            new("Expense", 20, recipient.Id, "First expense", "first-twenty", Pin), default);
        Assert.Equal("Completed", first.State);
        var beforeHold = await SnapshotAsync(f);
        var second = await service.CreateFundRequestAsync(member.Id, fund,
            new("Expense", 20, recipient.Id, "Second expense", "second-twenty", Pin), default);
        Assert.Equal("Pending24h", second.State);
        Assert.Equal(beforeHold, await SnapshotAsync(f));
        Assert.Equal(180, await TreasuryBalanceAsync(f, fund));

        clock.UtcNow = clock.UtcNow.AddHours(24);
        Assert.Equal("Completed", (await service.ReviewFundRequestAsync(owner, second.Id, true, "Mature hold", Pin, default)).State);
        Assert.Equal(160, await TreasuryBalanceAsync(f, fund));
        Assert.Equal(40, await f.Db.Wallets.AsNoTracking().Where(x => x.Id == wallet.Id).Select(x => x.AvailableBalance).SingleAsync());
    }

    [Theory]
    [InlineData("Reject")]
    [InlineData("Cancel")]
    public async Task Held_expense_can_be_rejected_or_cancelled_before_24_hours_without_money(string operation)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(new Clock());
        var (service, owner, fund) = await FundAsync(f, "Fixed");
        var recipient = await f.AddUserAsync("Recipient");
        await f.AddWalletAsync(recipient.Id, "Recipient", true);
        var held = await service.CreateFundRequestAsync(owner, fund,
            new("Expense", 50, recipient.Id, "Held expense", "stop-hold", Pin), default);
        var before = await SnapshotAsync(f);

        var stopped = operation == "Reject"
            ? await service.ReviewFundRequestAsync(owner, held.Id, false, "Rejected during hold", Pin, default)
            : await service.CancelFundRequestAsync(owner, held.Id, Pin, default);

        Assert.Equal(operation == "Reject" ? "Rejected" : "Cancelled", stopped.State);
        Assert.Equal(before, await SnapshotAsync(f));
    }

    [Fact]
    public async Task Revoked_spending_permission_prevents_mature_held_expense_execution()
    {
        var clock = new Clock();
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var member = await f.AddUserAsync("Member");
        var (service, owner, fund) = await FundAsync(f, "Fixed", member.Id);
        await service.SetSpendingPermissionAsync(owner, fund, member.Id, true, Pin, default);
        var recipient = await f.AddUserAsync("Recipient");
        await f.AddWalletAsync(recipient.Id, "Recipient", true);
        var held = await service.CreateFundRequestAsync(member.Id, fund,
            new("Expense", 50, recipient.Id, "Held expense", "revoked-hold", Pin), default);
        await service.SetSpendingPermissionAsync(owner, fund, member.Id, false, Pin, default);
        clock.UtcNow = clock.UtcNow.AddHours(24);
        var before = await SnapshotAsync(f);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviewFundRequestAsync(owner, held.Id, true, "Review after revocation", Pin, default));

        Assert.Equal("fund_spending_permission_required", error.Code);
        Assert.Equal(before, await SnapshotAsync(f));
        Assert.Equal("Pending24h", await f.Db.V1FundRequests.AsNoTracking().Where(x => x.Id == held.Id).Select(x => x.State).SingleAsync());
    }

    [Fact]
    public async Task Accepted_member_invalidates_fixed_obligation_configuration_before_spending()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(new Clock());
        var (service, owner, fund) = await FundAsync(f, "Fixed");
        var member = await f.AddUserAsync("New member");
        await V1FundMembershipTests.MakeFriends(f, owner, member.Id);
        var invitation = await service.InviteAsync(owner, fund, member.Id, default);
        await service.AcceptAsync(member.Id, invitation.Id, default);
        var recipient = await f.AddUserAsync("Recipient");
        await f.AddWalletAsync(recipient.Id, "Recipient", true);
        var before = await SnapshotAsync(f);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateFundRequestAsync(owner, fund,
            new("Expense", 10, recipient.Id, "Stale obligations", "stale-config", Pin), default));

        Assert.Equal(before, await SnapshotAsync(f));
        Assert.Empty(await f.Db.V1FundRequests.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Different_members_using_same_refund_key_both_leave_and_receive_own_refunds()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(new Clock());
        var firstMember = await f.AddUserAsync("First member");
        var (service, owner, fund) = await FundAsync(f, "Free", firstMember.Id);
        var secondMember = await f.AddUserAsync("Second member");
        var secondWallet = await f.AddWalletAsync(secondMember.Id, "Second member", true, balance: 1000);
        f.Db.V1FundMemberships.Add(new V1FundMembership { FundId = fund, UserId = secondMember.Id, Seat = 3 });
        await f.Db.SaveChangesAsync();
        await service.ContributeAsync(secondMember.Id, fund, new(secondWallet.Id, 100, "second-seed", Pin), default);
        var before = await SnapshotAsync(f);

        var first = await service.LeaveFundAsync(firstMember.Id, fund, new(Pin, "same-refund-key"), default);
        var second = await service.LeaveFundAsync(secondMember.Id, fund, new(Pin, "same-refund-key"), default);

        Assert.Equal(firstMember.Id, first.UserId);
        Assert.Equal(secondMember.Id, second.UserId);
        Assert.Equal(100, first.RefundAmountVnd);
        Assert.Equal(100, second.RefundAmountVnd);
        Assert.NotNull(first.LedgerTransactionId);
        Assert.NotNull(second.LedgerTransactionId);
        Assert.NotEqual(first.LedgerTransactionId, second.LedgerTransactionId);
        Assert.Equal(before.Postings + 2, (await SnapshotAsync(f)).Postings);
        Assert.Equal(2, await f.Db.V1FundMemberships.AsNoTracking().CountAsync(x => x.FundId == fund && x.UserId != owner && x.LeftAt != null));
        Assert.Equal(100, await TreasuryBalanceAsync(f, fund));
        var balances = await f.Db.Wallets.AsNoTracking().Where(x => x.OwnerUserId == firstMember.Id || x.OwnerUserId == secondMember.Id).Select(x => x.AvailableBalance).ToListAsync();
        Assert.Equal(2, balances.Count);
        Assert.All(balances, balance => Assert.Equal(1000, balance));
        var after = await SnapshotAsync(f);
        await service.LeaveFundAsync(firstMember.Id, fund, new(Pin, "same-refund-key"), default);
        await service.LeaveFundAsync(secondMember.Id, fund, new(Pin, "same-refund-key"), default);
        Assert.Equal(after, await SnapshotAsync(f));
    }

    [Theory]
    [InlineData("Preview")]
    [InlineData("Leave")]
    [InlineData("Kick")]
    [InlineData("Dissolve")]
    public async Task Pending_expense_hold_blocks_refund_until_policy_is_resolved(string operation)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(new Clock());
        var member = await f.AddUserAsync("Paid member");
        var (service, owner, fund) = await FundAsync(f, "Fixed", member.Id);
        var recipient = await f.AddUserAsync("Recipient");
        await f.AddWalletAsync(recipient.Id, "Recipient", true);
        var held = await service.CreateFundRequestAsync(owner, fund,
            new("Expense", 50, recipient.Id, "Held expense", "refund-hold", Pin), default);
        Assert.Equal("Pending24h", held.State);
        var before = await SnapshotAsync(f);

        var error = await Record.ExceptionAsync(async () =>
        {
            if (operation == "Preview") await service.PreviewRefundAsync(member.Id, fund, null, default);
            else if (operation == "Leave") await service.LeaveFundAsync(member.Id, fund, new(Pin, "leave-hold"), default);
            else if (operation == "Kick") await service.KickMemberAsync(owner, fund, member.Id, new(Pin, "Test kick", "kick-hold"), default);
            else await service.DissolveFundAsync(owner, fund, new(Pin, "dissolve-hold"), default);
        });

        Assert.Equal(before, await SnapshotAsync(f));
        Assert.Null(await f.Db.V1Funds.AsNoTracking().Where(x => x.Id == fund).Select(x => x.DissolvedAt).SingleAsync());
        Assert.Null(await f.Db.V1FundMemberships.AsNoTracking().Where(x => x.FundId == fund && x.UserId == member.Id).Select(x => x.LeftAt).SingleAsync());
        Assert.IsType<ConflictException>(error);
    }

    [Theory]
    [InlineData("Free")]
    [InlineData("Fixed")]
    public async Task Direct_expense_without_recipient_wallet_fails_without_money_or_completion(string kind)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(new Clock());
        var (service, owner, fund) = await FundAsync(f, kind);
        var recipient = await f.AddUserAsync("Recipient without wallet");
        var before = await SnapshotAsync(f);

        var error = await Record.ExceptionAsync(() => service.CreateFundRequestAsync(owner, fund,
            new("Expense", 10, recipient.Id, "Test expense", "missing-wallet", Pin), default));

        Assert.Equal(before, await SnapshotAsync(f));
        Assert.Empty(await f.Db.V1FundRequests.AsNoTracking().ToListAsync());
        Assert.IsType<ConflictException>(error);
        Assert.Equal("wallet_unavailable", ((ConflictException)error!).Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(86399)]
    public async Task Held_expense_cannot_be_approved_before_24_hours(int seconds)
    {
        var clock = new Clock();
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var (service, owner, fund) = await FundAsync(f, "Fixed");
        var recipient = await f.AddUserAsync("Recipient");
        await f.AddWalletAsync(recipient.Id, "Recipient", true);
        var held = await service.CreateFundRequestAsync(owner, fund,
            new("Expense", 50, recipient.Id, "Held expense", "hold", Pin), default);
        Assert.Equal("Pending24h", held.State);
        var before = await SnapshotAsync(f);
        clock.UtcNow = clock.UtcNow.AddSeconds(seconds);

        var error = await Record.ExceptionAsync(() => service.ReviewFundRequestAsync(owner, held.Id,
            true, "Approval during hold", Pin, default));

        Assert.Equal(before, await SnapshotAsync(f));
        Assert.Equal("Pending24h", await f.Db.V1FundRequests.AsNoTracking().Where(x => x.Id == held.Id).Select(x => x.State).SingleAsync());
        Assert.IsType<ConflictException>(error);
    }

    [Fact]
    public async Task Held_expense_at_24_hours_executes_once_and_replays_without_more_money()
    {
        var clock = new Clock();
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var (service, owner, fund) = await FundAsync(f, "Fixed");
        var recipient = await f.AddUserAsync("Recipient");
        var wallet = await f.AddWalletAsync(recipient.Id, "Recipient", true);
        var input = new V1FundRequestInput("Expense", 50, recipient.Id, "Held expense", "mature-hold", Pin);
        var held = await service.CreateFundRequestAsync(owner, fund, input, default);
        var before = await SnapshotAsync(f);
        clock.UtcNow = clock.UtcNow.AddHours(24);

        var executed = await service.ReviewFundRequestAsync(owner, held.Id, true, "Mature approval", Pin, default);
        Assert.Equal("Completed", executed.State);
        var after = await SnapshotAsync(f);
        Assert.Equal(before.Postings + 1, after.Postings);
        Assert.Equal(before.FinancialTransactions + 1, after.FinancialTransactions);
        Assert.Equal(150, await TreasuryBalanceAsync(f, fund));
        Assert.Equal(50, await f.Db.Wallets.AsNoTracking().Where(x => x.Id == wallet.Id).Select(x => x.AvailableBalance).SingleAsync());

        Assert.Equal(executed.Id, (await service.ReviewFundRequestAsync(owner, held.Id, true, "Replay approval", Pin, default)).Id);
        Assert.Equal(executed.Id, (await service.CreateFundRequestAsync(owner, fund, input, default)).Id);
        Assert.Equal(after, await SnapshotAsync(f));
    }

    [Fact]
    public async Task Different_members_can_use_same_expense_key_without_sharing_a_posting()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(new Clock());
        var ownerUser = await f.AddUserAsync("Owner");
        var owner = ownerUser.Id;
        var member = await f.AddUserAsync("Member");
        var service = V1ElectionTests.Configured(f);
        var fund = (await service.CreateAsync(owner, new("Fixed collision", "Fixed", 200, Pin), default)).Id;
        await service.ConfirmObligationsAsync(owner, fund, new(200, Pin), default);
        await V1FundMembershipTests.MakeFriends(f, owner, member.Id);
        var invitation = await service.InviteAsync(owner, fund, member.Id, default);
        await service.AcceptAsync(member.Id, invitation.Id, default);
        var revision = (await service.ConfirmObligationsAsync(owner, fund, new(200, Pin), default)).Revision;
        var ownerWallet = await f.AddWalletAsync(owner, "Owner", true, balance: 1000);
        var memberWallet = await f.AddWalletAsync(member.Id, "Member", true, balance: 1000);
        await service.ContributeAsync(owner, fund, new(ownerWallet.Id, 100, "owner-seed", Pin, revision), default);
        await service.ContributeAsync(member.Id, fund, new(memberWallet.Id, 100, "member-seed", Pin, revision), default);
        await service.SetSpendingPermissionAsync(owner, fund, member.Id, true, Pin, default);
        var recipient = await f.AddUserAsync("Recipient");
        var wallet = await f.AddWalletAsync(recipient.Id, "Recipient", true);
        var before = await SnapshotAsync(f);
        var input = new V1FundRequestInput("Expense", 10, recipient.Id, "Same expense intent", "shared-key", Pin);

        var first = await service.CreateFundRequestAsync(owner, fund, input, default);
        var second = await service.CreateFundRequestAsync(member.Id, fund, input, default);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("Completed", first.State);
        Assert.Equal("Completed", second.State);
        Assert.Equal(before.Postings + 2, (await SnapshotAsync(f)).Postings);
        Assert.Equal(180, await TreasuryBalanceAsync(f, fund));
        Assert.Equal(20, await f.Db.Wallets.AsNoTracking().Where(x => x.Id == wallet.Id).Select(x => x.AvailableBalance).SingleAsync());
        var after = await SnapshotAsync(f);
        Assert.Equal(first.Id, (await service.CreateFundRequestAsync(owner, fund, input, default)).Id);
        Assert.Equal(second.Id, (await service.CreateFundRequestAsync(member.Id, fund, input, default)).Id);
        Assert.Equal(after, await SnapshotAsync(f));
    }

    [Fact]
    public async Task Free_member_expense_waits_for_owner_and_approved_request_executes_once()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(new Clock());
        var member = await f.AddUserAsync("Member");
        var (service, owner, fund) = await FundAsync(f, "Free", member.Id);
        var recipient = await f.AddUserAsync("Recipient");
        var wallet = await f.AddWalletAsync(recipient.Id, "Recipient", true);
        var input = new V1FundRequestInput("Expense", 10, recipient.Id, "Member expense", "free-member", Pin);
        var before = await SnapshotAsync(f);

        var request = await service.CreateFundRequestAsync(member.Id, fund, input, default);

        Assert.Equal("AwaitingOwnerReview", request.State);
        Assert.Equal(before, await SnapshotAsync(f));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviewFundRequestAsync(member.Id, request.Id, true, "Self approval", Pin, default));
        Assert.Equal(before, await SnapshotAsync(f));
        var approved = await service.ReviewFundRequestAsync(owner, request.Id, true, "Owner approval", Pin, default);
        Assert.Equal("Completed", approved.State);
        Assert.Equal(before.Postings + 1, (await SnapshotAsync(f)).Postings);
        Assert.Equal(190, await TreasuryBalanceAsync(f, fund));
        Assert.Equal(10, await f.Db.Wallets.AsNoTracking().Where(x => x.Id == wallet.Id).Select(x => x.AvailableBalance).SingleAsync());
        var after = await SnapshotAsync(f);
        Assert.Equal(request.Id, (await service.ReviewFundRequestAsync(owner, request.Id, true, "Approval replay", Pin, default)).Id);
        Assert.Equal(request.Id, (await service.CreateFundRequestAsync(member.Id, fund, input, default)).Id);
        Assert.Equal(after, await SnapshotAsync(f));
    }

    private static async Task<(V1FundService Service, Guid Owner, Guid Fund)> FundAsync(TargetedCorrectnessFixture f, string kind, Guid? member = null)
    {
        var owner = await f.AddUserAsync("Owner");
        var wallet = await f.AddWalletAsync(owner.Id, "Owner", true, balance: 1000);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("Task14 fund", kind, kind == "Fixed" ? 200 : null, Pin), default);
        if (member is not null)
        {
            f.Db.V1FundMemberships.Add(new V1FundMembership { FundId = fund.Id, UserId = member.Value, Seat = 2 });
            await f.Db.SaveChangesAsync();
        }
        long? revision = null;
        if (kind == "Fixed")
            revision = (await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default)).Revision;
        await service.ContributeAsync(owner.Id, fund.Id, new(wallet.Id, member is null ? 200 : 100, "seed", Pin, revision), default);
        if (member is not null)
        {
            var memberWallet = await f.AddWalletAsync(member.Value, "Member", true, balance: 1000);
            await service.ContributeAsync(member.Value, fund.Id, new(memberWallet.Id, 100, "member-seed", Pin, revision), default);
        }
        return (service, owner.Id, fund.Id);
    }

    private static async Task<decimal> TreasuryBalanceAsync(TargetedCorrectnessFixture f, Guid fund)
    {
        var account = await f.Db.V1FundTreasuries.AsNoTracking().Where(x => x.FundId == fund).Select(x => x.LedgerAccountId).SingleAsync();
        return await f.Db.LedgerAccounts.AsNoTracking().Where(x => x.Id == account).Select(x => x.Balance).SingleAsync();
    }

    private static async Task<(int Postings, int Entries, int FinancialTransactions, string Balances)> SnapshotAsync(TargetedCorrectnessFixture f)
    {
        var accounts = await f.Db.LedgerAccounts.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Balance }).ToListAsync();
        return (await f.Db.LedgerTransactions.CountAsync(), await f.Db.LedgerEntries.CountAsync(),
            await f.Db.FinancialTransactions.CountAsync(), string.Join("|", accounts.Select(x => $"{x.Id}:{x.Balance}")));
    }

    private sealed class Clock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
    }
}
