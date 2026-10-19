using MediatR;
using SpendWise.Application.AccountRecovery;

namespace SpendWise.Application.Features.AccountRecovery.Queries.GetAccountRecoveryRequestById;

public sealed record GetAccountRecoveryRequestByIdQuery(
    Guid RequestId) : IRequest<AccountRecoveryDto>;
