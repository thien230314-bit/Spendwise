using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication.Passkeys;
using SpendWise.Application.Identity;

namespace SpendWise.API.Controllers;

[ApiController]
[Route("api/auth/passkeys")]
[EnableRateLimiting("auth")]
public sealed class PasskeysController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("register/options")]
    [Authorize]
    public async Task<ActionResult<PasskeyRegistrationOptionsResponse>> GetRegistrationOptions(CancellationToken ct)
    {
        var response = await mediator.Send(new GetPasskeyRegistrationOptionsCommand(UserId()), ct);
        return Ok(response);
    }

    [HttpPost("register/verify")]
    [Authorize]
    public async Task<ActionResult<PasskeyCredentialDto>> VerifyRegistration(
        [FromBody] VerifyPasskeyRegistrationRequest request,
        CancellationToken ct)
    {
        var response = await mediator.Send(new VerifyPasskeyRegistrationCommand(UserId(), request), ct);
        return Ok(response);
    }

    [HttpPost("login/options")]
    [AllowAnonymous]
    public async Task<ActionResult<PasskeyLoginOptionsResponse>> GetLoginOptions(
        [FromBody] PasskeyLoginOptionsRequest? request,
        CancellationToken ct)
    {
        var response = await mediator.Send(new GetPasskeyLoginOptionsCommand(request?.Email), ct);
        return Ok(response);
    }

    [HttpPost("login/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> VerifyLogin(
        [FromBody] VerifyPasskeyLoginRequest request,
        CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var response = await mediator.Send(new VerifyPasskeyLoginCommand(request, ip, userAgent), ct);
        return Ok(response);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<PasskeyCredentialDto>>> GetUserPasskeys(CancellationToken ct)
    {
        var response = await mediator.Send(new GetUserPasskeysQuery(UserId()), ct);
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokePasskey(Guid id, CancellationToken ct)
    {
        await mediator.Send(new RevokePasskeyCommand(UserId(), id), ct);
        return NoContent();
    }

    private Guid UserId() => currentUser.UserId ?? throw new UnauthorizedAccessException();
}
