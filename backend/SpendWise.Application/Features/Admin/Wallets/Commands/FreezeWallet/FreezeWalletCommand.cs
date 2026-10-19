using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Wallets.Commands.FreezeWallet;

// Command đóng băng ví theo quyền quản trị mà không xóa lịch sử tài chính.

public sealed record FreezeWalletCommand(
    Guid AdminId,
    Guid WalletId,
    FreezeWalletRequest Request) : IRequest;
