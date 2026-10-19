using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication.Commands.ChangeSecurityPin;
using SpendWise.Application.Features.Authentication.Commands.VerifySecurityPin;
using SpendWise.Application.Features.Authentication.Queries.GetSecurityPinStatus;
using SpendWise.Application.Identity;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("auth")]
[Route("api/security-pin")]
public sealed class SecurityPinController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("status")]
    public Task<SecurityPinStatusDto> Status(CancellationToken cancellationToken) =>
        mediator.Send(new GetSecurityPinStatusQuery(RequiredUser()), cancellationToken);

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(
        VerifySecurityPinRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new VerifySecurityPinCommand(RequiredUser(), request),
            cancellationToken);
        return NoContent();
    }

    [HttpPut]
    public async Task<IActionResult> Change(
        ChangeSecurityPinRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new ChangeSecurityPinCommand(RequiredUser(), request),
            cancellationToken);
        return NoContent();
    }

    private Guid RequiredUser() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException();
}
