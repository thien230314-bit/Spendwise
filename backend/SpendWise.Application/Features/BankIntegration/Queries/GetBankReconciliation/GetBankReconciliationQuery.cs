using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetBankReconciliation;

public sealed record GetBankReconciliationQuery : IRequest<BankReconciliationDto>;
