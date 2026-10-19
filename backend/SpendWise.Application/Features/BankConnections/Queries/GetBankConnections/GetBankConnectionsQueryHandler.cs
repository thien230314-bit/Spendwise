using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;

namespace SpendWise.Application.Features.BankConnections.Queries.GetBankConnections;

public sealed class GetBankConnectionsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetBankConnectionsQuery, IReadOnlyList<BankConnectionDto>>
{
    public async Task<IReadOnlyList<BankConnectionDto>> Handle(
        GetBankConnectionsQuery query,
        CancellationToken cancellationToken)
    {
        // Chỉ trả các connection thuộc UserId từ access token.
        return await db.BankConnections
            .AsNoTracking()
            .Where(item => item.UserId == query.UserId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new BankConnectionDto(
                item.Id,
                item.Provider.ToString(),
                item.AccountEnvironment.ToString(),
                item.BankBrandName,
                item.AccountType,
                item.AccountHolderName,
                item.MaskedAccountNumber,
                item.Status.ToString(),
                item.LinkedAt,
                item.LastSyncedAt))
            .ToListAsync(cancellationToken);
    }
}
