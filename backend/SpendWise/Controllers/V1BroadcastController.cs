using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.API.Infrastructure;
using SpendWise.Application.Features.Operations;

namespace SpendWise.API.Controllers;

[ApiController, Route("api/v1/system/broadcasts"), Authorize(Policy = V1Authorization.ManageMaintenance)]
public sealed class V1BroadcastController(V1BroadcastService service) : ControllerBase
{
    [HttpPost, EnableRateLimiting("financial")]
    public Task<V1BroadcastDto> Draft(BroadcastDraftRequest request, CancellationToken ct) => service.DraftAsync(User, request, ct);
    [HttpPost("{id:guid}/publish"), EnableRateLimiting("financial")]
    public async Task<IActionResult> Publish(Guid id, BroadcastPublishRequest request, CancellationToken ct) { await service.PublishAsync(User, id, request, ct); return NoContent(); }
    [HttpPost("{id:guid}/cancel"), EnableRateLimiting("financial")]
    public async Task<IActionResult> Cancel(Guid id, MaintenanceActionRequest request, CancellationToken ct) { await service.CancelAsync(User, id, request, ct); return NoContent(); }
    [HttpPost("{id:guid}/status")]
    public Task<V1BroadcastStatus> Status(Guid id, BroadcastStatusRequest request, CancellationToken ct) => service.StatusAsync(User, id, request.Reason, ct);
}
public sealed record BroadcastStatusRequest(string Reason);
