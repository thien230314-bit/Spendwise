using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Queries.GetRecurringTransactionById;

// Trả một định nghĩa định kỳ sau khi kiểm tra quyền sở hữu.
public sealed class GetRecurringTransactionByIdQueryHandler(
    ISpendWiseDbContext db,
    IClock clock) : IRequestHandler<GetRecurringTransactionByIdQuery, RecurringDto>
{
    public async Task<RecurringDto> Handle(
        GetRecurringTransactionByIdQuery query,
        CancellationToken cancellationToken)
    {
        // Không trả dữ liệu nếu định nghĩa không thuộc người dùng đang đăng nhập.
        var recurring = await db.RecurringTransactions
            .AsNoTracking()
            .Include(candidate => candidate.Occurrences)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == query.RecurringTransactionId
                    && candidate.OwnerUserId == query.UserId,
                cancellationToken)
            ?? throw new NotFoundException("recurring_not_found", "Recurring transaction not found.");

        return RecurringTransactionMappings.ToDto(recurring, clock.UtcNow);
    }
}
