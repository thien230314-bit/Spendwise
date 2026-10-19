using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Operations;

namespace SpendWise.API.Controllers;

[ApiController]
[Route("api/system")]
public sealed class OperationsController(IMediator mediator) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("maintenance")]
    public Task<MaintenanceStateDto> Maintenance(CancellationToken ct) =>
        mediator.Send(new GetMaintenanceStateQuery(), ct);

    [AllowAnonymous]
    [HttpGet("announcements/active")]
    public Task<EmergencyAnnouncementDto?> ActiveAnnouncement(CancellationToken ct) =>
        mediator.Send(new GetActiveAnnouncementQuery(), ct);
}

[ApiController]
[Authorize(Policy = "SystemAdmin")]
[Route("api/admin/operations")]
public sealed class AdminOperationsController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    [HttpPut("maintenance")]
    public Task<MaintenanceStateDto> ChangeMaintenance(ChangeMaintenanceRequest request, CancellationToken ct) =>
        mediator.Send(new ChangeMaintenanceStateCommand(AdminId(), request), ct);

    [HttpPost("announcements")]
    public Task<EmergencyAnnouncementDto> Publish(PublishAnnouncementRequest request, CancellationToken ct) =>
        mediator.Send(new PublishAnnouncementCommand(AdminId(), request), ct);

    [HttpDelete("announcements/{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, DeactivateAnnouncementRequest request, CancellationToken ct)
    {
        await mediator.Send(new DeactivateAnnouncementCommand(AdminId(), id, request), ct);
        return NoContent();
    }

    private Guid AdminId() => currentUser.UserId ?? throw new UnauthorizedAccessException();
}
