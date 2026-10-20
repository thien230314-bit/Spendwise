using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed partial class CoreAcceptanceHttpTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Election_HTTP_real_timeline_freezes_finance_and_fails_on_rejection_or_locked_voter(bool lockedVoter)
    {
        var clock = new AcceptanceClock();
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["V1:Activity:CoverageVerified"] = "true",
            ["V1:Activity:CoverageStartsAtUtc"] = clock.UtcNow.AddDays(-1).ToString("O"),
            ["RateLimiting:FinancialPermitLimit"] = "100"
        }, clock: clock);
        using var client = factory.CreateHttpsClient(); CoreFundSeed seed; User? other = null;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>(); seed = await CoreAcceptanceData.SeedAsync(db, false);
            if (lockedVoter)
            {
                other = SyntheticUser(); db.Users.Add(other);
                db.V1FundMemberships.Add(new() { FundId = seed.FundId, UserId = other.Id, Seat = 3, JoinedAt = clock.UtcNow }); await db.SaveChangesAsync();
            }
        }
        await ActorAsync(factory, client, seed.Member);
        using var tooEarly = await client.PostAsJsonAsync($"/api/v1/funds/{seed.FundId}/elections", new V1OpenElectionRequest(0));
        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        clock.UtcNow = clock.UtcNow.AddDays(15).AddMinutes(1);
        using (var diagnostics = factory.Services.CreateScope())
        {
            Assert.Same(clock, diagnostics.ServiceProvider.GetRequiredService<IClock>());
            var policy = diagnostics.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<V1ActivityPolicy>>().Value;
            var proof = await diagnostics.ServiceProvider.GetRequiredService<IV1InactivityEvidence>().GetAsync(seed.Owner.Id, default);
            Assert.True(proof.Eligible, $"coverage={policy.CoverageVerified}; start={policy.CoverageStartsAtUtc:O}; now={clock.UtcNow:O}; proof={proof}");
        }
        using var opened = await client.PostAsJsonAsync($"/api/v1/funds/{seed.FundId}/elections", new V1OpenElectionRequest(0));
        Assert.True(opened.StatusCode == HttpStatusCode.OK, await opened.Content.ReadAsStringAsync());
        var election = (await opened.Content.ReadFromJsonAsync<V1ElectionDto>())!;
        Assert.Equal("Registration", election.Phase);
        using var registered = await client.PostAsJsonAsync($"/api/v1/funds/elections/{election.Id}/candidates", new { SecurityPin = Pin });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        using var contribution = await client.PostAsJsonAsync($"/api/v1/funds/{seed.FundId}/contributions", new V1ContributeRequest(seed.MemberWallet, 1, "election-blocked", Pin));
        Assert.Equal(HttpStatusCode.Conflict, contribution.StatusCode);
        using var expense = await client.PostAsJsonAsync($"/api/v1/funds/{seed.FundId}/requests", new V1FundRequestInput("Expense", 1, seed.Member.Id, "Synthetic paused expense", "election-expense", Pin));
        Assert.Equal(HttpStatusCode.Conflict, expense.StatusCode);
        clock.UtcNow = clock.UtcNow.AddHours(24);
        using var round1 = await client.PostAsync($"/api/v1/funds/elections/{election.Id}/advance", null);
        Assert.Equal(HttpStatusCode.OK, round1.StatusCode);
        Assert.Equal("Nomination", (await round1.Content.ReadFromJsonAsync<V1ElectionDto>())!.Phase);
        if (lockedVoter)
        {
            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().Users.Where(u => u.Id == seed.Member.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Locked));
            using var denied = await client.PostAsJsonAsync($"/api/v1/funds/elections/{election.Id}/ballots", new V1BallotRequest(1, seed.Member.Id, null));
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            using var acceptDenied = await client.PostAsJsonAsync($"/api/v1/funds/elections/{election.Id}/accept", new { SecurityPin = Pin });
            Assert.Equal(HttpStatusCode.Unauthorized, acceptDenied.StatusCode);
            await ActorAsync(factory, client, other!);
            using var failed = await client.PostAsync($"/api/v1/funds/elections/{election.Id}/advance", null);
            Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
            var end = (await failed.Content.ReadFromJsonAsync<V1ElectionDto>())!;
            Assert.Equal("Failed", end.Phase); Assert.Equal("EligibilityChanged", end.FailureReason);
        }
        else
        {
            using var ballot = await client.PostAsJsonAsync($"/api/v1/funds/elections/{election.Id}/ballots", new V1BallotRequest(1, seed.Member.Id, null));
            Assert.Equal(HttpStatusCode.OK, ballot.StatusCode);
            clock.UtcNow = clock.UtcNow.AddHours(48);
            using var confirmation = await client.PostAsync($"/api/v1/funds/elections/{election.Id}/advance", null);
            Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
            Assert.Equal("Confirmation", (await confirmation.Content.ReadFromJsonAsync<V1ElectionDto>())!.Phase);
            using var yes = await client.PostAsJsonAsync($"/api/v1/funds/elections/{election.Id}/ballots", new V1BallotRequest(3, null, true));
            Assert.Equal(HttpStatusCode.OK, yes.StatusCode);
            clock.UtcNow = clock.UtcNow.AddHours(24);
            using var acceptance = await client.PostAsync($"/api/v1/funds/elections/{election.Id}/advance", null);
            Assert.Equal(HttpStatusCode.OK, acceptance.StatusCode);
            Assert.Equal("AwaitingAcceptance", (await acceptance.Content.ReadFromJsonAsync<V1ElectionDto>())!.Phase);
            using var rejected = await client.PostAsJsonAsync($"/api/v1/funds/elections/{election.Id}/reject", new { SecurityPin = Pin });
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.Equal("WinnerRejected", (await rejected.Content.ReadFromJsonAsync<V1ElectionDto>())!.FailureReason);
        }
        using var read = await client.GetAsync($"/api/v1/funds/elections/{election.Id}"); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var verifyScope = factory.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(V1ElectionPhase.Failed, (await verify.V1Elections.SingleAsync()).Phase);
        Assert.Equal(lockedVoter ? 2 : 1, await verify.V1ElectionVoters.CountAsync());
        Assert.Equal(seed.Owner.Id, await verify.V1Funds.Where(f => f.Id == seed.FundId).Select(f => f.OwnerUserId).SingleAsync());
        Assert.Equal(2, await verify.LedgerTransactions.CountAsync());
        Assert.Empty(await verify.V1FundRequests.ToListAsync());
    }

    private sealed class AcceptanceClock : IClock { public DateTime UtcNow { get; set; } = DateTime.UtcNow; }
}
