using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Transactions.Queries.GetTransactions;

// Query lấy giao dịch của người dùng theo bộ lọc được phép.

public sealed record GetTransactionsQuery(
    Guid UserId,
    TransactionQuery Filter) : IRequest<PagedResult<TransactionDto>>;
