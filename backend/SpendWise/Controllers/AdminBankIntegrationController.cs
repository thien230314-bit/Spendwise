using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Banking;
using SpendWise.Application.Finance;
using SpendWise.Application.Features.BankIntegration.Commands.CreateSePaySandboxTransaction;
using SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankConnectionById;
using SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankConnections;
using SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankTransactionById;
using SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankTransactions;
using SpendWise.Application.Features.BankIntegration.Queries.GetBankIntegrationEvents;
using SpendWise.Application.Features.BankIntegration.Queries.GetBankReconciliation;
using SpendWise.Application.Features.BankIntegration.Queries.GetSePayBankHubStatus;
using SpendWise.Application.Features.BankIntegration.Queries.GetSePayTestModeAccounts;
using SpendWise.Application.Common.Exceptions;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize(Policy = "SystemAdmin")]
[Route("api/admin")]
public sealed class AdminBankIntegrationController(
    IMediator mediator,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("bank-connections")]
    public async Task<IReadOnlyList<AdminBankConnectionDto>> Connections(
        [FromQuery] Guid? userId,
        [FromQuery] string? status,
        [FromQuery] string? provider,
        [FromQuery] string? bank,
        CancellationToken cancellationToken) => await mediator.Send(
            new GetAdminBankConnectionsQuery(userId, status, provider, bank),
            cancellationToken);

    [HttpGet("bank-connections/{id:guid}")]
    public async Task<AdminBankConnectionDto> Connection(
        Guid id,
        CancellationToken cancellationToken) => await mediator.Send(
            new GetAdminBankConnectionByIdQuery(id),
            cancellationToken);

    [HttpGet("bank-transactions")]
    public async Task<PagedResult<AdminBankTransactionDto>> Transactions(
        [FromQuery] AdminBankTransactionFilter filter,
        CancellationToken cancellationToken) => await mediator.Send(
            new GetAdminBankTransactionsQuery(filter),
            cancellationToken);

    [HttpGet("bank-transactions/{id:guid}")]
    public async Task<AdminBankTransactionDto> Transaction(
        Guid id,
        CancellationToken cancellationToken) => await mediator.Send(
            new GetAdminBankTransactionByIdQuery(id),
            cancellationToken);

    [HttpGet("bank-integration/events")]
    public async Task<IReadOnlyList<AdminBankIntegrationEventDto>> Events(
        [FromQuery] string? status,
        [FromQuery] string? eventType,
        [FromQuery] string? externalTransactionId,
        [FromQuery] string? externalBankAccountId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken) => await mediator.Send(
            new GetBankIntegrationEventsQuery(
                status,
                eventType,
                externalTransactionId,
                externalBankAccountId,
                from,
                to),
            cancellationToken);

    [HttpGet("bank-integration/reconciliation")]
    public async Task<BankReconciliationDto> Reconciliation(CancellationToken cancellationToken) =>
        await mediator.Send(new GetBankReconciliationQuery(), cancellationToken);

    [HttpGet("integrations/sepay/status")]
    public async Task<SePayBankHubStatusDto> Status(CancellationToken cancellationToken) =>
        await mediator.Send(new GetSePayBankHubStatusQuery(), cancellationToken);

    [HttpGet("integrations/sepay/test-mode/bank-accounts")]
    public async Task<IReadOnlyList<SePayTestModeAccountDto>> TestModeAccounts(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            throw new NotFoundException("sepay_test_mode_unavailable", "Test Mode account lookup is unavailable.");
        return await mediator.Send(new GetSePayTestModeAccountsQuery(), cancellationToken);
    }

    [HttpPost("integrations/sepay/sandbox/transactions")]
    public async Task<BankSandboxTransactionAcceptedDto> SandboxTransaction(
        CreateBankSandboxTransactionRequest request,
        CancellationToken cancellationToken)
    {
        // ASP.NET environment được truyền rõ để Handler cấm simulator ngoài Development.
        return await mediator.Send(
            new CreateSePaySandboxTransactionCommand(environment.IsDevelopment(), request),
            cancellationToken);
    }
}
