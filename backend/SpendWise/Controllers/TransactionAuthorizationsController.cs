using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Finance.TransactionAuthorizations;

namespace SpendWise.API.Controllers;

[ApiController]
[Route("api/transaction-authorizations")]
[Authorize]
public sealed class TransactionAuthorizationsController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("options")]
    [EnableRateLimiting("financial")]
    public async Task<ActionResult<TransactionAuthorizationOptionsResponse>> CreateOptions(
        [FromBody] CreateTransactionAuthorizationOptionsRequest request,
        CancellationToken ct)
    {
        var response = await mediator.Send(new CreateTransactionAuthorizationOptionsCommand(UserId(), request), ct);
        return Ok(response);
    }

    [HttpPost("verify")]
    [EnableRateLimiting("financial")]
    public async Task<ActionResult<TransactionAuthorizationExecutionResult>> VerifyAndExecute(
        [FromBody] VerifyTransactionAuthorizationRequest request,
        CancellationToken ct)
    {
        var response = await mediator.Send(new VerifyAndExecuteTransactionAuthorizationCommand(UserId(), request), ct);
        return Ok(response);
    }

    private Guid UserId() => currentUser.UserId ?? throw new UnauthorizedAccessException();
}
