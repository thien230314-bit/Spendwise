using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Administration;

public sealed record ScopedLedgerCurrencyTotal(string Currency, decimal Debit, decimal Credit, bool Balanced);
public sealed record ScopedCaseTransaction(Guid Id, string Type, string Status, decimal Amount, string Currency,
    DateTime OccurredAt, Guid LedgerTransactionId, string LedgerStatus, IReadOnlyList<ScopedLedgerCurrencyTotal> LedgerTotals);

public sealed class ScopedCaseTransactionReader(ISpendWiseDbContext db, PlatformPermissionService permissions)
{
    public async Task<ScopedCaseTransaction> GetAsync(ClaimsPrincipal principal, Guid caseId, Guid subjectId, string reason, CancellationToken ct)
    {
        Guid? actor = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
        string purpose;
        try
        {
            purpose = InputBoundary.RequiredText(reason, 1000, "Reason");
            AdminAuditWriter.RequireReason(purpose);
        }
        catch (AppException)
        {
            await AuditAsync(actor, caseId, subjectId, "Invalid access reason", "InvalidReason", null, ct);
            throw;
        }
        var subjects = permissions.SupportSubjects(principal, caseId, subjectId);
        if (!await subjects.AnyAsync(ct))
        {
            await AuditAsync(actor, caseId, subjectId, purpose, "ScopeDenied", null, ct);
            throw new ForbiddenException("case_access_denied", "An assigned active case is required.");
        }
        var transactions = db.FinancialTransactions.AsNoTracking().Where(t => subjects.Any(u => u.Id == t.OwnerUserId)
            && db.SupportCases.Any(c => c.Id == caseId && c.LinkedTransactionId == t.Id));
        var transaction = await transactions.Select(t => new { t.Id, t.Type, t.Status, t.Amount, t.Currency, t.OccurredAt,
            t.LedgerTransactionId, LedgerStatus = t.LedgerTransaction.Status }).SingleOrDefaultAsync(ct);
        if (transaction is null)
        {
            await AuditAsync(actor, caseId, subjectId, purpose, "TransactionUnavailable", null, ct);
            throw new NotFoundException("case_transaction_not_found", "Case has no accessible linked transaction.");
        }
        // Query-time case/session predicates also protect the second data read from reassignment.
        var entries = await db.LedgerEntries.AsNoTracking().Where(e => e.LedgerTransactionId == transaction.LedgerTransactionId
                && transactions.Any(t => t.LedgerTransactionId == e.LedgerTransactionId))
            .Select(e => new { e.Currency, e.Direction, e.Amount }).ToListAsync(ct);
        if (!await transactions.AnyAsync(t => t.Id == transaction.Id, ct))
        {
            await AuditAsync(actor, caseId, subjectId, purpose, "ScopeChanged", null, ct);
            throw new ForbiddenException("case_access_denied", "Case access changed.");
        }
        var totals = entries.GroupBy(e => e.Currency).OrderBy(g => g.Key).Select(g =>
        {
            var debit = g.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
            var credit = g.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);
            return new ScopedLedgerCurrencyTotal(g.Key, debit, credit,
                debit == credit && g.All(e => e.Amount >= 0 && Enum.IsDefined(e.Direction)));
        }).ToList();
        await AuditAsync(actor, caseId, subjectId, purpose, "Allowed", transaction.Id, ct);
        return new(transaction.Id, transaction.Type.ToString(), transaction.Status.ToString(), transaction.Amount,
            transaction.Currency, transaction.OccurredAt, transaction.LedgerTransactionId, transaction.LedgerStatus.ToString(), totals);
    }

    private async Task AuditAsync(Guid? actor, Guid caseId, Guid subject, string reason, string outcome, Guid? transaction, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog { ActorUserId = actor, Action = outcome == "Allowed" ? "V1_CASE_TRANSACTION_READ" : "V1_CASE_TRANSACTION_READ_DENIED",
            TargetType = "SupportCase", TargetId = caseId.ToString(), Reason = reason,
            MetadataJson = JsonSerializer.Serialize(new { SubjectUserId = subject, Permission = "SensitiveRead", Outcome = outcome, TransactionId = transaction }) });
        await db.SaveChangesAsync(ct);
    }
}
