using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Transactions.Commands.CreateExpense;
using SpendWise.Application.Features.Transactions.Commands.CreateIncome;
using SpendWise.Application.Features.Transactions.Commands.CreateMultiWalletExpense;
using SpendWise.Application.Features.Transactions.Commands.UpdateTransaction;
using SpendWise.Application.Features.Transactions.Queries.GetTransactionDetails;
using SpendWise.Application.Features.Transactions.Queries.GetTransactions;
using SpendWise.Application.Finance;

namespace SpendWise.API.Controllers;

// Cung cấp API thu, chi và tra cứu; Controller không tự cập nhật số dư.

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<TransactionDto>> Search(
        [FromQuery] TransactionQuery query,
        CancellationToken ct)
    {
        // Gửi Query lọc và phân trang lịch sử tài chính của người dùng.
        var userId = UserId();
        var transactionsQuery = new GetTransactionsQuery(userId, query);

        return await mediator.Send(transactionsQuery, ct);
    }

    [HttpGet("{transactionId:guid}")]
    public async Task<TransactionDto> Get(
        Guid transactionId,
        CancellationToken ct)
    {
        // Đọc một giao dịch theo Id và quyền sở hữu.
        var userId = UserId();
        var query = new GetTransactionDetailsQuery(userId, transactionId);

        return await mediator.Send(query, ct);
    }

    [HttpPost("income")]
    [EnableRateLimiting("financial")]
    public async Task<ActionResult<TransactionDto>> Income(
        CreateManualTransactionRequest request,
        CancellationToken ct)
    {
        // Gửi Command posting khoản thu vào ví của người dùng.
        var userId = UserId();
        var command = new CreateIncomeCommand(userId, request);
        var result = await mediator.Send(command, ct);

        return CreatedAtAction(
            nameof(Get),
            new { transactionId = result.Id },
            result);
    }

    [HttpPost("expense")]
    [EnableRateLimiting("financial")]
    public async Task<ActionResult<TransactionDto>> Expense(
        CreateManualTransactionRequest request,
        CancellationToken ct)
    {
        // Gửi Command posting khoản chi từ ví của người dùng.
        var userId = UserId();
        var command = new CreateExpenseCommand(userId, request);
        var result = await mediator.Send(command, ct);

        return CreatedAtAction(
            nameof(Get),
            new { transactionId = result.Id },
            result);
    }

    [HttpPost("expense/multi-wallet")]
    [EnableRateLimiting("financial")]
    public async Task<ActionResult<TransactionDto>> MultiWalletExpense(
        CreateMultiWalletExpenseRequest request,
        CancellationToken ct)
    {
        // UserId luôn lấy từ access token; từng ví được Handler kiểm tra quyền sở hữu.
        var result = await mediator.Send(
            new CreateMultiWalletExpenseCommand(UserId(), request),
            ct);
        return CreatedAtAction(nameof(Get), new { transactionId = result.Id }, result);
    }

    [HttpPatch("{transactionId:guid}")]
    [EnableRateLimiting("financial")]
    public async Task<TransactionDto> Update(
        Guid transactionId,
        UpdateTransactionRequest request,
        CancellationToken ct)
    {
        // Gửi Command correction; Handler xác minh PIN và tạo posting đảo/thay thế.
        var userId = UserId();
        var command = new UpdateTransactionCommand(userId, transactionId, request);

        return await mediator.Send(command, ct);
    }

    private Guid UserId()
    {
        // Lấy định danh từ phiên đã xác thực, không tin UserId do client gửi.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}
