using MediatR;
using SpendWise.Application.Banking;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.BankTransactions.Queries.GetBankTransactions;

public sealed record GetBankTransactionsQuery(
    Guid UserId,
    BankTransactionFilter Filter) : IRequest<PagedResult<BankTransactionDto>>;
