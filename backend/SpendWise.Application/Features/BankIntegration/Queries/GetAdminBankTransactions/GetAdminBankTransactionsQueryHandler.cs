using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankTransactions;

public sealed class GetAdminBankTransactionsQueryHandler(
    ISpendWiseDbContext db)
    : IRequestHandler<GetAdminBankTransactionsQuery, PagedResult<AdminBankTransactionDto>>
{
    public async Task<PagedResult<AdminBankTransactionDto>> Handle(
        GetAdminBankTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var filter = query.Filter;
        InputBoundary.Page(filter.Page, filter.PageSize);
        if (filter.From is not null
            && filter.To is not null
            && filter.From.Value.ToUniversalTime() > filter.To.Value.ToUniversalTime())
        {
            throw new AppException("invalid_input", "From must not be later than To.");
        }

        // Giữ toàn bộ filter trên IQueryable để SQL Server lọc, join và phân trang trong database.
        var rows = db.BankTransactions.AsNoTracking().AsQueryable();
        if (filter.UserId is not null)
            rows = rows.Where(item => item.UserId == filter.UserId.Value);
        if (filter.BankConnectionId is not null)
            rows = rows.Where(item => item.BankConnectionId == filter.BankConnectionId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Provider))
        {
            if (!Enum.TryParse<BankProvider>(filter.Provider, true, out var provider)
                || !Enum.IsDefined(provider))
            {
                throw new AppException("invalid_input", "Bank provider is invalid.");
            }
            rows = rows.Where(item => item.Provider == provider);
        }
        if (!string.IsNullOrWhiteSpace(filter.Direction))
        {
            if (!Enum.TryParse<BankTransactionDirection>(filter.Direction, true, out var direction)
                || !Enum.IsDefined(direction))
            {
                throw new AppException("invalid_input", "Direction must be Credit or Debit.");
            }
            rows = rows.Where(item => item.Direction == direction);
        }
        if (filter.From is not null)
            rows = rows.Where(item => item.TransactionDate >= filter.From.Value.ToUniversalTime());
        if (filter.To is not null)
            rows = rows.Where(item => item.TransactionDate <= filter.To.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = InputBoundary.OptionalText(filter.Search, 200, "Search")!;
            rows = rows.Where(item =>
                item.Content.Contains(search)
                || item.ExternalTransactionId.Contains(search)
                || (item.ReferenceCode != null && item.ReferenceCode.Contains(search))
                || item.User.FullName.Contains(search)
                || item.User.SpendId.Contains(search)
                || item.BankConnection.BankBrandName.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(item => item.TransactionDate)
            .ThenByDescending(item => item.ImportedAt)
            .ThenByDescending(item => item.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(item => new AdminBankTransactionDto(
                item.Id,
                item.UserId,
                item.User.FullName,
                item.User.SpendId,
                item.BankConnectionId,
                item.Provider.ToString(),
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
                item.ImportedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminBankTransactionDto>(
            items,
            filter.Page,
            filter.PageSize,
            totalCount);
    }
}
