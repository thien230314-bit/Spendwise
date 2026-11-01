using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Social;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class FixedFundV2HttpTests
{
    private static async Task<(HttpClient Client, User User)> SetupUserAsync(SpendWiseApiFactory factory, string name)
    {
        var client = factory.CreateHttpsClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var key = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = name,
            Email = $"{key}@test.local",
            NormalizedEmail = $"{key.ToUpperInvariant()}@TEST.LOCAL",
            SpendId = key[..12],
            NormalizedSpendId = key[..12].ToUpperInvariant(),
            PasswordHash = "synthetic_hash",
            SecurityPinHash = new SecurityPinService().Hash("123456"),
            SystemRole = SystemRole.User
        };
        var session = new UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow };
        db.AddRange(user, session);
        await db.SaveChangesAsync();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.CreateAccessToken(user, session.Id).Token);
        return (client, user);
    }

    [Fact]
    public async Task Http_FixedFundCreation_EnforcesTrustCommitment()
    {
        using var factory = new SpendWiseApiFactory();
        var (client, owner) = await SetupUserAsync(factory, "OwnerHttp");

        // 1. Missing trust commitment payload -> 409 Conflict
        var badPayload1 = new { Name = "FixedHttp1", Kind = "Fixed", TargetVnd = 10_000_000, SecurityPin = "123456" };
        var res1 = await client.PostAsJsonAsync("/api/v1/funds", badPayload1);
        Assert.Equal(HttpStatusCode.Conflict, res1.StatusCode);
        var err1 = await res1.Content.ReadAsStringAsync();
        Assert.Contains("trust_commitment_required", err1);

        // 2. Trust commitment = false -> 409 Conflict
        var badPayload2 = new { Name = "FixedHttp2", Kind = "Fixed", TargetVnd = 10_000_000, SecurityPin = "123456", TrustCommitmentAccepted = false, CommitmentVersion = "v2.0" };
        var res2 = await client.PostAsJsonAsync("/api/v1/funds", badPayload2);
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);

        // 3. Trust commitment = true but version = "v1.0" -> 409 Conflict
        var badPayload3 = new { Name = "FixedHttp3", Kind = "Fixed", TargetVnd = 10_000_000, SecurityPin = "123456", TrustCommitmentAccepted = true, CommitmentVersion = "v1.0" };
        var res3 = await client.PostAsJsonAsync("/api/v1/funds", badPayload3);
        Assert.Equal(HttpStatusCode.Conflict, res3.StatusCode);

        // 4. Valid trust commitment (true, v2.0) -> 200 OK
        var okPayload = new { Name = "FixedHttpOk", Kind = "Fixed", TargetVnd = 10_000_000, SecurityPin = "123456", TrustCommitmentAccepted = true, CommitmentVersion = "v2.0", ContributionDurationHours = 48 };
        var resOk = await client.PostAsJsonAsync("/api/v1/funds", okPayload);
        Assert.Equal(HttpStatusCode.OK, resOk.StatusCode);
    }

    [Fact]
    public async Task Http_AcceptInvitation_EnforcesFixedTrust_AndPreservesFreeFundCompatibility()
    {
        using var factory = new SpendWiseApiFactory();
        var (clientOwner, owner) = await SetupUserAsync(factory, "OwnerAccept");
        var (clientMember, member) = await SetupUserAsync(factory, "MemberAccept");

        // Make friends
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var lower = owner.Id.CompareTo(member.Id) < 0 ? owner.Id : member.Id;
            var higher = lower == owner.Id ? member.Id : owner.Id;
            var req = new FriendRequest
            {
                RequesterUserId = lower,
                AddresseeUserId = higher,
                LowerUserId = lower,
                HigherUserId = higher,
                Status = FriendRequestStatus.Accepted
            };
            db.FriendRequests.Add(req);
            db.Friendships.Add(new Friendship
            {
                LowerUserId = lower,
                HigherUserId = higher,
                AcceptedRequest = req,
                AcceptedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // --- PART A: Fixed Fund Invitation Accept ---
        var createFixedRes = await clientOwner.PostAsJsonAsync("/api/v1/funds", new
        {
            Name = "FixedHttpFund",
            Kind = "Fixed",
            TargetVnd = 6_000_000,
            SecurityPin = "123456",
            TrustCommitmentAccepted = true,
            CommitmentVersion = "v2.0"
        });
        Assert.Equal(HttpStatusCode.OK, createFixedRes.StatusCode);
        var fixedFund = await createFixedRes.Content.ReadFromJsonAsync<V1FundDto>();
        Assert.NotNull(fixedFund);

        var inviteFixedRes = await clientOwner.PostAsJsonAsync($"/api/v1/funds/{fixedFund.Id}/invitations", new { UserId = member.Id });
        Assert.Equal(HttpStatusCode.OK, inviteFixedRes.StatusCode);
        var fixedInvite = await inviteFixedRes.Content.ReadFromJsonAsync<V1InvitationDto>();
        Assert.NotNull(fixedInvite);

        // Member attempts accept Fixed without trust commitment -> 409
        var resAcceptNoTrust = await clientMember.PostAsJsonAsync($"/api/v1/funds/invitations/{fixedInvite.Id}/accept", new { });
        Assert.Equal(HttpStatusCode.Conflict, resAcceptNoTrust.StatusCode);
        Assert.Contains("trust_commitment_required", await resAcceptNoTrust.Content.ReadAsStringAsync());

        // Member accepts Fixed with valid commitment -> 200 OK
        var resAcceptFixedOk = await clientMember.PostAsJsonAsync($"/api/v1/funds/invitations/{fixedInvite.Id}/accept", new
        {
            TrustCommitmentAccepted = true,
            CommitmentVersion = "v2.0"
        });
        Assert.Equal(HttpStatusCode.OK, resAcceptFixedOk.StatusCode);

        // --- PART B: Free Fund Invitation Accept (Zero Contamination) ---
        var createFreeRes = await clientOwner.PostAsJsonAsync("/api/v1/funds", new
        {
            Name = "FreeHttpFund",
            Kind = "Free",
            SecurityPin = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, createFreeRes.StatusCode);
        var freeFund = await createFreeRes.Content.ReadFromJsonAsync<V1FundDto>();
        Assert.NotNull(freeFund);

        var (clientMemberFree, memberFree) = await SetupUserAsync(factory, "MemberFreeHttp");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var lower = owner.Id.CompareTo(memberFree.Id) < 0 ? owner.Id : memberFree.Id;
            var higher = lower == owner.Id ? memberFree.Id : owner.Id;
            var req = new FriendRequest
            {
                RequesterUserId = lower,
                AddresseeUserId = higher,
                LowerUserId = lower,
                HigherUserId = higher,
                Status = FriendRequestStatus.Accepted
            };
            db.FriendRequests.Add(req);
            db.Friendships.Add(new Friendship
            {
                LowerUserId = lower,
                HigherUserId = higher,
                AcceptedRequest = req,
                AcceptedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var inviteFreeRes = await clientOwner.PostAsJsonAsync($"/api/v1/funds/{freeFund.Id}/invitations", new { UserId = memberFree.Id });
        Assert.Equal(HttpStatusCode.OK, inviteFreeRes.StatusCode);
        var freeInvite = await inviteFreeRes.Content.ReadFromJsonAsync<V1InvitationDto>();
        Assert.NotNull(freeInvite);

        // Member accepts Free Fund with empty body -> 200 OK
        var resAcceptFreeOk = await clientMemberFree.PostAsJsonAsync($"/api/v1/funds/invitations/{freeInvite.Id}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, resAcceptFreeOk.StatusCode);
    }
}
