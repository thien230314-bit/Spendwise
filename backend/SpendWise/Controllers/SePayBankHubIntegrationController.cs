using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.BankIntegration.Commands.ProcessBankTransactionIpn;
using SpendWise.Application.Features.BankIntegration.Commands.ProcessLifecycleEvent;

namespace SpendWise.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/integrations/sepay/bankhub")]
public sealed class SePayBankHubIntegrationController(
    IMediator mediator,
    IBankHubConfiguration configuration) : ControllerBase
{
    [HttpPost("events")]
    public async Task<IntegrationAcknowledgementDto> Events(
        SePayLifecycleEventRequest request,
        [FromHeader(Name = "X-Secret-Key")] string? providerSecret,
        CancellationToken cancellationToken)
    {
        // Endpoint không dùng JWT nhưng bắt buộc xác thực secret server-to-server của SePay.
        if (!configuration.VerifyLifecycleSecret(providerSecret ?? string.Empty))
        {
            throw new AppException("bank_webhook_unauthorized", "Invalid lifecycle webhook secret.", 401);
        }
        return await mediator.Send(new ProcessLifecycleEventCommand(request), cancellationToken);
    }

    [HttpPost("ipn")]
    public async Task<IntegrationAcknowledgementDto> Ipn(
        SePayBankTransactionIpnRequest request,
        [FromHeader(Name = "Authorization")] string? providerAuthorization,
        CancellationToken cancellationToken)
    {
        // IPN dùng Authorization: Apikey riêng, không dùng access token SpendWise.
        if (!configuration.VerifyIpnAuthorization(providerAuthorization ?? string.Empty))
        {
            throw new AppException("bank_webhook_unauthorized", "Invalid IPN API key.", 401);
        }
        return await mediator.Send(new ProcessBankTransactionIpnCommand(request), cancellationToken);
    }
}
