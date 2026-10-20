using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.API.Infrastructure;
using SpendWise.Application.Features.Administration;

namespace SpendWise.API.Controllers;

public sealed record A2ReasonRequest(string Reason);

[ApiController, Route("api/v1/admin"), Authorize(Policy = V1Authorization.OperationsRead)]
public sealed class AdminA2Controller(AdminA2Service users) : ControllerBase
{
    [HttpPost("users/search")]
    public Task<A2Page<A2User>> Search(A2Query query, CancellationToken ct) => users.SearchAsync(User, query, ct);

    [HttpPost("support/{caseId:guid}/users/{userId:guid}/profile")]
    public Task<A2Profile> Profile(Guid caseId, Guid userId, A2ReasonRequest request, CancellationToken ct) =>
        users.ProfileAsync(User, caseId, userId, request.Reason, ct);

    [HttpPost("support/{caseId:guid}/users/{userId:guid}/sessions")]
    public Task<A2Page<A2Session>> Sessions(Guid caseId, Guid userId, A2Query query, CancellationToken ct) => users.SessionsAsync(User, caseId, userId, query, ct);

    [HttpPost("support/{caseId:guid}/users/{userId:guid}/restrictions")]
    public Task<A2Restrictions> Restrictions(Guid caseId, Guid userId, A2ReasonRequest request, CancellationToken ct) =>
        users.RestrictionsAsync(User, caseId, userId, request.Reason, ct);

    [HttpPost("support/{caseId:guid}/users/{userId:guid}/revoke-sessions")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Revoke(Guid caseId, Guid userId, A2RevokeRequest request, CancellationToken ct)
    { await users.RevokeAsync(User, caseId, userId, request, ct); return NoContent(); }

    [HttpPut("support/{caseId:guid}/users/{userId:guid}/status")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Status(Guid caseId, Guid userId, A2StatusRequest request, CancellationToken ct)
    { await users.ChangeStatusAsync(User, caseId, userId, request, ct); return NoContent(); }

    [HttpPost("support/{caseId:guid}/users/{userId:guid}/groups")]
    public Task<A2Page<A2Group>> Groups(Guid caseId, Guid userId, A2Query query, CancellationToken ct) => users.GroupsAsync(User, caseId, userId, query, ct);
    [HttpPost("support/{caseId:guid}/users/{userId:guid}/friends")]
    public Task<A2Page<A2Friend>> Friends(Guid caseId, Guid userId, A2Query query, CancellationToken ct) => users.FriendsAsync(User, caseId, userId, query, ct);
    [HttpPost("support/{caseId:guid}/users/{userId:guid}/history")]
    public Task<A2Page<A2HistoryItem>> History(Guid caseId, Guid userId, A2Query query, CancellationToken ct) => users.HistoryAsync(User, caseId, userId, query, ct);
}
