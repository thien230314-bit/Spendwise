using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA3PostingBoundaryTests
{
    [Fact]
    public async Task Ambiguous_business_postings_are_listed_not_arbitrarily_verified_and_foreign_fund_is_denied()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var a = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var group = new Group { Name = "Fund" }; var other = new Group { Name = "Other authorized fund" }; var ticket = new SupportCase { AssignedAdminId = a.Id, LinkedUserId = user.Id };
        var account = new LedgerAccount { Code = "FixtureFund", Type = LedgerAccountType.SharedFund }; var otherAccount = new LedgerAccount { Code = "FixtureOther", Type = LedgerAccountType.SharedFund };
        var clearing = new LedgerAccount { Code = "SYSTEM:GroupExpenseClearing:VND", Type = LedgerAccountType.ExpenseClearing, IsSystem = true }; var expense = new GroupExpense { GroupId = group.Id, Amount = 100, PaidByType = GroupExpensePaidByType.Fund };
        LedgerTransaction Posting(string reference) => new() { Reference = reference, BusinessType = "GroupFundExpense", BusinessId = expense.Id, Status = LedgerTransactionStatus.Completed,
            Entries = [new() { LedgerAccountId = account.Id, Direction = LedgerEntryDirection.Credit, Amount = 100 }, new() { LedgerAccountId = clearing.Id, Direction = LedgerEntryDirection.Debit, Amount = 100 }] };
        var one = Posting("One"); var two = Posting("Two");
        f.Db.AddRange(group, other, ticket, account, otherAccount, clearing, expense, one, two,
            new GroupMember { GroupId = group.Id, UserId = user.Id }, new GroupMember { GroupId = other.Id, UserId = user.Id },
            new SharedFund { Group = group, LedgerAccount = account }, new SharedFund { Group = other, LedgerAccount = otherAccount });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(a.Id, (await f.AddSessionAsync(a.Id)).Id, "Admin"); var s = AdminA3Tests.Service(f);
        var row = Assert.Single((await s.FinanceAsync(p, ticket.Id, group.Id, "Expenses", new("Trace ambiguous business"), default)).Items);
        Assert.Equal("UNVERIFIED", row.Verification); Assert.Equal(2, row.PostingCandidateCount); Assert.Null(row.LedgerTransactionId);
        var candidates = await s.ExpensePostingsAsync(p, ticket.Id, group.Id, expense.Id, new("Inspect actual candidates", PageSize: 1), default); Assert.Equal(2, candidates.Total); Assert.Single(candidates.Items);
        Assert.Equal(2, (await s.PostingAsync(p, ticket.Id, group.Id, one.Id, "Trace candidate entries", default)).Entries.Count);
        await Assert.ThrowsAsync<ForbiddenException>(() => s.PostingAsync(p, ticket.Id, other.Id, one.Id, "Reject cross-fund posting", default));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.ExpensePostingsAsync(p, ticket.Id, other.Id, expense.Id, new("Reject cross-fund expense"), default));
        // Even an inconsistent local history pointer cannot grant access to a posting with no entry in that fund account.
        f.Db.Add(new FundTransaction { GroupId = other.Id, Type = FundTransactionType.Expense, LedgerTransactionId = one.Id, Amount = 100 }); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.PostingAsync(p, ticket.Id, other.Id, one.Id, "Reject corrupted foreign pointer", default));
    }
}
