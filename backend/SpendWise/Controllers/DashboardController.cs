using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Dashboard;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize(Policy = "User")]
[Route("api/dashboard")]
public sealed class DashboardController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken ct) =>
        mediator.Send(new GetDashboardQuery(currentUser.UserId ?? throw new UnauthorizedAccessException()), ct);
}
