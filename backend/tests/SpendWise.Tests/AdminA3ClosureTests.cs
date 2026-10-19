using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA3ClosureTests
{
    [Fact]
    public async Task V1_timeline_correlates_only_same_fund_records_and_keeps_distinct_event_ids()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id }; var fund = new V1Fund("Fund", "SPW000001", V1FundKind.Free, user.Id, null);
        var foreign = new V1Fund("Foreign", "SPW000002", V1FundKind.Free, user.Id, null);
        var invite = new V1FundInvitation { FundId = fund.Id, InvitedUserId = user.Id, InvitedByUserId = user.Id, Status = V1InvitationStatus.Accepted, AcceptedAt = f.Clock.UtcNow, ExpiresAt = f.Clock.UtcNow.AddDays(1) };
        var other = new V1FundInvitation { FundId = foreign.Id, InvitedUserId = user.Id, InvitedByUserId = user.Id, ExpiresAt = f.Clock.UtcNow.AddDays(1) };
        var offer = new V1OwnershipOffer { FundId = fund.Id, FromUserId = user.Id, ToUserId = user.Id, AcceptedAt = f.Clock.UtcNow };
        var joined = new V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = "Joined", ReferenceId = invite.Id, Detail = "private-note" };
        f.Db.AddRange(ticket, fund, foreign, invite, other, offer, new V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 1 }, joined,
            new V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = "Joined", ReferenceId = invite.Id },
            new V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = "Invited", ReferenceId = other.Id },
            new V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = "OwnershipAccepted", ReferenceId = offer.Id },
            new V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = "ContributionPosted" });
        foreach (var (action, status) in new[] { ("InvitationDeclined", V1InvitationStatus.Rejected), ("InvitationRejected", V1InvitationStatus.Rejected), ("InvitationCancelled", V1InvitationStatus.Cancelled) })
        {
            var decision = new V1FundInvitation { FundId = fund.Id, InvitedByUserId = user.Id, InvitedUserId = user.Id, Status = status, ExpiresAt = f.Clock.UtcNow.AddDays(1) };
            f.Db.AddRange(decision, new V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = action, ReferenceId = decision.Id });
        }
        var malformedActor = new V1FundActivity { FundId = fund.Id, ActorUserId = actor.Id, Event = "Joined", ReferenceId = invite.Id }; f.Db.Add(malformedActor);
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var s = AdminA3Tests.Service(f);
        var timeline = await s.ActivityAsync(p, ticket.Id, fund.Id, new("Trace history"), default);
        Assert.Equal(8, timeline.Total); Assert.Equal(8, timeline.Items.Select(x => x.Id).Distinct().Count());
        var item = Assert.Single(timeline.Items, x => x.Id == joined.Id); Assert.Equal("Accepted", item.Outcome); Assert.Equal(invite.Id, item.CorrelationId); Assert.Equal("MATCHED", item.ReferenceVerification); Assert.Equal("NOT_RECORDED", item.Cause);
        Assert.Equal("NOT_FOUND_IN_FUND", Assert.Single(timeline.Items, x => x.ReferenceId == other.Id).ReferenceVerification);
        Assert.DoesNotContain("private-note", System.Text.Json.JsonSerializer.Serialize(timeline));
        Assert.Equal(4, (await s.InvitationsAsync(p, ticket.Id, fund.Id, new("Read invitation records"), default)).Total);
        Assert.Equal("REQUIRES_REVIEW", Assert.Single(timeline.Items, x => x.Id == malformedActor.Id).ReferenceVerification);
        var first = await s.ActivityAsync(p, ticket.Id, fund.Id, new("Page history", PageSize: 2), default); var second = await s.ActivityAsync(p, ticket.Id, fund.Id, new("Page history", Page: 2, PageSize: 2), default);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
    }

    [Theory]
    [InlineData("Contribution", "Valid", "LEDGER_COMPLETED")]
    [InlineData("Expense", "Valid", "LEDGER_COMPLETED")]
    [InlineData("Refund", "Valid", "LEDGER_COMPLETED")]
    [InlineData("Contribution", "MissingBusiness", "UNVERIFIED")]
    [InlineData("Expense", "WrongGroup", "LEDGER_REQUIRES_REVIEW")]
    [InlineData("Refund", "WrongUser", "LEDGER_REQUIRES_REVIEW")]
    [InlineData("Refund", "Pending", "LEDGER_REQUIRES_REVIEW")]
    [InlineData("Expense", "Unbalanced", "LEDGER_REQUIRES_REVIEW")]
    [InlineData("Contribution", "WrongCurrency", "LEDGER_REQUIRES_REVIEW")]
    [InlineData("Contribution", "MissingLedger", "MISSING_LEDGER")]
    [InlineData("Refund", "MissingLedger", "MISSING_LEDGER")]
    [InlineData("Refund", "Unbalanced", "LEDGER_REQUIRES_REVIEW")]
    [InlineData("Expense", "MissingBusiness", "UNVERIFIED")]
    [InlineData("Contribution", "WrongDirection", "LEDGER_REQUIRES_REVIEW")]
    [InlineData("Contribution", "HistoryMismatch", "LEDGER_REQUIRES_REVIEW")]
    public async Task Legacy_finance_verifies_business_provenance_instead_of_balance_alone(string kind, string condition, string expected)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var a = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject"); var other = await f.AddUserAsync("Other");
        var wallet = await f.AddWalletAsync(user.Id, "Main", true); var otherWallet = await f.AddWalletAsync(other.Id, "Other", true);
        var ticket = new SupportCase { AssignedAdminId = a.Id, LinkedUserId = user.Id }; var group = new Group { Name = "Legacy" }; var foreign = new Group { Name = "Foreign" };
        var account = new LedgerAccount { Code = "FixtureFund", Type = LedgerAccountType.SharedFund, GroupId = group.Id }; var clearing = new LedgerAccount { Code = "SYSTEM:GroupExpenseClearing:VND", Type = LedgerAccountType.ExpenseClearing, IsSystem = true };
        var posting = new LedgerTransaction { Reference = "FixturePosting", BusinessType = kind == "Contribution" ? "GroupContribution" : kind == "Expense" ? "GroupFundExpense" : "GroupRefund", Status = condition == "Pending" ? LedgerTransactionStatus.Pending : LedgerTransactionStatus.Completed };
        var contribution = new GroupContribution { GroupId = group.Id, UserId = user.Id, WalletId = wallet.Id, Amount = 100, LedgerTransactionId = posting.Id };
        var expense = new GroupExpense { GroupId = condition == "WrongGroup" ? foreign.Id : group.Id, Amount = 100, PaidByType = GroupExpensePaidByType.Fund };
        var deletion = new GroupDeletionRequest { GroupId = group.Id, OwnerUserId = user.Id, Status = GroupDeletionRequestStatus.Completed, CompletedAt = f.Clock.UtcNow, FundBalanceSnapshot = 100 };
        var refund = new GroupRefund { Request = deletion, UserId = user.Id, RefundAmount = 100, DestinationWalletId = wallet.Id, LedgerTransactionId = condition == "MissingLedger" ? null : posting.Id, Status = GroupRefundStatus.Completed, CompletedAt = f.Clock.UtcNow };
        posting.BusinessId = condition == "MissingBusiness" ? Guid.NewGuid() : kind == "Contribution" ? contribution.Id : kind == "Expense" ? expense.Id : refund.Id;
        var outflow = kind != "Contribution";
        posting.Entries = [new() { LedgerAccountId = account.Id, Amount = 100, Direction = outflow ? LedgerEntryDirection.Credit : LedgerEntryDirection.Debit },
            new() { LedgerAccountId = kind == "Expense" ? clearing.Id : condition == "WrongUser" ? otherWallet.LedgerAccount.Id : wallet.LedgerAccount.Id, Amount = condition == "Unbalanced" ? 90 : 100, Currency = condition == "WrongCurrency" ? "USD" : "VND", Direction = outflow ? LedgerEntryDirection.Debit : LedgerEntryDirection.Credit }];
        var tx = new FundTransaction { GroupId = group.Id, UserId = user.Id, Type = kind == "Contribution" ? FundTransactionType.Contribution : FundTransactionType.Expense, Amount = 100, WalletId = kind == "Contribution" ? wallet.Id : null, LedgerTransactionId = condition == "MissingLedger" ? null : posting.Id, ReferenceCode = posting.Reference };
        f.Db.AddRange(ticket, group, foreign, account, clearing, new GroupMember { GroupId = group.Id, UserId = user.Id, Role = GroupRole.Owner }, new SharedFund { Group = group, LedgerAccount = account });
        if (condition != "MissingLedger") f.Db.Add(posting);
        if (kind == "Contribution" && condition != "MissingBusiness" && condition != "MissingLedger") f.Db.Add(contribution);
        if (kind == "Expense" && condition != "MissingBusiness") f.Db.Add(expense);
        if (kind == "Refund") f.Db.AddRange(deletion, refund); else f.Db.Add(tx);
        if (condition == "WrongDirection") foreach (var entry in posting.Entries) entry.Direction = entry.Direction == LedgerEntryDirection.Debit ? LedgerEntryDirection.Credit : LedgerEntryDirection.Debit;
        if (condition is "Valid" or "HistoryMismatch") f.Db.Add(new FinancialTransaction { OwnerUserId = user.Id, LedgerTransactionId = posting.Id,
            RelatedBusinessId = posting.BusinessId, WalletId = kind == "Expense" ? null : wallet.Id, Amount = condition == "HistoryMismatch" ? 99 : 100 });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(a.Id, (await f.AddSessionAsync(a.Id)).Id, "Admin"); var s = AdminA3Tests.Service(f);
        var row = Assert.Single((await s.FinanceAsync(p, ticket.Id, group.Id, kind == "Refund" ? "Refunds" : "Transactions", new("Trace legacy money"), default)).Items);
        Assert.Equal(expected, row.Verification);
        if (condition == "Valid") Assert.Equal(posting.BusinessId, row.BusinessId);
        if (condition == "Valid") { Assert.Single(row.FinancialTransactionIds); Assert.Equal("MATCHED", row.FinancialHistoryVerification); }
        if (condition == "HistoryMismatch") Assert.Equal("REQUIRES_REVIEW", row.FinancialHistoryVerification);
        if (condition == "Valid" && kind != "Refund")
        {
            f.Db.FundTransactions.Remove(tx); await f.Db.SaveChangesAsync();
            var direct = Assert.Single((await s.FinanceAsync(p, ticket.Id, group.Id, kind == "Expense" ? "Expenses" : "Contributions", new("Trace original business record"), default)).Items);
            Assert.Equal("LEDGER_COMPLETED", direct.Verification); Assert.Equal(posting.BusinessId, direct.Id);
            Assert.Equal(2, (await s.PostingAsync(p, ticket.Id, group.Id, posting.Id, "Trace business without display history", default)).Entries.Count);
        }
        var before = await f.Db.LedgerTransactions.CountAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.PostingAsync(p, ticket.Id, foreign.Id, posting.Id, "Reject foreign fund", default));
        Assert.Equal(before, await f.Db.LedgerTransactions.CountAsync());
    }

    [Theory]
    [InlineData("NoRefund", "NOT_VERIFIED")]
    [InlineData("Pending", "NOT_VERIFIED")]
    [InlineData("MissingLedger", "NOT_VERIFIED")]
    public async Task Closed_legacy_status_and_zero_balance_do_not_prove_refunds(string condition, string expected)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var a = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var g = new Group { Name = "Closed", Status = GroupStatus.Closed }; var ticket = new SupportCase { AssignedAdminId = a.Id, LinkedUserId = user.Id };
        var deletion = new GroupDeletionRequest { GroupId = g.Id, OwnerUserId = user.Id, Status = GroupDeletionRequestStatus.Completed, CompletedAt = f.Clock.UtcNow, FundBalanceSnapshot = 100 };
        f.Db.AddRange(g, ticket, deletion, new GroupInvite { GroupId = g.Id, InvitedUserId = user.Id, InvitedByUserId = user.Id, Status = InviteStatus.Accepted });
        if (condition != "NoRefund") f.Db.Add(new GroupRefund { Request = deletion, UserId = user.Id, RefundAmount = 100, Status = condition == "Pending" ? GroupRefundStatus.Pending : GroupRefundStatus.Completed });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(a.Id, (await f.AddSessionAsync(a.Id)).Id, "Admin");
        var result = await AdminA3Tests.Service(f).DissolutionAsync(p, ticket.Id, g.Id, "Trace dissolution", default);
        Assert.Equal("Dissolved", result.Status); Assert.False(result.CountsAsActiveGroup); Assert.Equal(expected, result.RefundVerification); Assert.Equal(user.Id, result.LastOwnerUserId); Assert.Equal(deletion.Id, result.DissolutionRecordId);
        Assert.Null(result.FounderUserId); Assert.NotNull(result.DissolvedAt);
    }
}
