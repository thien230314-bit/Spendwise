using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankConnections.Queries.GetBankConnectionById;

public sealed record GetBankConnectionByIdQuery(Guid UserId, Guid BankConnectionId) : IRequest<BankConnectionDto>;
