using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Transfers.Commands.TransferBetweenWallets;
using SpendWise.Application.Banking;
using SpendWise.Application.Features.BankDirectPayments;
using SpendWise.Application.Finance;

namespace SpendWise.API.Controllers;

// Nhận yêu cầu chuyển tiền và chuyển toàn bộ kiểm tra tài chính sang Application.

[ApiController]
[Authorize]
[Route("api/transfers")]
public sealed class TransfersController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("recipients")]
    public async Task<BankTransferRecipientDto> Find(
        [FromQuery] string phone,
        CancellationToken ct)
    {
        // Tìm đúng số điện thoại và chỉ trả snapshot ngân hàng đã che.
        var userId = UserId();
        var query = new FindBankTransferRecipientQuery(userId, phone);

        return await mediator.Send(query, ct);
    }

    [HttpGet("{transferId:guid}")]
    public async Task<BankDirectPaymentDto> Get(
        Guid transferId,
        CancellationToken ct)
    {
        // Đọc lệnh bank-direct khi người dùng là bên gửi hoặc bên nhận.
        var userId = UserId();
        var query = new GetBankDirectPaymentQuery(userId, transferId);

        return await mediator.Send(query, ct);
    }

    [HttpPost]
    [EnableRateLimiting("financial")]
    public async Task<ActionResult<BankDirectPaymentDto>> Send(
        CreateBankDirectTransferRequest request,
        CancellationToken ct)
    {
        // Tạo hướng dẫn chuyển khoản; bước này không đổi số dư SpendWise Wallet.
        var userId = UserId();
        var command = new CreateBankDirectTransferCommand(
            userId,
            currentUser.SessionId,
            request);
        var result = await mediator.Send(command, ct);

        return CreatedAtAction(
            nameof(Get),
            new { transferId = result.Id },
            result);
    }

    [HttpGet]
    public Task<PagedResult<BankDirectPaymentDto>> History(
        [FromQuery] BankDirectPaymentHistoryFilter filter,
        CancellationToken ct) =>
        mediator.Send(new GetBankDirectPaymentHistoryQuery(UserId(), filter), ct);

    [HttpPost("{paymentId:guid}/confirm")]
    [EnableRateLimiting("financial")]
    public Task<BankDirectPaymentDto> Confirm(
        Guid paymentId,
        ConfirmBankDirectPaymentRequest request,
        CancellationToken ct) =>
        mediator.Send(new ConfirmBankDirectPaymentCommand(UserId(), paymentId, request), ct);

    [HttpPost("internal")]
    [EnableRateLimiting("financial")]
    public async Task<ActionResult<TransferDto>> Internal(
        InternalTransferRequest request,
        CancellationToken ct)
    {
        // Gửi Command chuyển tiền giữa hai ví cùng chủ sở hữu.
        var userId = UserId();
        var command = new TransferBetweenWalletsCommand(userId, request);
        var result = await mediator.Send(command, ct);

        // No internal-transfer GET resource exists; return the result without a dead Location.
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("p2p")]
    [EnableRateLimiting("financial")]
    public async Task<ActionResult<TransferDto>> P2P(
        P2PTransferRequest request,
        CancellationToken ct)
    {
        // Chuyển tiền P2P giữa hai tài khoản User trên SpendWise
        var userId = UserId();
        var command = new SpendWise.Application.Features.Transfers.Commands.P2PTransfer.P2PTransferCommand(userId, request);
        var result = await mediator.Send(command, ct);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    private Guid UserId()
    {
        // Lấy định danh từ phiên đã xác thực, không tin UserId do client gửi.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}
