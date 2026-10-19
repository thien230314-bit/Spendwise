using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Cases.Commands.CreateDispute;

// Command mở tranh chấp cho giao dịch theo workflow kiểm soát.

public sealed record CreateDisputeCommand(
    Guid UserId,
    CreateDisputeRequest Request) : IRequest<CaseDto>;
