using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankConnections;

public sealed record GetAdminBankConnectionsQuery(
    Guid? UserId,
    string? Status,
    string? Provider,
    string? Bank) : IRequest<IReadOnlyList<AdminBankConnectionDto>>;
