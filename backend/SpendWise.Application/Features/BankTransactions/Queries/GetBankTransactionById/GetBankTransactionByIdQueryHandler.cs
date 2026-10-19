using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;

namespace SpendWise.Application.Features.BankTransactions.Queries.GetBankTransactionById;

public sealed class GetBankTransactionByIdQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetBankTransactionByIdQuery, BankTransactionDto>
{
    public async Task<BankTransactionDto> Handle(
        GetBankTransactionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var transaction = await db.BankTransactions
            .AsNoTracking()
            .Include(item => item.BankConnection)
            .Include(item => item.Category)
            .SingleOrDefaultAsync(
                item => item.Id == query.BankTransactionId && item.UserId == query.UserId,
                cancellationToken)
            ?? throw new NotFoundException("bank_transaction_not_found", "Bank transaction not found.");
        return BankingMappings.ToTransactionDto(transaction);
    }
}
