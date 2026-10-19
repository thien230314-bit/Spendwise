using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetSePayTestModeAccounts;

public sealed class GetSePayTestModeAccountsQueryHandler(ISePayTestModeAccountReader reader)
    : IRequestHandler<GetSePayTestModeAccountsQuery, IReadOnlyList<SePayTestModeAccountDto>>
{
    public Task<IReadOnlyList<SePayTestModeAccountDto>> Handle(
        GetSePayTestModeAccountsQuery query, CancellationToken cancellationToken) =>
        reader.GetAccountsAsync(cancellationToken);
}
