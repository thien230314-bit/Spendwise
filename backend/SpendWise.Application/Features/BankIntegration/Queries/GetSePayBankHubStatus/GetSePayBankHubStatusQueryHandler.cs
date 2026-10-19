using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetSePayBankHubStatus;

public sealed class GetSePayBankHubStatusQueryHandler(
    IBankHubConfiguration configuration) : IRequestHandler<GetSePayBankHubStatusQuery, SePayBankHubStatusDto>
{
    public Task<SePayBankHubStatusDto> Handle(
        GetSePayBankHubStatusQuery query,
        CancellationToken cancellationToken) => Task.FromResult(configuration.GetSafeStatus());
}
