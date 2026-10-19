using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.API.Infrastructure;
using SpendWise.Application.Features.Operations;

namespace SpendWise.API.Controllers;

[ApiController]
[Route("api/v1/system/maintenance")]
public sealed class V1MaintenanceController(V1MaintenanceService maintenance, V1MaintenanceReminders reminders) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public Task<IReadOnlyList<MaintenancePlanDto>> Status(CancellationToken ct) => maintenance.StatusAsync(ct);
    [HttpPost]
    [Authorize(Policy = V1Authorization.ManageMaintenance)]
    public Task<MaintenancePlanDto> Schedule(ScheduleMaintenanceRequest request, CancellationToken ct) => maintenance.ScheduleAsync(User, request, ct);
    [HttpPost("{id:guid}/reminders"), Authorize(Policy = V1Authorization.ManageMaintenance)]
    public async Task<IActionResult> Reminders(Guid id, MaintenanceReminderRequest request, CancellationToken ct) { await reminders.ConfigureAsync(User, id, request, ct); return NoContent(); }
    [HttpPost("emergency"), Authorize(Policy = V1Authorization.ManageMaintenance)]
    public Task<MaintenancePlanDto> Emergency(EmergencyMaintenanceRequest request, CancellationToken ct) => maintenance.EmergencyAsync(User, request, ct);
    [HttpPost("{id:guid}/extend"), Authorize(Policy = V1Authorization.ManageMaintenance)]
    public async Task<IActionResult> Extend(Guid id, ExtendMaintenanceRequest request, CancellationToken ct) { await maintenance.ExtendAsync(User, id, request, ct); return NoContent(); }
    [HttpPut("{id:guid}"), Authorize(Policy = V1Authorization.ManageMaintenance)]
    public async Task<IActionResult> Reschedule(Guid id, RescheduleMaintenanceRequest request, CancellationToken ct) { await maintenance.RescheduleAsync(User, id, request, ct); return NoContent(); }
    [HttpPost("{id:guid}/review"), Authorize(Policy = V1Authorization.ManageMaintenance)]
    public async Task<IActionResult> Review(Guid id, MaintenanceActionRequest request, CancellationToken ct) { await maintenance.ReviewEmergencyAsync(User, id, request, ct); return NoContent(); }
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = V1Authorization.ManageMaintenance)]
    public async Task<IActionResult> Cancel(Guid id, MaintenanceActionRequest request, CancellationToken ct) { await maintenance.CancelAsync(User, id, request, ct); return NoContent(); }
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = V1Authorization.ManageMaintenance)]
    public async Task<IActionResult> Close(Guid id, MaintenanceActionRequest request, CancellationToken ct) { await maintenance.CloseAsync(User, id, request, ct); return NoContent(); }
}
