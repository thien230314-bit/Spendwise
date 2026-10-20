using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Operations;

// The caller holds exclusive maintenance admission until the reopening transaction commits.
public sealed class V1LedgerMaintenanceSafety(ISpendWiseDbContext db, IClock clock) : IV1MaintenanceSafety
{
    public async Task<MaintenanceSafetyResult> CheckAsync(Guid planId, CancellationToken ct)
    {
        if (db is not DbContext context || context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Reopening safety requires an owned database transaction.");
        await db.AcquireMaintenanceAdmissionAsync(true, ct);
        if (!await db.V1MaintenancePlans.AnyAsync(p => p.Id == planId && p.StartsAt <= clock.UtcNow && p.CancelledAt == null && p.ClosedAt == null, ct))
            return new(false, null);
        if (await db.LedgerTransactions.AnyAsync(t => t.Status == LedgerTransactionStatus.Pending, ct)
            || await db.Transfers.AnyAsync(t => t.Status == TransferStatus.Processing, ct)
            || await db.FinancialTransactions.AnyAsync(t => t.Status == FinancialTransactionStatus.Processing, ct)
            || await db.Wallets.AnyAsync(w => !db.LedgerAccounts.Any(a => a.WalletId == w.Id && a.Currency == w.Currency && a.Balance == w.AvailableBalance), ct)
            || await db.LedgerAccounts.AnyAsync(a => a.Balance != db.LedgerEntries
                .Where(e => e.LedgerAccountId == a.Id && (e.LedgerTransaction.Status == LedgerTransactionStatus.Completed || e.LedgerTransaction.Status == LedgerTransactionStatus.Reversed))
                .Sum(e => e.Direction == LedgerEntryDirection.Debit ? e.Amount : -e.Amount), ct)
            || await db.LedgerTransactions.Where(t => t.Status == LedgerTransactionStatus.Completed || t.Status == LedgerTransactionStatus.Reversed).AnyAsync(t =>
                t.Entries.Count < 2 || t.Entries.Any(e => e.Amount <= 0 || e.Currency != e.LedgerAccount.Currency)
                || t.Entries.Select(e => e.Currency).Distinct().Count() != 1
                || t.Entries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount)
                    != t.Entries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount), ct))
            return new(false, null);
        return new(true, $"core-ledger:{planId:N}:{clock.UtcNow.Ticks}");
    }
}
