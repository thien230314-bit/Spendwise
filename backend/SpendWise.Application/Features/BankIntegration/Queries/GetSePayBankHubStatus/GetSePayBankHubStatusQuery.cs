using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetSePayBankHubStatus;

public sealed record GetSePayBankHubStatusQuery : IRequest<SePayBankHubStatusDto>;
