using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankTransactions.Queries.GetBankTransactions;

public sealed class GetBankTransactionsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetBankTransactionsQuery, PagedResult<BankTransactionDto>>
{
    public async Task<PagedResult<BankTransactionDto>> Handle(
        GetBankTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        InputBoundary.Page(query.Filter.Page, query.Filter.PageSize);
        var rows = db.BankTransactions
            .AsNoTracking()
            .Where(item => item.UserId == query.UserId);

        if (query.Filter.BankConnectionId is not null)
            rows = rows.Where(item => item.BankConnectionId == query.Filter.BankConnectionId);
        if (!string.IsNullOrWhiteSpace(query.Filter.Direction))
        {
            if (!Enum.TryParse<BankTransactionDirection>(query.Filter.Direction, true, out var direction)
                || !Enum.IsDefined(direction))
            {
                throw new AppException("bank_transaction_invalid", "Direction must be Credit or Debit.");
            }
            rows = rows.Where(item => item.Direction == direction);
        }
        if (query.Filter.From is not null)
            rows = rows.Where(item => item.TransactionDate >= query.Filter.From.Value.ToUniversalTime());
        if (query.Filter.To is not null)
            rows = rows.Where(item => item.TransactionDate <= query.Filter.To.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(query.Filter.Search))
        {
            var search = InputBoundary.OptionalText(query.Filter.Search, 200, "Search")!;
            rows = rows.Where(item => item.Content.Contains(search)
                || (item.ReferenceCode != null && item.ReferenceCode.Contains(search)));
        }

        var total = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(item => item.TransactionDate)
            .Skip((query.Filter.Page - 1) * query.Filter.PageSize)
            .Take(query.Filter.PageSize)
            .Select(item => new BankTransactionDto(
                item.Id,
                item.BankConnectionId,
                item.BankConnection.BankBrandName,
                item.BankConnection.MaskedAccountNumber,
                item.ExternalTransactionId,
                item.Direction.ToString(),
                item.Amount,
                item.Currency,
                item.TransactionDate,
                item.Content,
                item.ReferenceCode,
                item.PaymentCode,
                item.Gateway,
                item.Source.ToString(),
                item.ImportedAt,
                item.CategoryId,
                item.Category == null ? null : item.Category.Name))
            .ToListAsync(cancellationToken);
        return new PagedResult<BankTransactionDto>(items, query.Filter.Page, query.Filter.PageSize, total);
    }
}
