using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Admin.Transactions.Commands.SearchAdminTransactions;

// Handler tra cứu giao dịch ở chế độ quản trị chỉ đọc.

public sealed class SearchAdminTransactionsCommandHandler(
    ISpendWiseDbContext db,
    AdminAuditWriter audit)
    : IRequestHandler<SearchAdminTransactionsCommand, IReadOnlyList<AdminTransactionDto>>
{
    public async Task<IReadOnlyList<AdminTransactionDto>> Handle(
        SearchAdminTransactionsCommand command,
        CancellationToken cancellationToken)
    {
        // Tra cứu giao dịch quản trị theo bộ lọc và ghi audit truy cập.
        AdminAuditWriter.RequireReason(command.Request.Reason);
        audit.Add(
            command.AdminId,
            "VIEW_SENSITIVE_TRANSACTION",
            "FinancialTransaction",
            null,
            command.Request.Reason,
            null);
        await db.SaveChangesAsync(cancellationToken);

        var transactions = await db.FinancialTransactions
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .Select(item => new AdminTransactionDto(
                item.Id,
                item.Type,
                item.Status,
                item.Amount,
                item.Currency,
                item.LedgerTransactionId,
                item.RelatedBusinessId,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        return transactions;
    }
}
