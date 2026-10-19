using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AccountRecovery;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.AccountRecovery.Queries.GetAccountRecoveryRequests;

public sealed class GetAccountRecoveryRequestsQueryHandler(
    ISpendWiseDbContext db)
    : IRequestHandler<GetAccountRecoveryRequestsQuery, IReadOnlyList<AccountRecoveryDto>>
{
    public async Task<IReadOnlyList<AccountRecoveryDto>> Handle(
        GetAccountRecoveryRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var requests = db.AccountRecoveryRequests.AsNoTracking().Include(item => item.Reviews).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Filter.Status))
        {
            if (!Enum.TryParse<AccountRecoveryStatus>(query.Filter.Status, true, out var status))
            {
                throw new AppException("invalid_recovery_status", "Account recovery status is invalid.");
            }
            requests = requests.Where(item => item.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(query.Filter.Search))
        {
            var search = query.Filter.Search.Trim();
            requests = requests.Where(item => item.FullName.Contains(search)
                || item.Phone.Contains(search)
                || (item.EmailHint != null && item.EmailHint.Contains(search)));
        }

        var result = await requests.OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return result.Select(AccountRecoveryMappings.ToDto).ToList();
    }
}
