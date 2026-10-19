using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Transfers.Commands.P2PTransfer;

public sealed record P2PTransferCommand(
    Guid SenderUserId,
    P2PTransferRequest Request) : IRequest<TransferDto>;
