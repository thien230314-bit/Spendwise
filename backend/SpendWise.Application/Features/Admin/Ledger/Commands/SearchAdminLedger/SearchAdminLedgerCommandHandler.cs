using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Admin.Ledger.Commands.SearchAdminLedger;

// Handler tra cứu posting ledger cho đối soát mà không chỉnh sửa bút toán.

public sealed class SearchAdminLedgerCommandHandler(
    ISpendWiseDbContext db,
    AdminAuditWriter audit)
    : IRequestHandler<SearchAdminLedgerCommand, IReadOnlyList<LedgerTransactionDto>>
{
    public async Task<IReadOnlyList<LedgerTransactionDto>> Handle(
        SearchAdminLedgerCommand command,
        CancellationToken cancellationToken)
    {
        // Tra cứu ledger theo bộ lọc đồng thời ghi audit cho thao tác nhạy cảm.
        AdminAuditWriter.RequireReason(command.Request.Reason);
        audit.Add(
            command.AdminId,
            "VIEW_LEDGER",
            "LedgerTransaction",
            null,
            command.Request.Reason,
            null);
        await db.SaveChangesAsync(cancellationToken);

        var rows = await db.LedgerTransactions
            .AsNoTracking()
            .Include(item => item.Entries)
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        var result = new List<LedgerTransactionDto>();
        foreach (var row in rows)
        {
            var debitTotal = row.Entries
                .Where(entry => entry.Direction == LedgerEntryDirection.Debit)
                .Sum(entry => entry.Amount);
            var creditTotal = row.Entries
                .Where(entry => entry.Direction == LedgerEntryDirection.Credit)
                .Sum(entry => entry.Amount);
            result.Add(new LedgerTransactionDto(
                row.Id,
                row.Reference,
                row.BusinessType,
                row.BusinessId,
                row.Status.ToString(),
                debitTotal,
                creditTotal,
                debitTotal == creditTotal && debitTotal > 0,
                row.CreatedAt));
        }

        return result;
    }
}
