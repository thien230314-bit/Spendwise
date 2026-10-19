using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Wallets.Commands.CloseWallet;
using SpendWise.Application.Features.Wallets.Commands.CreateWallet;
using SpendWise.Application.Features.Wallets.Commands.UpdateWallet;
using SpendWise.Application.Features.Wallets.Queries.GetWalletDetails;
using SpendWise.Application.Features.Wallets.Queries.GetWallets;
using SpendWise.Application.Finance;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize]
[Route("api/wallets")]
public sealed class WalletsController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<WalletDto>> List(CancellationToken ct)
    {
        // Lấy danh sách ví thuộc người dùng hiện tại.
        var userId = UserId();
        var query = new GetWalletsQuery(userId);

        return await mediator.Send(query, ct);
    }

    [HttpGet("{walletId:guid}")]
    public async Task<WalletDto> Get(Guid walletId, CancellationToken ct)
    {
        // Query kèm UserId để Handler kiểm tra quyền sở hữu.
        var userId = UserId();
        var query = new GetWalletDetailsQuery(userId, walletId);

        return await mediator.Send(query, ct);
    }

    [HttpPost]
    public async Task<ActionResult<WalletDto>> Create(
        CreateWalletRequest request,
        CancellationToken ct)
    {
        // Gửi Command tạo ví và trả địa chỉ tài nguyên vừa tạo.
        var userId = UserId();
        var command = new CreateWalletCommand(userId, request);
        var wallet = await mediator.Send(command, ct);

        return CreatedAtAction(
            nameof(Get),
            new { walletId = wallet.Id },
            wallet);
    }

    [HttpPut("{walletId:guid}")]
    public async Task<WalletDto> Update(
        Guid walletId,
        UpdateWalletRequest request,
        CancellationToken ct)
    {
        // UpdateWallet chỉ sửa metadata; số dư không có trong request.
        return await mediator.Send(new UpdateWalletCommand(UserId(), walletId, request), ct);
    }

    [HttpDelete("{walletId:guid}")]
    public async Task<IActionResult> Close(
        Guid walletId,
        CancellationToken ct)
    {
        // Đóng ví thay vì xóa để giữ lịch sử giao dịch và ledger.
        var userId = UserId();
        var command = new CloseWalletCommand(userId, walletId);
        await mediator.Send(command, ct);

        return NoContent();
    }

    private Guid UserId()
    {
        // Không chấp nhận UserId do client tự khai báo.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}
