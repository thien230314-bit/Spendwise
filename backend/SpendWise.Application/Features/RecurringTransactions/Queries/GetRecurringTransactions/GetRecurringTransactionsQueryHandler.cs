using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Queries.GetRecurringTransactions;

// Handler lấy các cấu hình giao dịch định kỳ của người dùng.

public sealed class GetRecurringTransactionsQueryHandler(
    ISpendWiseDbContext db,
    IClock clock)
    : IRequestHandler<GetRecurringTransactionsQuery, IReadOnlyList<RecurringDto>>
{
    public async Task<IReadOnlyList<RecurringDto>> Handle(
        GetRecurringTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        // Lấy các lịch giao dịch định kỳ thuộc người dùng theo lần chạy kế tiếp.
        var transactions = await db.RecurringTransactions
            .AsNoTracking()
            .Include(transaction => transaction.Occurrences)
            .Where(transaction => transaction.OwnerUserId == query.UserId)
            .OrderBy(transaction => transaction.NextRunAt)
            .ToListAsync(cancellationToken);

        return transactions.Select(transaction =>
            RecurringTransactionMappings.ToDto(transaction, clock.UtcNow)).ToList();
    }
}
