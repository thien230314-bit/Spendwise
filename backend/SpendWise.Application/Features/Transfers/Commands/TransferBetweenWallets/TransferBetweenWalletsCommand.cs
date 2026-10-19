using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Transfers.Commands.TransferBetweenWallets;

// Command chuyển tiền giữa hai ví cùng chủ trong một database transaction.

public sealed record TransferBetweenWalletsCommand(
    Guid UserId,
    InternalTransferRequest Request) : IRequest<TransferDto>;
