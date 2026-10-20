using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using Xunit;

namespace SpendWise.Tests;

[Trait("Category", "SqlServer")]
public sealed class Task17SupportBroadcastSqlHttpTests
{
    private const string Pin = IdentityTestData.DefaultPin;
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Support_HTTP_SQL02_intake_assignment_information_reply_and_terminal_decision(bool reject)
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); var subject = CoreAcceptanceHttpTests.SyntheticUser();
        var outsider = CoreAcceptanceHttpTests.SyntheticUser(); var admin = CoreAcceptanceHttpTests.SyntheticUser(SystemRole.Admin); var other = CoreAcceptanceHttpTests.SyntheticUser(SystemRole.Admin);
        await using (var db = f.Context()) { db.AddRange(subject, outsider, admin, other); await db.SaveChangesAsync(); }
        await f.ActorAsync(subject);
        using var intake = await f.Client.PostAsJsonAsync("/api/cases/support", new { Category = "General", Priority = "Normal", Subject = "Synthetic support", Description = "Synthetic receipt investigation" });
        Assert.True(intake.IsSuccessStatusCode, await intake.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await intake.Content.ReadAsStringAsync()); var id = json.RootElement.GetProperty("id").GetGuid();
        await using (var db = f.Context()) Assert.Equal(CaseStatus.New, (await db.SupportCases.SingleAsync()).Status);
        await f.ActorAsync(outsider); using var privateRead = await f.Client.GetAsync($"/api/cases/support/{id}"); Assert.Equal(HttpStatusCode.NotFound, privateRead.StatusCode);
        using var privateReply = await f.Client.PostAsJsonAsync($"/api/cases/support/{id}/messages", new { Message = "Synthetic foreign reply denied" }); Assert.Equal(HttpStatusCode.NotFound, privateReply.StatusCode);
        using var userResolve = await f.Client.PostAsJsonAsync($"/api/v1/admin/support/{id}/resolve", new A5ResolveRequest("Synthetic forbidden decision", "Synthetic decision", reject)); Assert.Equal(HttpStatusCode.Forbidden, userResolve.StatusCode);
        await f.ActorAsync(admin); using var assigned = await f.Client.PostAsJsonAsync($"/api/v1/admin/support/{id}/assign", new { OperatorId = admin.Id, Reason = "Synthetic take ownership" }); Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        await f.ActorAsync(other); using var steal = await f.Client.PostAsJsonAsync($"/api/v1/admin/support/{id}/assign", new { OperatorId = other.Id, Reason = "Synthetic unauthorized takeover" }); Assert.Equal(HttpStatusCode.Forbidden, steal.StatusCode);
        await f.ActorAsync(admin); using var info = await f.Client.PostAsJsonAsync($"/api/v1/admin/support/{id}/request-info", new { Reason = "Synthetic evidence request", Message = "Please provide synthetic receipt" }); Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        await using (var db = f.Context()) Assert.Equal(CaseStatus.WaitingForUser, (await db.SupportCases.SingleAsync()).Status);
        await f.ActorAsync(subject); using var reply = await f.Client.PostAsJsonAsync($"/api/cases/support/{id}/messages", new { Message = "Here is the synthetic receipt evidence" }); Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        await using (var db = f.Context()) Assert.Equal(CaseStatus.Open, (await db.SupportCases.SingleAsync()).Status);
        await f.ActorAsync(admin); using var resolution = await f.Client.PostAsJsonAsync($"/api/v1/admin/support/{id}/resolve", new A5ResolveRequest("Synthetic reviewed evidence", "Synthetic final support decision", reject)); Assert.Equal(HttpStatusCode.OK, resolution.StatusCode);
        await f.ActorAsync(subject); using var ownRead = await f.Client.GetAsync($"/api/cases/support/{id}"); Assert.Equal(HttpStatusCode.OK, ownRead.StatusCode);
        await using var verify = f.Context(); Assert.Equal(reject ? CaseStatus.Rejected : CaseStatus.Resolved, (await verify.SupportCases.SingleAsync()).Status);
        Assert.True(await verify.CaseHistories.AnyAsync(h => h.CaseId == id && h.Action == "provide_info"));
        Assert.True(await verify.AuditLogs.AnyAsync(a => a.Action == (reject ? "A5_CASE_REJECTED" : "A5_CASE_RESOLVED")));
        Assert.True(await verify.Notifications.AnyAsync(n => n.UserId == subject.Id)); Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        Assert.All(await verify.EmailOutboxMessages.ToListAsync(), m => Assert.Equal(EmailDeliveryStatus.Pending, m.Status));
    }

    [Fact]
    public async Task Broadcast_HTTP_SQL02_publish_cancel_dispatch_outbox_and_permission_are_consistent()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); var admin = CoreAcceptanceHttpTests.SyntheticUser(SystemRole.Admin); var recipient = CoreAcceptanceHttpTests.SyntheticUser();
        await using (var db = f.Context()) { db.AddRange(admin, recipient); await db.SaveChangesAsync(); }
        await f.ActorAsync(recipient);
        var request = new BroadcastDraftRequest("Operational", "User", "Synthetic notice", "Synthetic operational notice", "task17-publish", Pin, "Synthetic operations");
        using var forbidden = await f.Client.PostAsJsonAsync("/api/v1/system/broadcasts", request); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await f.ActorAsync(admin); using var draft = await f.Client.PostAsJsonAsync("/api/v1/system/broadcasts", request); Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        var b = (await draft.Content.ReadFromJsonAsync<V1BroadcastDto>())!;
        using var badPin = await f.Client.PostAsJsonAsync($"/api/v1/system/broadcasts/{b.Id}/publish", new BroadcastPublishRequest(null, b.Revision, "654321", "Synthetic wrong PIN")); Assert.Equal(HttpStatusCode.Forbidden, badPin.StatusCode);
        using var publish = await f.Client.PostAsJsonAsync($"/api/v1/system/broadcasts/{b.Id}/publish", new BroadcastPublishRequest(null, b.Revision, Pin, "Synthetic reviewed publication")); Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);
        using (var scope = f.Factory.Services.CreateScope()) { var service = scope.ServiceProvider.GetRequiredService<V1BroadcastService>(); await service.ProcessDueAsync(default); await service.ProcessDueAsync(default); }
        using var status = await f.Client.PostAsJsonAsync($"/api/v1/system/broadcasts/{b.Id}/status", new { Reason = "Synthetic persisted delivery verification" }); Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        var result = (await status.Content.ReadFromJsonAsync<V1BroadcastStatus>())!; Assert.Equal("Published", result.State); Assert.Equal(1, result.Persisted); Assert.Equal(0, result.PushSubmitted);
        using var next = await f.Client.PostAsJsonAsync("/api/v1/system/broadcasts", request with { IdempotencyKey = "task17-cancel" }); Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        var cancelId = (await next.Content.ReadFromJsonAsync<V1BroadcastDto>())!.Id;
        using var scheduled = await f.Client.PostAsJsonAsync($"/api/v1/system/broadcasts/{cancelId}/publish", new BroadcastPublishRequest(f.Clock.UtcNow.AddHours(1), 1, Pin, "Synthetic later notice")); Assert.Equal(HttpStatusCode.NoContent, scheduled.StatusCode);
        await f.ActorAsync(recipient); using var deniedCancel = await f.Client.PostAsJsonAsync($"/api/v1/system/broadcasts/{cancelId}/cancel", new MaintenanceActionRequest(Pin, "Synthetic denied cancel")); Assert.Equal(HttpStatusCode.Forbidden, deniedCancel.StatusCode);
        await f.ActorAsync(admin); using var cancelled = await f.Client.PostAsJsonAsync($"/api/v1/system/broadcasts/{cancelId}/cancel", new MaintenanceActionRequest(Pin, "Synthetic cancel future notice")); Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        f.Clock.UtcNow = f.Clock.UtcNow.AddHours(2); using (var scope = f.Factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<V1BroadcastService>().ProcessDueAsync(default);
        await using var verify = f.Context(); Assert.Single(await verify.Notifications.ToListAsync()); Assert.Single(await verify.OutboxMessages.ToListAsync()); Assert.Single(await verify.NotificationDeduplications.ToListAsync());
        Assert.Equal("Cancelled", await verify.V1Broadcasts.Where(x => x.Id == cancelId).Select(x => x.State).SingleAsync());
        foreach (var action in new[] { "V1_BROADCAST_DRAFTED", "V1_BROADCAST_SCHEDULED", "V1_BROADCAST_PERSISTED", "V1_BROADCAST_CANCELLED" }) Assert.True(await verify.AuditLogs.AnyAsync(a => a.Action == action));
        Assert.Empty(await verify.EmailOutboxMessages.ToListAsync()); Assert.Empty(await verify.LedgerTransactions.ToListAsync());
    }
}
