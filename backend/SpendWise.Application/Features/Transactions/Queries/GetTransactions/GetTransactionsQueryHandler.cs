using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Transactions.Queries.GetTransactions;

// Handler lấy giao dịch của người dùng theo bộ lọc được phép.

public sealed class GetTransactionsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetTransactionsQuery, PagedResult<TransactionDto>>
{
    public async Task<PagedResult<TransactionDto>> Handle(
        GetTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        // Lọc lịch sử tài chính của người dùng theo ví, danh mục, thời gian và phân trang.
        InputBoundary.Page(query.Filter.Page, query.Filter.PageSize);
        var page = query.Filter.Page;
        var pageSize = query.Filter.PageSize;
        var transactions = db.FinancialTransactions
            .AsNoTracking()
            .Where(transaction => transaction.OwnerUserId == query.UserId)
            .AsQueryable();

        if (query.Filter.WalletId is not null)
        {
            transactions = transactions.Where(
                transaction => transaction.WalletId == query.Filter.WalletId
                    || transaction.WalletAllocations.Any(allocation =>
                        allocation.WalletId == query.Filter.WalletId));
        }
        if (query.Filter.CategoryId is not null)
        {
            transactions = transactions.Where(
                transaction => transaction.CategoryId == query.Filter.CategoryId);
        }
        if (!string.IsNullOrWhiteSpace(query.Filter.Type))
        {
            if (!Enum.TryParse<FinancialTransactionType>(query.Filter.Type, true, out var type)
                || !Enum.IsDefined(type))
            {
                throw new AppException(
                    "invalid_transaction_type",
                    "Transaction type is invalid.");
            }

            transactions = transactions.Where(transaction => transaction.Type == type);
        }
        if (query.Filter.From is not null)
        {
            var from = query.Filter.From.Value.ToUniversalTime();
            transactions = transactions.Where(transaction => transaction.OccurredAt >= from);
        }
        if (query.Filter.To is not null)
        {
            var to = query.Filter.To.Value.ToUniversalTime();
            transactions = transactions.Where(transaction => transaction.OccurredAt <= to);
        }
        if (query.Filter.MinimumAmount is not null)
        {
            transactions = transactions.Where(
                transaction => transaction.Amount >= query.Filter.MinimumAmount);
        }
        if (query.Filter.MaximumAmount is not null)
        {
            transactions = transactions.Where(
                transaction => transaction.Amount <= query.Filter.MaximumAmount);
        }
        if (!string.IsNullOrWhiteSpace(query.Filter.Search))
        {
            transactions = transactions.Where(
                transaction => transaction.Description.Contains(query.Filter.Search));
        }

        var totalCount = await transactions.CountAsync(cancellationToken);
        var entities = await transactions
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(transaction => transaction.Wallet)
            .Include(transaction => transaction.Category)
            .Include(transaction => transaction.WalletAllocations)
                .ThenInclude(allocation => allocation.Wallet)
            .ToListAsync(cancellationToken);
        var items = entities.Select(transaction => TransactionMappings.ToDto(transaction)).ToList();

        return new PagedResult<TransactionDto>(
            items,
            page,
            pageSize,
            totalCount);
    }
}
