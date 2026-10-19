using Microsoft.EntityFrameworkCore;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA3FinanceTests
{
    [Theory]
    [InlineData("Valid", "LEDGER_COMPLETED")]
    [InlineData("Unbalanced", "LEDGER_REQUIRES_REVIEW")]
    [InlineData("WrongWallet", "LEDGER_REQUIRES_REVIEW")]
    [InlineData("WrongBusiness", "LEDGER_REQUIRES_REVIEW")]
    [InlineData("Pending", "LEDGER_REQUIRES_REVIEW")]
    public async Task Contribution_verification_requires_real_balanced_bound_completed_posting(string condition, string expected)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var wallet = await f.AddWalletAsync(user.Id, "Source", true); var alternate = await f.AddWalletAsync(user.Id, "Other", false);
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id };
        var fund = new V1Fund("Free", "SPW000001", V1FundKind.Free, user.Id, null);
        var membership = new V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 1 };
        var account = new LedgerAccount { Code = "SyntheticFund", Type = LedgerAccountType.SharedFund, Balance = 100 };
        var posting = new LedgerTransaction { Reference = "SyntheticContribution", BusinessType = "V1FundContribution", Status = condition == "Pending" ? LedgerTransactionStatus.Pending : LedgerTransactionStatus.Completed };
        var contribution = new V1FundContribution { FundId = fund.Id, MembershipId = membership.Id, UserId = user.Id, WalletId = wallet.Id, LedgerTransactionId = posting.Id, AmountVnd = 100 };
        posting.BusinessId = condition == "WrongBusiness" ? Guid.NewGuid() : contribution.Id;
        posting.Entries = [new() { LedgerAccountId = account.Id, Amount = 100, Direction = LedgerEntryDirection.Debit },
            new() { LedgerAccountId = condition == "WrongWallet" ? alternate.LedgerAccount.Id : wallet.LedgerAccount.Id, Amount = condition == "Unbalanced" ? 90 : 100, Direction = LedgerEntryDirection.Credit }];
        f.Db.AddRange(ticket, fund, membership, account, posting, contribution, new V1FundTreasury { FundId = fund.Id, LedgerAccountId = account.Id });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var s = AdminA3Tests.Service(f);
        var row = Assert.Single((await s.FinanceAsync(p, ticket.Id, fund.Id, "Contributions", new("Trace contribution"), default)).Items);
        Assert.Equal(expected, row.Verification); Assert.Equal(wallet.Id, row.WalletId); Assert.Equal(posting.Id, row.LedgerTransactionId);
        var traced = await s.PostingAsync(p, ticket.Id, fund.Id, posting.Id, "Trace stored posting", default); Assert.Equal(2, traced.Entries.Count); Assert.False(traced.Truncated);
        await Assert.ThrowsAsync<SpendWise.Application.Common.Exceptions.ForbiddenException>(() => s.PostingAsync(p, ticket.Id, fund.Id, Guid.NewGuid(), "Reject foreign posting", default));
        var treasury = await s.TreasuryAsync(p, ticket.Id, fund.Id, "Verify treasury", default); Assert.False(treasury.ZeroLedgerEvidence); Assert.Equal(100, treasury.StoredBalance);
        Assert.Equal(condition == "Pending" ? 0 : 100, treasury.CompletedLedgerNet);
        Assert.Equal(1, await f.Db.LedgerTransactions.CountAsync()); Assert.Equal(2, await f.Db.LedgerEntries.CountAsync());
    }
}
