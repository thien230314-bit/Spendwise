using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.AccountRecovery;
using SpendWise.Application.Features.AccountRecovery.Commands.SubmitAccountRecovery;

namespace SpendWise.API.Controllers;

// Nhận yêu cầu khôi phục nhưng luôn trả response trung lập để chống dò tài khoản.
[ApiController]
[Route("api/account-recovery")]
[EnableRateLimiting("auth")]
public sealed class AccountRecoveryController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Submit(
        SubmitAccountRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SubmitAccountRecoveryCommand(request), cancellationToken);
        return Accepted(result);
    }
}
