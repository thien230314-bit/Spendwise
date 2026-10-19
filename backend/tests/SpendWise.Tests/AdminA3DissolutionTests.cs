using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA3DissolutionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task V1_marker_retained_membership_and_refund_availability_are_reported_independently(bool dissolved)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var a = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var fund = new V1Fund("V1", "SPW000001", V1FundKind.Fixed, user.Id, 100); var ticket = new SupportCase { AssignedAdminId = a.Id, LinkedUserId = user.Id };
        f.Db.AddRange(fund, ticket, new V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 1, LeftAt = dissolved ? f.Clock.UtcNow : null }); await f.Db.SaveChangesAsync();
        if (dissolved) await f.Db.V1Funds.Where(x => x.Id == fund.Id).ExecuteUpdateAsync(x => x.SetProperty(v => v.DissolvedAt, f.Clock.UtcNow));
        var p = V1PermissionTests.Principal(a.Id, (await f.AddSessionAsync(a.Id)).Id, "Admin"); var row = await AdminA3Tests.Service(f).DissolutionAsync(p, ticket.Id, fund.Id, "Trace V1 historical status", default);
        Assert.Equal(dissolved ? "Dissolved" : "Active", row.Status); Assert.Equal(!dissolved, row.CountsAsActiveGroup); Assert.Equal(user.Id, row.FounderUserId); Assert.Equal(user.Id, row.LastOwnerUserId);
        Assert.StartsWith("NOT_VERIFIED", row.RefundVerification); Assert.Equal("NOT_VERIFIED", row.LedgerZeroVerification); Assert.Equal("SPW000001", row.Code);
    }

    [Fact]
    public async Task Zero_snapshot_and_complete_recipient_records_mean_no_refund_due_not_a_money_transfer()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var a = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var group = new Group { Name = "Closed", Status = GroupStatus.Closed }; var ticket = new SupportCase { AssignedAdminId = a.Id, LinkedUserId = user.Id };
        var deletion = new GroupDeletionRequest { GroupId = group.Id, OwnerUserId = user.Id, Status = GroupDeletionRequestStatus.Completed };
        f.Db.AddRange(group, ticket, deletion, new GroupMember { GroupId = group.Id, UserId = user.Id, Role = GroupRole.Owner },
            new GroupDeletionVote { Request = deletion, UserId = user.Id, Status = GroupDeletionVoteStatus.Approved }, new GroupRefund { Request = deletion, UserId = user.Id, RefundAmount = 0, Status = GroupRefundStatus.Completed });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(a.Id, (await f.AddSessionAsync(a.Id)).Id, "Admin"); var row = await AdminA3Tests.Service(f).DissolutionAsync(p, ticket.Id, group.Id, "Verify no refund due", default);
        Assert.Equal("VERIFIED_NO_REFUND_DUE", row.RefundVerification); Assert.Equal("NOT_VERIFIED", row.LedgerZeroVerification); Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }

    [Theory]
    [InlineData("Complete", "VERIFIED", "VERIFIED_NET_ZERO", "VND")]
    [InlineData("ZeroWithoutRefund", "NOT_VERIFIED", "VERIFIED_NET_ZERO", "VND")]
    [InlineData("RefundWithoutOpeningHistory", "VERIFIED", "NOT_VERIFIED", "VND")]
    [InlineData("Complete", "VERIFIED", "VERIFIED_NET_ZERO", "USD")]
    public async Task Dissolution_refund_coverage_and_ledger_zero_are_independent(string condition, string refundProof, string zeroProof, string currency)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var a = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var wallet = await f.AddWalletAsync(user.Id, "Main", true); wallet.Currency = wallet.LedgerAccount.Currency = currency;
        var g = new Group { Name = "Closed", Status = GroupStatus.Closed, Currency = currency };
        var account = new LedgerAccount { Code = "FixtureFund", Type = LedgerAccountType.SharedFund, Currency = currency };
        var ticket = new SupportCase { AssignedAdminId = a.Id, LinkedUserId = user.Id };
        var deletion = new GroupDeletionRequest { GroupId = g.Id, OwnerUserId = user.Id, Status = GroupDeletionRequestStatus.Completed, CompletedAt = f.Clock.UtcNow, FundBalanceSnapshot = 100 };
        var refund = new GroupRefund { Request = deletion, UserId = user.Id, RefundAmount = 100, Status = GroupRefundStatus.Completed, DestinationWalletId = wallet.Id, CompletedAt = f.Clock.UtcNow };
        var posting = new LedgerTransaction { Reference = "FixtureRefund", BusinessType = "GroupRefund", BusinessId = refund.Id, Status = LedgerTransactionStatus.Completed,
            Entries = [new() { LedgerAccountId = account.Id, Direction = LedgerEntryDirection.Credit, Amount = 100, Currency = currency }, new() { LedgerAccountId = wallet.LedgerAccount.Id, Direction = LedgerEntryDirection.Debit, Amount = 100, Currency = currency }] };
        refund.LedgerTransactionId = posting.Id;
        f.Db.AddRange(g, account, ticket, deletion, posting, new GroupDeletionVote { Request = deletion, UserId = user.Id, Status = GroupDeletionVoteStatus.Approved },
            new GroupInvite { GroupId = g.Id, InvitedByUserId = user.Id, InvitedUserId = user.Id, Status = InviteStatus.Accepted }, new SharedFund { Group = g, LedgerAccount = account, Currency = currency });
        if (condition != "ZeroWithoutRefund") f.Db.Add(refund);
        if (condition != "RefundWithoutOpeningHistory") f.Db.Add(new LedgerTransaction { Reference = "FixtureOpening", Status = LedgerTransactionStatus.Completed,
            Entries = [new() { LedgerAccountId = account.Id, Direction = LedgerEntryDirection.Debit, Amount = 100, Currency = currency }, new() { LedgerAccountId = wallet.LedgerAccount.Id, Direction = LedgerEntryDirection.Credit, Amount = 100, Currency = currency }] });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(a.Id, (await f.AddSessionAsync(a.Id)).Id, "Admin");
        var result = await AdminA3Tests.Service(f).DissolutionAsync(p, ticket.Id, g.Id, "Verify historical closure", default);
        Assert.Equal(refundProof, result.RefundVerification); Assert.Equal(zeroProof, result.LedgerZeroVerification); Assert.False(result.CountsAsActiveGroup);
        Assert.Equal(condition == "ZeroWithoutRefund" ? 0 : 1, result.VerifiedRefunds); Assert.Equal(condition == "RefundWithoutOpeningHistory" ? -100 : 0, result.CompletedLedgerNet);
    }
}
