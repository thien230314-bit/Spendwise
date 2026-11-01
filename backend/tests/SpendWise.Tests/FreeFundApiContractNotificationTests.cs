using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Notifications;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public class FreeFundApiContractNotificationTests
{
    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    [Fact]
    public async Task FreeFund_EquityEndpoint_ReturnsBreakdown_ActiveReservations_AndMemberEquities()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("EquityFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Member B creates an Expense of 200,000 VND and Owner approves -> ApprovedUnused reservation
        var req = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 200_000, memberB.Id, "Office supplies", "req-supplies", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ReviewFundRequestAsync(ownerA.Id, req.Id, true, "Approved supplies", TargetedCorrectnessFixture.SecurityPin, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Query Equity endpoint as Owner A
        var equityOwner = await s.EquityAsync(ownerA.Id, fund.Id, default);

        Assert.Equal(fund.Id, equityOwner.FundId);
        Assert.Equal("EquityFund", equityOwner.FundName);
        Assert.Equal("Free", equityOwner.FundKind);
        Assert.Equal(1_000_000, equityOwner.TotalBalanceVnd);
        Assert.Equal(200_000, equityOwner.ActiveReservedVnd);
        Assert.Equal(800_000, equityOwner.AvailableBalanceVnd);

        // Owner equity
        Assert.Equal(ownerA.Id, equityOwner.CurrentUserEquity.UserId);
        Assert.Equal(600_000, equityOwner.CurrentUserEquity.ContributedVnd);
        Assert.Equal(600_000, equityOwner.CurrentUserEquity.NetEquityVnd);
        Assert.Equal(600_000, equityOwner.CurrentUserEquity.EligibleRefundVnd);

        // Query Equity endpoint as Member B
        var equityB = await s.EquityAsync(memberB.Id, fund.Id, default);
        Assert.Equal(memberB.Id, equityB.CurrentUserEquity.UserId);
        Assert.Equal(400_000, equityB.CurrentUserEquity.ContributedVnd);
        Assert.Equal(400_000, equityB.CurrentUserEquity.NetEquityVnd);
        Assert.Equal(400_000, equityB.CurrentUserEquity.EligibleRefundVnd);
        Assert.Equal(2, equityB.MemberEquities.Count);
    }

    [Fact]
    public async Task FreeFund_GetRequest_Authorization_OwnerAndRequesterAllowed_OtherMembersForbidden()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var memberC = await f.AddUserAsync("MemberC");
        var strangerD = await f.AddUserAsync("StrangerD");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberC.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("AuthFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);
        var invC = await s.InviteAsync(ownerA.Id, fund.Id, memberC.Id, default);
        await s.AcceptAsync(memberC.Id, invC.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 500_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // B creates request
        var reqB = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 100_000, memberB.Id, "Tools", "req-b", TargetedCorrectnessFixture.SecurityPin), default);

        // 1. Requester B can fetch request detail
        var reqForB = await s.GetFundRequestAsync(memberB.Id, reqB.Id, default);
        Assert.Equal(reqB.Id, reqForB.Id);
        Assert.Equal(memberB.Id, reqForB.RequestedByUserId);

        // 2. Owner A can fetch request detail
        var reqForOwner = await s.GetFundRequestAsync(ownerA.Id, reqB.Id, default);
        Assert.Equal(reqB.Id, reqForOwner.Id);

        // 3. Other member C is forbidden from fetching B's request detail
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            s.GetFundRequestAsync(memberC.Id, reqB.Id, default));

        // 4. Stranger D (non-member) is forbidden
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            s.GetFundRequestAsync(strangerD.Id, reqB.Id, default));
    }

    [Fact]
    public async Task FreeFund_ListRequests_StrictDataIsolation_NonOwnersOnlySeeOwnRequests()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var memberC = await f.AddUserAsync("MemberC");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberC.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("IsolateRequestsFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);
        var invC = await s.InviteAsync(ownerA.Id, fund.Id, memberC.Id, default);
        await s.AcceptAsync(memberC.Id, invC.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);
        var walletC = await f.AddWalletAsync(memberC.Id, "WalletC", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 300_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberC.Id, fund.Id, new(walletC.Id, 200_000, "c-c", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // B creates 1 request
        await s.CreateFundRequestAsync(memberB.Id, fund.Id, new("Expense", 50_000, memberB.Id, "Item B1", "b1", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // C creates 1 request
        await s.CreateFundRequestAsync(memberC.Id, fund.Id, new("Expense", 70_000, memberC.Id, "Item C1", "c1", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Owner creates 1 direct expense
        await s.CreateFundRequestAsync(ownerA.Id, fund.Id, new("Expense", 40_000, ownerA.Id, "Item A1", "a1", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // 1. Member B only sees B1
        var requestsForB = await s.FundRequestsAsync(memberB.Id, fund.Id, 1, 20, default);
        Assert.Single(requestsForB);
        Assert.Equal(memberB.Id, requestsForB[0].RequestedByUserId);

        // 2. Member C only sees C1
        var requestsForC = await s.FundRequestsAsync(memberC.Id, fund.Id, 1, 20, default);
        Assert.Single(requestsForC);
        Assert.Equal(memberC.Id, requestsForC[0].RequestedByUserId);

        // 3. Owner A sees all 3 requests
        var requestsForOwner = await s.FundRequestsAsync(ownerA.Id, fund.Id, 1, 20, default);
        Assert.Equal(3, requestsForOwner.Count);
    }

    [Fact]
    public async Task FreeFund_TransactionsEndpoint_ReturnsPublicFundHistory_HidesPrivateWallets()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var strangerD = await f.AddUserAsync("StrangerD");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("TxFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Member B calls Transactions
        var txs = await s.FundTransactionsAsync(memberB.Id, fund.Id, 1, 20, default);
        Assert.NotEmpty(txs);
        Assert.All(txs, t =>
        {
            Assert.Equal(fund.Id, t.FundId);
            Assert.True(t.AmountVnd > 0);
            Assert.False(string.IsNullOrWhiteSpace(t.Description));
        });

        // Stranger D is rejected as non-member (NotFound to prevent leaking existence)
        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.FundTransactionsAsync(strangerD.Id, fund.Id, 1, 20, default));
    }

    [Fact]
    public async Task FreeFund_NotificationsAndOutbox_CreatedForFullLifecycleEvents()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("NotifFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 500_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // 1. B creates expense -> notification published
        var req = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 100_000, memberB.Id, "Food", "req-food", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        var notifs = await f.Db.Notifications.AsNoTracking().ToListAsync();
        Assert.Contains(notifs, n => n.Type == NotificationType.V1FundActivity && n.Message == "FundRequestCreated");

        var outbox = await f.Db.OutboxMessages.AsNoTracking().ToListAsync();
        Assert.NotEmpty(outbox);

        // 2. Owner approves -> notification published
        await s.ReviewFundRequestAsync(ownerA.Id, req.Id, true, "Approved food", TargetedCorrectnessFixture.SecurityPin, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        var notifsAfterApprove = await f.Db.Notifications.AsNoTracking().ToListAsync();
        Assert.Contains(notifsAfterApprove, n => n.Type == NotificationType.V1FundActivity && n.Message == "FundExpenseApproved");

        // 3. Fast forward 7 days + 1 min -> Expire stale approved request
        clock.UtcNow = clock.UtcNow.AddDays(7).AddMinutes(1);
        var expiredCount = await s.ExpireStaleApprovedUnusedRequestsAsync(fund.Id, default);
        Assert.Equal(1, expiredCount);

        var notifsAfterExpire = await f.Db.Notifications.AsNoTracking().ToListAsync();
        Assert.Contains(notifsAfterExpire, n => n.Type == NotificationType.V1FundActivity && n.Message == "FundRequestExpired");
    }
}
