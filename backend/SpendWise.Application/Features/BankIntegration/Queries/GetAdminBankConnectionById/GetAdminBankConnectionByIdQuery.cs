using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankConnectionById;

public sealed record GetAdminBankConnectionByIdQuery(Guid BankConnectionId) : IRequest<AdminBankConnectionDto>;
