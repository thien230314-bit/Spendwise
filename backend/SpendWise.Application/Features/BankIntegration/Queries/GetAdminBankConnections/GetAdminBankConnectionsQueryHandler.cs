using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankConnections;

public sealed class GetAdminBankConnectionsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetAdminBankConnectionsQuery, IReadOnlyList<AdminBankConnectionDto>>
{
    public async Task<IReadOnlyList<AdminBankConnectionDto>> Handle(
        GetAdminBankConnectionsQuery query,
        CancellationToken cancellationToken)
    {
        var rows = db.BankConnections.AsNoTracking().AsQueryable();
        if (query.UserId is not null) rows = rows.Where(item => item.UserId == query.UserId);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<BankConnectionStatus>(query.Status, true, out var status) || !Enum.IsDefined(status))
                throw new AppException("invalid_input", "Bank connection status is invalid.");
            rows = rows.Where(item => item.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            if (!Enum.TryParse<BankProvider>(query.Provider, true, out var provider) || !Enum.IsDefined(provider))
                throw new AppException("invalid_input", "Bank provider is invalid.");
            rows = rows.Where(item => item.Provider == provider);
        }
        if (!string.IsNullOrWhiteSpace(query.Bank))
        {
            var bank = InputBoundary.OptionalText(query.Bank, 100, "Bank")!;
            rows = rows.Where(item => item.BankBrandName.Contains(bank));
        }

        return await rows
            .OrderByDescending(item => item.CreatedAt)
            .Take(500)
            .Select(item => new AdminBankConnectionDto(
                item.Id,
                item.UserId,
                item.Provider.ToString(),
                item.AccountEnvironment.ToString(),
                item.ExternalAccountId,
                item.BankBrandName,
                item.MaskedAccountNumber,
                item.Status.ToString(),
                item.LinkedAt,
                item.LastSyncedAt,
                item.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
