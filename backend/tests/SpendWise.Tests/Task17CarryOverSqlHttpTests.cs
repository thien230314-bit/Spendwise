using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Features.Wallets;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

[Trait("Category", "SqlServer")]
public sealed class Task17CarryOverSqlHttpTests
{
    private const string Pin = IdentityTestData.DefaultPin;
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Fixed_carryover_HTTP_SQL02_settles_nonconsent_and_preserves_period_and_membership_entitlement(bool declineMiddleSeat)
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); CoreFundSeed s; SpendWise.Domain.Identity.User third; Guid thirdWallet;
        await using (var db = f.Context())
        {
            s = await CoreAcceptanceData.SeedAsync(db); third = CoreAcceptanceHttpTests.SyntheticUser();
            var wallet = WalletFactory.Create(third.Id, "Synthetic third", WalletType.Main, true); wallet.AvailableBalance = wallet.LedgerAccount.Balance = 2_000_000;
            db.AddRange(third, wallet, new V1FundMembership { FundId = s.FundId, UserId = third.Id, Seat = 3, JoinedAt = f.Clock.UtcNow }); await db.SaveChangesAsync(); thirdWallet = wallet.Id;
            var service = CoreAcceptanceData.Funds(db); var config = await service.ConfirmObligationsAsync(s.Owner.Id, s.FundId, new(1_500_000, Pin), default);
            await service.ContributeAsync(third.Id, s.FundId, new(wallet.Id, 500_000, "task17-third", Pin, config.Revision), default);
        }
        if (declineMiddleSeat)
        {
            var previous = s.Member; var wallet = s.MemberWallet;
            s = s with { Member = third, MemberWallet = thirdWallet }; third = previous; thirdWallet = wallet;
        }
        f.Clock.UtcNow = DateTime.UtcNow.AddSeconds(1);
        await f.ActorAsync(s.Owner);
        using var expense = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/requests", new V1FundRequestInput("Expense", 100_000, s.Owner.Id, "Synthetic old period expense", "task17-spent", Pin));
        Assert.Equal(HttpStatusCode.OK, expense.StatusCode); Assert.Equal("Completed", (await expense.Content.ReadFromJsonAsync<V1FundRequestDto>())!.State);
        using var close = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/activity/close", new { SecurityPin = Pin }); Assert.True(close.IsSuccessStatusCode, await close.Content.ReadAsStringAsync());
        using var proposalResponse = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/activity-proposals", new V1ProposeActivityRequest(1_000_000, "task17-next", Pin));
        Assert.Equal(HttpStatusCode.OK, proposalResponse.StatusCode); var proposal = (await proposalResponse.Content.ReadFromJsonAsync<V1ActivityProposalDto>())!;
        using var ownerVote = await f.Client.PostAsJsonAsync($"/api/v1/funds/activity-proposals/{proposal.Id}/vote", new { Support = true }); Assert.Equal(HttpStatusCode.OK, ownerVote.StatusCode);
        await f.ActorAsync(s.Member); using var memberVote = await f.Client.PostAsJsonAsync($"/api/v1/funds/activity-proposals/{proposal.Id}/vote", new { Support = true }); Assert.Equal(HttpStatusCode.OK, memberVote.StatusCode);
        await f.ActorAsync(third); using var noVote = await f.Client.PostAsJsonAsync($"/api/v1/funds/activity-proposals/{proposal.Id}/vote", new { Support = false }); Assert.Equal(HttpStatusCode.OK, noVote.StatusCode);
        using var previewResponse = await f.Client.GetAsync($"/api/v1/funds/{s.FundId}/refund-preview"); Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = (await previewResponse.Content.ReadFromJsonAsync<V1RefundPreviewDto>())!.EligibleRefundAmountVnd; Assert.Equal(declineMiddleSeat ? 466_667 : 466_666, preview);
        f.Clock.UtcNow = f.Clock.UtcNow.AddHours(24);
        using (var scope = f.Factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<V1FundService>().ProgressActivitiesAsync(default);
        using var unauthorized = await f.Client.PostAsJsonAsync($"/api/v1/funds/activity-proposals/{proposal.Id}/settle", new { SecurityPin = Pin }); Assert.Equal(HttpStatusCode.Forbidden, unauthorized.StatusCode);
        await f.ActorAsync(s.Owner);
        await using (var faulted = f.Context(new FailSettlementRefund()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CoreAcceptanceData.Funds(faulted, f.Clock).SettleActivityAsync(s.Owner.Id, proposal.Id, Pin, default));
        }
        await using (var rollback = f.Context())
        {
            Assert.Equal(4, await rollback.LedgerTransactions.CountAsync());
            Assert.Equal(3, await rollback.V1FundMemberships.CountAsync(m => m.FundId == s.FundId && m.LeftAt == null));
            Assert.Null((await rollback.V1ActivityProposals.SingleAsync()).SettledAt);
            Assert.All(await rollback.V1ActivityVoters.ToListAsync(), v => Assert.Null(v.CarryOverVnd));
        }
        using var duplicateProposal = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/activity-proposals", new V1ProposeActivityRequest(1_000_000, "task17-duplicate-period", Pin)); Assert.Equal(HttpStatusCode.Conflict, duplicateProposal.StatusCode);
        await using (var checkProposal = f.Context()) Assert.Single(await checkProposal.V1ActivityProposals.Where(p => p.FundId == s.FundId).ToListAsync());
        await Task.WhenAll(Settle(), Settle()); await Settle();
        using var finance = await f.Client.GetAsync($"/api/v1/funds/{s.FundId}/finance"); Assert.Equal(HttpStatusCode.OK, finance.StatusCode);
        var state = (await finance.Content.ReadFromJsonAsync<V1FinanceDto>())!;
        Assert.Equal(1_400_000 - preview, state.BalanceVnd); Assert.Equal(2, state.Obligations.Count);
        var carry = state.Obligations.ToDictionary(o => o.UserId, o => o.PaidVnd);
        Assert.Equal(466_667, carry[s.Owner.Id]); Assert.Equal(declineMiddleSeat ? 466_666 : 466_667, carry[s.Member.Id]);
        await using (var verify = f.Context())
        {
            Assert.Equal(1_500_000 + preview, await verify.Wallets.Where(w => w.Id == thirdWallet).Select(w => w.AvailableBalance).SingleAsync());
            Assert.Single(await verify.LedgerTransactions.Where(t => t.BusinessType == "V1FundRefund").ToListAsync());
            Assert.Equal(5, await verify.LedgerTransactions.CountAsync());
            Assert.NotNull(await verify.V1FundMemberships.Where(m => m.UserId == third.Id && m.FundId == s.FundId).Select(m => m.LeftAt).SingleAsync());
            // Accepted invitation fixture preserves the real HTTP membership-episode creation path.
            verify.V1FundInvitations.Add(new() { FundId = s.FundId, InvitedUserId = third.Id, InvitedByUserId = s.Owner.Id, Status = V1InvitationStatus.Pending, ExpiresAt = f.Clock.UtcNow.AddDays(1) }); await verify.SaveChangesAsync();
        }
        Guid invitation;
        await using (var db = f.Context()) invitation = await db.V1FundInvitations.Where(i => i.InvitedUserId == third.Id).Select(i => i.Id).SingleAsync();
        await f.ActorAsync(third); using var rejoin = await f.Client.PostAsync($"/api/v1/funds/invitations/{invitation}/accept", null); Assert.Equal(HttpStatusCode.OK, rejoin.StatusCode);
        using var old = await f.Client.GetAsync($"/api/v1/funds/{s.FundId}/refund-preview"); Assert.Equal(HttpStatusCode.OK, old.StatusCode); Assert.Equal(0, (await old.Content.ReadFromJsonAsync<V1RefundPreviewDto>())!.EligibleRefundAmountVnd);
        var newcomer = CoreAcceptanceHttpTests.SyntheticUser(); Guid newInvitation;
        await using (var db = f.Context())
        {
            var invite = new V1FundInvitation { FundId = s.FundId, InvitedUserId = newcomer.Id, InvitedByUserId = s.Owner.Id, Status = V1InvitationStatus.Pending, ExpiresAt = f.Clock.UtcNow.AddDays(1) };
            db.AddRange(newcomer, invite); await db.SaveChangesAsync(); newInvitation = invite.Id;
        }
        await f.ActorAsync(newcomer); using var newJoin = await f.Client.PostAsync($"/api/v1/funds/invitations/{newInvitation}/accept", null); Assert.Equal(HttpStatusCode.OK, newJoin.StatusCode);
        using var noOldShare = await f.Client.GetAsync($"/api/v1/funds/{s.FundId}/refund-preview"); Assert.Equal(HttpStatusCode.OK, noOldShare.StatusCode); Assert.Equal(0, (await noOldShare.Content.ReadFromJsonAsync<V1RefundPreviewDto>())!.EligibleRefundAmountVnd);
        await f.ActorAsync(s.Owner); using var confirm = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/obligations/confirm", new V1ConfirmObligationsRequest(2_000_000, Pin)); Assert.True(confirm.IsSuccessStatusCode, await confirm.Content.ReadAsStringAsync());
        var revision = (await confirm.Content.ReadFromJsonAsync<V1FinanceDto>())!.Revision;
        await f.ActorAsync(third); using var contribute = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/contributions", new V1ContributeRequest(thirdWallet, 100_000, "task17-fresh", Pin, revision)); Assert.True(contribute.IsSuccessStatusCode, await contribute.Content.ReadAsStringAsync());
        using var fresh = await f.Client.GetAsync($"/api/v1/funds/{s.FundId}/refund-preview"); Assert.Equal(HttpStatusCode.OK, fresh.StatusCode); Assert.Equal(100_000, (await fresh.Content.ReadFromJsonAsync<V1RefundPreviewDto>())!.EligibleRefundAmountVnd);
        using var leave = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/leave", new V1LeaveFundRequest(Pin, "task17-fresh-leave", thirdWallet)); Assert.Equal(HttpStatusCode.OK, leave.StatusCode); Assert.Equal(100_000, (await leave.Content.ReadFromJsonAsync<V1RefundResultDto>())!.RefundAmountVnd);
        await f.ActorAsync(s.Owner); using var closeNext = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/activity/close", new { SecurityPin = Pin }); Assert.True(closeNext.IsSuccessStatusCode);
        using var nextPeriod = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/activity-proposals", new V1ProposeActivityRequest(1_000_000, "task17-third-period", Pin)); Assert.Equal(HttpStatusCode.OK, nextPeriod.StatusCode);
        var nextId = (await nextPeriod.Content.ReadFromJsonAsync<V1ActivityProposalDto>())!.Id;
        using var nextOwnerVote = await f.Client.PostAsJsonAsync($"/api/v1/funds/activity-proposals/{nextId}/vote", new { Support = true }); Assert.Equal(HttpStatusCode.OK, nextOwnerVote.StatusCode);
        await f.ActorAsync(s.Member); using var nextMemberVote = await f.Client.PostAsJsonAsync($"/api/v1/funds/activity-proposals/{nextId}/vote", new { Support = true }); Assert.Equal(HttpStatusCode.OK, nextMemberVote.StatusCode);
        f.Clock.UtcNow = f.Clock.UtcNow.AddHours(24);
        using (var scope = f.Factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<V1FundService>().ProgressActivitiesAsync(default);
        await f.ActorAsync(s.Owner); using var nextSettlement = await f.Client.PostAsJsonAsync($"/api/v1/funds/activity-proposals/{nextId}/settle", new { SecurityPin = Pin }); Assert.True(nextSettlement.IsSuccessStatusCode, await nextSettlement.Content.ReadAsStringAsync());
        using var finalFinance = await f.Client.GetAsync($"/api/v1/funds/{s.FundId}/finance"); Assert.Equal(HttpStatusCode.OK, finalFinance.StatusCode);
        var nextState = (await finalFinance.Content.ReadFromJsonAsync<V1FinanceDto>())!; Assert.Equal(1_400_000 - preview, nextState.BalanceVnd); Assert.All(nextState.Obligations, o => Assert.Equal(carry[o.UserId], o.PaidVnd));
        await using var final = f.Context(); var entries = await final.LedgerEntries.ToListAsync();
        Assert.Equal(entries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount), entries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount));
        Assert.Equal(7, await final.LedgerTransactions.CountAsync());

        async Task Settle()
        { using var response = await f.Client.PostAsJsonAsync($"/api/v1/funds/activity-proposals/{proposal.Id}/settle", new { SecurityPin = Pin }); Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync()); Assert.Equal("Settled", (await response.Content.ReadFromJsonAsync<V1ActivityProposalDto>())!.State); }
    }

    private sealed class FailSettlementRefund : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData data,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<FinancialTransaction>().Any(e => e.State == EntityState.Added && e.Entity.Type == FinancialTransactionType.Refund))
                throw new InvalidOperationException("Synthetic settlement failure after ledger save");
            return ValueTask.FromResult(result);
        }
    }
}
