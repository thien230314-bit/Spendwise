using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.API.Infrastructure;
using SpendWise.Application.Features.Administration;
using MediatR;
using SpendWise.Application.Admin;
using SpendWise.Application.Features.Admin.Dashboard.Queries.GetAdminDashboard;

namespace SpendWise.API.Controllers;

[ApiController, Route("api/v1/admin/overview"), Authorize(Policy = V1Authorization.OperationsRead)]
public sealed class AdminA1Controller(AdminA1Service overview, IMediator mediator) : ControllerBase
{
    [HttpGet("/api/admin/dashboard")]
    [HttpGet("/api/admin/reports")]
    public Task<AdminDashboardDto> Legacy([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        mediator.Send(new GetAdminDashboardQuery(from, to, User), ct);
    [HttpGet]
    public Task<A1Summary> Summary([FromQuery] DateOnly? date, CancellationToken ct) => overview.SummaryAsync(User, date, ct);
    [HttpGet("tasks")]
    public Task<A1Page<A1Task>> Tasks([FromQuery] A1ListQuery query, CancellationToken ct) => overview.TasksAsync(User, query, ct);
    [HttpGet("sources/{metric}")]
    public Task<A1Page<A1FeedItem>> Sources(string metric, [FromQuery] DateOnly? date, [FromQuery] A1ListQuery query, CancellationToken ct) => overview.SourceRecordsAsync(User, metric, date, query, ct);
    [HttpGet("alerts")]
    public Task<A1Page<A1FeedItem>> Alerts([FromQuery] A1ListQuery query, CancellationToken ct) => overview.AlertsAsync(User, query, ct);
    [HttpGet("activity")]
    public Task<A1Page<A1FeedItem>> Activity([FromQuery] A1ListQuery query, CancellationToken ct) => overview.ActivityAsync(User, query, ct);
}
