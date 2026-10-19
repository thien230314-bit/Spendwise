using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.BankTransactions.Queries.GetBankTransactionById;
using SpendWise.Application.Features.BankTransactions.Queries.GetBankTransactions;
using SpendWise.Application.Features.BankTransactions.Commands.ReclassifyBankTransaction;
using SpendWise.Application.Finance;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize]
[Route("api/bank-transactions")]
public sealed class BankTransactionsController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<BankTransactionDto>> List(
        [FromQuery] BankTransactionFilter filter,
        CancellationToken cancellationToken)
    {
        // Query luôn kèm UserId từ JWT nên không thể đọc giao dịch ngân hàng của người khác.
        return await mediator.Send(new GetBankTransactionsQuery(UserId(), filter), cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<BankTransactionDto> Get(Guid id, CancellationToken cancellationToken)
    {
        return await mediator.Send(new GetBankTransactionByIdQuery(UserId(), id), cancellationToken);
    }

    [HttpPatch("{id:guid}/category")]
    public Task<BankTransactionDto> Reclassify(
        Guid id,
        ReclassifyBankTransactionRequest request,
        CancellationToken cancellationToken) =>
        mediator.Send(new ReclassifyBankTransactionCommand(UserId(), id, request), cancellationToken);

    private Guid UserId() => currentUser.UserId ?? throw new UnauthorizedAccessException();
}
