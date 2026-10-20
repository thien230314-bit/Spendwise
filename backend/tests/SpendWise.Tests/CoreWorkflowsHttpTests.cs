using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Features.Operations;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed partial class CoreAcceptanceHttpTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Dispute_HTTP_report_resolve_and_authorization_preserve_financial_state(bool rejected)
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient();
        CoreFundSeed seed; Guid id; DateTime created;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            seed = await CoreAcceptanceData.SeedAsync(db); id = await CoreAcceptanceData.PendingAsync(db, seed);
            created = await db.V1FundRequests.Where(r => r.Id == id).Select(r => r.CreatedAt).SingleAsync();
        }
        await ActorAsync(factory, client, seed.Member);
        using var report = await client.PostAsJsonAsync($"/api/v1/funds/requests/{id}/report", new { Reason = "Synthetic suspicious expense" });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal("Suspended", (await report.Content.ReadFromJsonAsync<V1FundRequestDto>())!.State);
        var resolution = new A5ResolveRequest("Synthetic investigation", "Synthetic reviewed evidence", rejected);
        using var denied = await client.PostAsJsonAsync($"/api/v1/admin/support/disputes/{id}/resolve", resolution);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await ActorAsync(factory, client, seed.Admin);
        using var resolved = await client.PostAsJsonAsync($"/api/v1/admin/support/disputes/{id}/resolve", resolution);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        using var retry = await client.PostAsJsonAsync($"/api/v1/admin/support/disputes/{id}/resolve", resolution);
        Assert.Equal(HttpStatusCode.Forbidden, retry.StatusCode);
        using var verifyScope = factory.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var request = await verify.V1FundRequests.SingleAsync(r => r.Id == id);
        Assert.Equal(rejected ? "Pending24h" : "Cancelled", request.State); Assert.Equal(created, request.CreatedAt);
        Assert.Equal(rejected ? 300_000 : 0, await CoreAcceptanceData.HeldAsync(verify, seed.FundId));
        Assert.Equal(rejected ? CaseStatus.Rejected : CaseStatus.Resolved, (await verify.SupportCases.SingleAsync()).Status);
        Assert.Equal(2, await verify.LedgerTransactions.CountAsync());
        Assert.Single(await verify.CaseHistories.Where(h => h.Action == "status_change").ToListAsync());
        Assert.Equal(1_500_000, await verify.Wallets.Where(w => w.Id == seed.OwnerWallet).Select(w => w.AvailableBalance).SingleAsync());
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Membership_HTTP_leave_rejoin_zero_old_entitlement_and_new_contribution(bool fixedFund)
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient(); CoreFundSeed seed;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>(); seed = await CoreAcceptanceData.SeedAsync(db, fixedFund);
            var pair = new[] { seed.Owner.Id, seed.Member.Id }.Order().ToArray();
            db.Friendships.Add(new() { LowerUserId = pair[0], HigherUserId = pair[1], AcceptedAt = DateTime.UtcNow,
                AcceptedRequest = new() { RequesterUserId = seed.Owner.Id, AddresseeUserId = seed.Member.Id, LowerUserId = pair[0], HigherUserId = pair[1], Status = SpendWise.Domain.Social.FriendRequestStatus.Accepted } });
            await db.SaveChangesAsync();
        }
        await ActorAsync(factory, client, seed.Member);
        using var leave = await client.PostAsJsonAsync($"/api/v1/funds/{seed.FundId}/leave", new V1LeaveFundRequest(Pin, "synthetic-leave", seed.MemberWallet));
        Assert.Equal(HttpStatusCode.OK, leave.StatusCode);
        Assert.Equal(500_000, (await leave.Content.ReadFromJsonAsync<V1RefundResultDto>())!.RefundAmountVnd);
        await ActorAsync(factory, client, seed.Owner);
        using var invite = await client.PostAsJsonAsync($"/api/v1/funds/{seed.FundId}/invitations", new { UserId = seed.Member.Id });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var invitation = (await invite.Content.ReadFromJsonAsync<V1InvitationDto>())!;
        await ActorAsync(factory, client, seed.Member);
        using var accept = await client.PostAsync($"/api/v1/funds/invitations/{invitation.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        using var old = await client.GetAsync($"/api/v1/funds/{seed.FundId}/refund-preview");
        Assert.True(old.StatusCode == HttpStatusCode.OK, await old.Content.ReadAsStringAsync());
        Assert.Equal(0, (await old.Content.ReadFromJsonAsync<V1RefundPreviewDto>())!.EligibleRefundAmountVnd);
        long? revision = null;
        if (fixedFund)
        {
            await ActorAsync(factory, client, seed.Owner);
            using var confirm = await client.PostAsJsonAsync($"/api/v1/funds/{seed.FundId}/obligations/confirm", new V1ConfirmObligationsRequest(1_000_000, Pin));
            Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
            revision = (await confirm.Content.ReadFromJsonAsync<V1FinanceDto>())!.Revision;
        }
        await ActorAsync(factory, client, seed.Member);
        using var contribution = await client.PostAsJsonAsync($"/api/v1/funds/{seed.FundId}/contributions", new V1ContributeRequest(seed.MemberWallet, 100_000, "synthetic-rejoin", Pin, revision));
        Assert.Equal(HttpStatusCode.OK, contribution.StatusCode);
        using var fresh = await client.GetAsync($"/api/v1/funds/{seed.FundId}/refund-preview");
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.Equal(100_000, (await fresh.Content.ReadFromJsonAsync<V1RefundPreviewDto>())!.EligibleRefundAmountVnd);
        using var finalLeave = await client.PostAsJsonAsync($"/api/v1/funds/{seed.FundId}/leave", new V1LeaveFundRequest(Pin, "synthetic-leave-new", seed.MemberWallet));
        Assert.Equal(HttpStatusCode.OK, finalLeave.StatusCode);
        Assert.Equal(100_000, (await finalLeave.Content.ReadFromJsonAsync<V1RefundResultDto>())!.RefundAmountVnd);
        using var replay = await client.PostAsJsonAsync($"/api/v1/funds/{seed.FundId}/leave", new V1LeaveFundRequest(Pin, "synthetic-leave-new", seed.MemberWallet));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var verifyScope = factory.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(2_000_000, await verify.Wallets.Where(w => w.Id == seed.MemberWallet).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Equal(2, await verify.V1FundMemberships.CountAsync(m => m.FundId == seed.FundId && m.UserId == seed.Member.Id && m.LeftAt != null));
        Assert.Equal(5, await verify.LedgerTransactions.CountAsync());
    }

    [Theory]
    [InlineData(SystemRole.Admin)] [InlineData(SystemRole.User)] [InlineData(SystemRole.SuperAdmin)]
    public async Task Admin_notifications_and_maintenance_HTTP_permissions_and_persisted_changes(SystemRole role)
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient();
        var actor = SyntheticUser(role); await ActorAsync(factory, client, actor, addUser: true);
        Guid caseId;
        using (var setupScope = factory.Services.CreateScope())
        {
            var setup = setupScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var subject = SyntheticUser(); var ticket = new SupportCase { LinkedUserId = subject.Id, AssignedAdminId = actor.Id, Status = CaseStatus.Investigating };
            setup.AddRange(subject, ticket); await setup.SaveChangesAsync(); caseId = ticket.Id;
        }
        var draft = new BroadcastDraftRequest("Operational", "User", "Synthetic notice", "Synthetic operational message", "synthetic-notice", Pin, "Synthetic operations");
        using var response = await client.PostAsJsonAsync("/api/v1/system/broadcasts", draft);
        var expected = role == SystemRole.Admin ? HttpStatusCode.OK : role == SystemRole.SuperAdmin ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
        Assert.Equal(expected, response.StatusCode);
        foreach (var route in new[] { "search", "delivery-history", "failures", "templates", "maintenance-overview" })
        {
            using var read = await client.PostAsJsonAsync($"/api/v1/admin/notifications/{route}", new { Reason = "Synthetic notification review", CaseId = caseId });
            Assert.Equal(expected, read.StatusCode);
        }
        var plan = new ScheduleMaintenanceRequest(DateTime.UtcNow.AddHours(3), DateTime.UtcNow.AddHours(4), "Groups", Pin, "Synthetic scheduled maintenance");
        using var scheduled = await client.PostAsJsonAsync("/api/v1/system/maintenance", plan);
        Assert.Equal(expected, scheduled.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        if (role == SystemRole.Admin)
        {
            var id = (await response.Content.ReadFromJsonAsync<V1BroadcastDto>())!.Id;
            Assert.Equal(draft.Message, (await db.V1Broadcasts.SingleAsync(b => b.Id == id)).Message);
            var planId = (await scheduled.Content.ReadFromJsonAsync<MaintenancePlanDto>())!.Id;
            Assert.Equal(plan.StartsAt, (await db.V1MaintenancePlans.SingleAsync(p => p.Id == planId)).StartsAt);
            using var cancel = await client.PostAsJsonAsync($"/api/v1/system/maintenance/{planId}/cancel", new MaintenanceActionRequest(Pin, "Synthetic cancellation"));
            Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
            Assert.NotNull(await db.V1MaintenancePlans.Where(p => p.Id == planId).Select(p => p.CancelledAt).SingleAsync());
            using var legacyTarget = await client.PostAsJsonAsync("/api/v1/system/broadcasts", draft with { TargetRole = "SuperAdmin", IdempotencyKey = "legacy-target" });
            Assert.Equal(HttpStatusCode.Conflict, legacyTarget.StatusCode);
        }
        else { Assert.Empty(await db.V1Broadcasts.ToListAsync()); Assert.Empty(await db.V1MaintenancePlans.ToListAsync()); }
        Assert.Empty(await db.LedgerTransactions.ToListAsync());
    }

    private static async Task ActorAsync(SpendWiseApiFactory factory, HttpClient client, User user, bool addUser = false)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        if (addUser) db.Users.Add(user);
        var session = new UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow }; db.UserSessions.Add(session); await db.SaveChangesAsync();
        Authorize(client, scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, session.Id).Token);
    }

    [Fact]
    public async Task Fixed_reconfirmation_still_rejects_expense_outflows()
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var seed = await CoreAcceptanceData.SeedAsync(db); var funds = CoreAcceptanceData.Funds(db);
        await funds.CreateFundRequestAsync(seed.Owner.Id, seed.FundId, new("Expense", 100_000, seed.Owner.Id, "Synthetic paid expense", "paid", Pin), default);
        var error = await Assert.ThrowsAsync<SpendWise.Application.Common.Exceptions.ConflictException>(() =>
            funds.ConfirmObligationsAsync(seed.Owner.Id, seed.FundId, new(1_000_000, Pin), default));
        Assert.Equal("FINANCE_BLOCKED", error.Code);
    }

    [Theory]
    [InlineData(SystemRole.Admin)] [InlineData(SystemRole.User)] [InlineData(SystemRole.SuperAdmin)]
    public async Task Support_HTTP_queue_claim_request_info_obeys_role_and_persists_assignment(SystemRole role)
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient(); var actor = SyntheticUser(role);
        await ActorAsync(factory, client, actor, addUser: true); Guid caseId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>(); var subject = SyntheticUser();
            var ticket = new SupportCase { LinkedUserId = subject.Id, Subject = "Synthetic acceptance support", Description = "Synthetic report" };
            db.AddRange(subject, ticket); await db.SaveChangesAsync(); caseId = ticket.Id;
        }
        var expected = role == SystemRole.Admin ? HttpStatusCode.OK : role == SystemRole.SuperAdmin ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
        foreach (var path in new[] { "/api/v1/admin/support/search", "/api/v1/admin/support/tickets/search", "/api/v1/admin/support/templates" })
        {
            using var response = await client.PostAsJsonAsync(path, new { Reason = "Synthetic case review" }); Assert.Equal(expected, response.StatusCode);
        }
        using var claim = await client.PostAsJsonAsync($"/api/v1/admin/support/{caseId}/assign", new { OperatorId = actor.Id, Reason = "Synthetic self assignment" });
        Assert.Equal(expected, claim.StatusCode);
        using var info = await client.PostAsJsonAsync($"/api/v1/admin/support/{caseId}/request-info", new { Reason = "Synthetic document request", Message = "Please provide synthetic receipt evidence" });
        Assert.Equal(expected, info.StatusCode);
        using var verifyScope = factory.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>(); var result = await verify.SupportCases.SingleAsync();
        Assert.Equal(role == SystemRole.Admin ? actor.Id : (Guid?)null, result.AssignedAdminId);
        Assert.Equal(role == SystemRole.Admin ? CaseStatus.WaitingForUser : CaseStatus.New, result.Status);
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
    }
}
