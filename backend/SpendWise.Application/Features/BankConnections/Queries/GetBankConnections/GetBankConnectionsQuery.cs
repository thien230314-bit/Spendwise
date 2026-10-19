using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankConnections.Queries.GetBankConnections;

public sealed record GetBankConnectionsQuery(Guid UserId) : IRequest<IReadOnlyList<BankConnectionDto>>;
