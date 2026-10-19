using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Wallets.Commands.CreateWallet;

// Command tạo ví mới và chọn ví mặc định trong cùng database transaction.

public sealed record CreateWalletCommand(
    Guid UserId,
    CreateWalletRequest Request) : IRequest<WalletDto>;
