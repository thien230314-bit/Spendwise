using MediatR;
using SpendWise.Application.AccountRecovery;

namespace SpendWise.Application.Features.AccountRecovery.Queries.GetAccountRecoveryRequests;

public sealed record GetAccountRecoveryRequestsQuery(
    AdminAccountRecoveryFilter Filter)
    : IRequest<IReadOnlyList<AccountRecoveryDto>>;
