using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Wallets.Commands.UpdateWallet;

// Yêu cầu sửa metadata của ví, không cho phép truyền số dư.
public sealed record UpdateWalletCommand(
    Guid UserId,
    Guid WalletId,
    UpdateWalletRequest Request) : IRequest<WalletDto>;
