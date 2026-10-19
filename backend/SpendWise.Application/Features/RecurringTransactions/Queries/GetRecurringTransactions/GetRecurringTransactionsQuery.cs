using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Queries.GetRecurringTransactions;

// Query lấy các cấu hình giao dịch định kỳ của người dùng.

public sealed record GetRecurringTransactionsQuery(
    Guid UserId) : IRequest<IReadOnlyList<RecurringDto>>;
