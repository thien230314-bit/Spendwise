using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.MobilePush;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize(Policy = "User")]
[Route("api/mobile/push/installations")]
public sealed class MobilePushController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPut("{installationId:guid}")]
    public async Task<IActionResult> Register(
        Guid installationId,
        RegisterMobilePushInstallationRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new RegisterMobilePushInstallationCommand(UserId(), installationId, request),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{installationId:guid}")]
    public async Task<IActionResult> Unregister(
        Guid installationId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new UnregisterMobilePushInstallationCommand(UserId(), installationId),
            cancellationToken);
        return NoContent();
    }

    private Guid UserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException();
}
