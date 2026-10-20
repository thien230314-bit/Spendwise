using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Finance;
using Xunit;

namespace SpendWise.Tests;

[Trait("Category", "SqlServer")]
public sealed class Task17AccountSqlHttpTests
{
    [Fact]
    public async Task Restricted_P2P_recipient_receives_credit_with_balanced_SQL02_ledger()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); CoreFundSeed s;
        await using (var db = f.Context()) { s = await CoreAcceptanceData.SeedAsync(db, false); await db.Users.Where(u => u.Id == s.Member.Id).ExecuteUpdateAsync(x => x.SetProperty(u => u.Status, UserStatus.Restricted)); }
        await f.ActorAsync(s.Owner);
        using var response = await f.Client.PostAsJsonAsync("/api/transfers/p2p", new { SourceWalletId = s.OwnerWallet, RecipientUserId = s.Member.Id, Amount = 100_000, Description = "Synthetic inbound", IdempotencyKey = "task17-inbound", SecurityPin = IdentityTestData.DefaultPin });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        await using var verify = f.Context();
        Assert.Equal(1_400_000, await verify.Wallets.Where(w => w.Id == s.OwnerWallet).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Equal(1_600_000, await verify.Wallets.Where(w => w.Id == s.MemberWallet).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Equal(UserStatus.Restricted, await verify.Users.Where(u => u.Id == s.Member.Id).Select(u => u.Status).SingleAsync());
        var entries = await verify.LedgerEntries.ToListAsync(); Assert.Equal(entries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount), entries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Restricted_member_receives_Fixed_or_Free_refund_once_via_HTTP_SQL02(bool fixedFund)
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); CoreFundSeed s;
        await using (var db = f.Context()) { s = await CoreAcceptanceData.SeedAsync(db, fixedFund); await db.Users.Where(u => u.Id == s.Member.Id).ExecuteUpdateAsync(x => x.SetProperty(u => u.Status, UserStatus.Restricted)); }
        await f.ActorAsync(s.Member);
        using var preview = await f.Client.GetAsync($"/api/v1/funds/{s.FundId}/refund-preview");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode); Assert.Equal(500_000, (await preview.Content.ReadFromJsonAsync<V1RefundPreviewDto>())!.EligibleRefundAmountVnd);
        for (var i = 0; i < 2; i++)
        {
            using var refund = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/leave", new V1LeaveFundRequest(IdentityTestData.DefaultPin, "task17-refund", s.MemberWallet));
            Assert.True(refund.StatusCode == HttpStatusCode.OK, await refund.Content.ReadAsStringAsync());
            Assert.Equal(500_000, (await refund.Content.ReadFromJsonAsync<V1RefundResultDto>())!.RefundAmountVnd);
        }
        await using var verify = f.Context();
        Assert.Equal(2_000_000, await verify.Wallets.Where(w => w.Id == s.MemberWallet).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Equal(UserStatus.Restricted, await verify.Users.Where(u => u.Id == s.Member.Id).Select(u => u.Status).SingleAsync());
        Assert.Single(await verify.LedgerTransactions.Where(t => t.BusinessType == "V1FundRefund").ToListAsync());
        var entries = await verify.LedgerEntries.ToListAsync(); Assert.Equal(entries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount), entries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount));
    }
}
