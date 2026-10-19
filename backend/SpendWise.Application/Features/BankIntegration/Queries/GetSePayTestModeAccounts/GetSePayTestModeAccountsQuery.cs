using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetSePayTestModeAccounts;

public sealed record GetSePayTestModeAccountsQuery : IRequest<IReadOnlyList<SePayTestModeAccountDto>>;
