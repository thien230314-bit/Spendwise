using MediatR;

namespace SpendWise.Application.Features.Wallets.Commands.CloseWallet;

// Command đóng ví khi thỏa điều kiện và giữ nguyên lịch sử tài chính.

public sealed record CloseWalletCommand(
    Guid UserId,
    Guid WalletId) : IRequest;
