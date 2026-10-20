using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

[Trait("Category", "SqlServer")]
public sealed class Task17ElectionSqlHttpTests
{
    [Theory]
    [InlineData("tie")] [InlineData("no-votes")] [InlineData("expired")] [InlineData("locked-winner")]
    public async Task Election_edge_HTTP_SQL02_preserves_snapshot_owner_and_money(string scenario)
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); CoreFundSeed s; User? other = null;
        await using (var db = f.Context())
        {
            s = await CoreAcceptanceData.SeedAsync(db, false);
            if (scenario is "tie" or "locked-winner")
            {
                other = CoreAcceptanceHttpTests.SyntheticUser(); db.AddRange(other, new V1FundMembership { FundId = s.FundId, UserId = other.Id, Seat = 3, JoinedAt = f.Clock.UtcNow }); await db.SaveChangesAsync();
            }
        }
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(15).AddMinutes(1); await f.ActorAsync(s.Member);
        using var open = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/elections", new V1OpenElectionRequest(0)); Assert.True(open.IsSuccessStatusCode, await open.Content.ReadAsStringAsync());
        var id = (await open.Content.ReadFromJsonAsync<V1ElectionDto>())!.Id;
        using var finance = await f.Client.PostAsJsonAsync($"/api/v1/funds/{s.FundId}/contributions", new V1ContributeRequest(s.MemberWallet, 1, "task17-paused", IdentityTestData.DefaultPin)); Assert.Equal(HttpStatusCode.Conflict, finance.StatusCode);
        await Post("candidates", new { SecurityPin = IdentityTestData.DefaultPin });
        if (scenario == "tie") { await f.ActorAsync(other!); await Post("candidates", new { SecurityPin = IdentityTestData.DefaultPin }); await f.ActorAsync(s.Member); }
        f.Clock.UtcNow = f.Clock.UtcNow.AddHours(24); Assert.Equal("Nomination", (await Advance()).Phase);
        if (scenario != "no-votes")
        {
            await Post("ballots", new V1BallotRequest(1, s.Member.Id, null));
            if (other is not null) { await f.ActorAsync(other); await Post("ballots", new V1BallotRequest(1, scenario == "tie" ? other.Id : s.Member.Id, null)); await f.ActorAsync(s.Member); }
        }
        f.Clock.UtcNow = f.Clock.UtcNow.AddHours(48); var round = await Advance();
        if (scenario == "tie")
        {
            Assert.Equal("Runoff", round.Phase); await Post("ballots", new V1BallotRequest(2, s.Member.Id, null));
            await f.ActorAsync(other!); await Post("ballots", new V1BallotRequest(2, other!.Id, null));
            f.Clock.UtcNow = f.Clock.UtcNow.AddHours(24); Assert.Equal("RunoffTie", (await Advance()).FailureReason);
        }
        else if (scenario == "no-votes") Assert.Equal("NoVotes", round.FailureReason);
        else
        {
            Assert.Equal("Confirmation", round.Phase); await Post("ballots", new V1BallotRequest(3, null, true));
            if (other is not null) { await f.ActorAsync(other); await Post("ballots", new V1BallotRequest(3, null, true)); await f.ActorAsync(s.Member); }
            f.Clock.UtcNow = f.Clock.UtcNow.AddHours(24); Assert.Equal("AwaitingAcceptance", (await Advance()).Phase);
            if (scenario == "locked-winner")
            {
                await using (var db = f.Context()) await db.Users.Where(u => u.Id == s.Member.Id).ExecuteUpdateAsync(x => x.SetProperty(u => u.Status, UserStatus.Locked));
                using var denied = await f.Client.PostAsJsonAsync($"/api/v1/funds/elections/{id}/accept", new { SecurityPin = IdentityTestData.DefaultPin }); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
                await f.ActorAsync(other!); Assert.Equal("EligibilityChanged", (await Advance()).FailureReason);
            }
            else { f.Clock.UtcNow = f.Clock.UtcNow.AddHours(24); Assert.Equal("AcceptanceExpired", (await Advance()).FailureReason); }
        }
        await using var verify = f.Context(); Assert.Equal(V1ElectionPhase.Failed, (await verify.V1Elections.SingleAsync()).Phase);
        Assert.Equal(other is null ? 1 : 2, await verify.V1ElectionVoters.CountAsync());
        Assert.Equal(s.Owner.Id, (await verify.V1Funds.SingleAsync()).OwnerUserId);
        Assert.Equal(2, await verify.LedgerTransactions.CountAsync()); Assert.Empty(await verify.V1FundRequests.ToListAsync());

        async Task Post(string action, object request)
        { using var response = await f.Client.PostAsJsonAsync($"/api/v1/funds/elections/{id}/{action}", request); Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync()); }
        async Task<V1ElectionDto> Advance()
        { using var response = await f.Client.PostAsync($"/api/v1/funds/elections/{id}/advance", null); Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync()); return (await response.Content.ReadFromJsonAsync<V1ElectionDto>())!; }
    }
}
