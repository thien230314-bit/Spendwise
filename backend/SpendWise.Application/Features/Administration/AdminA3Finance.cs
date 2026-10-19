using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Administration;

public sealed record A3Money
{
    public Guid Id { get; init; }
    public string Kind { get; init; } = "";
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public Guid? UserId { get; init; }
    public Guid? RequestedByUserId { get; init; }
    public Guid? ReviewedByUserId { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public Guid? WalletId { get; init; }
    public Guid? LedgerTransactionId { get; init; }
    public DateTime At { get; init; }
    public string State { get; init; } = "";
    public string Verification { get; init; } = "UNVERIFIED";
    public string? LedgerReference { get; init; }
}
public sealed record A3Treasury(Guid? LedgerAccountId, string? Currency, decimal? StoredBalance, decimal? CompletedLedgerNet,
    bool ZeroLedgerEvidence, string HoldAvailability, string ExecutionAvailability);
public sealed record A3Entry(Guid Id, Guid AccountId, string Direction, decimal Amount, string Currency);
public sealed record A3Posting(Guid Id, string Reference, string Status, string BusinessType, Guid BusinessId, DateTime? CompletedAt,
    IReadOnlyList<A3Entry> Entries, bool Truncated);

public sealed partial class AdminA3Service
{
    public Task<A3Posting> PostingAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, Guid postingId, string reason, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, reason, "POSTING", async (_, fund, token) =>
        {
            var linked = fund.Code != null
                ? await db.V1FundContributions.AnyAsync(c => c.FundId == fundId && c.LedgerTransactionId == postingId, token)
                : await db.FundTransactions.AnyAsync(t => t.GroupId == fundId && t.LedgerTransactionId == postingId, token)
                    || await db.GroupRefunds.AnyAsync(r => r.LedgerTransactionId == postingId && db.GroupDeletionRequests.Any(d => d.Id == r.GroupDeletionRequestId && d.GroupId == fundId), token);
            Guid? account = fund.Code != null ? await db.V1FundTreasuries.Where(t => t.FundId == fundId).Select(t => (Guid?)t.LedgerAccountId).SingleOrDefaultAsync(token)
                : await db.SharedFunds.Where(t => t.GroupId == fundId).Select(t => (Guid?)t.LedgerAccountId).SingleOrDefaultAsync(token);
            if (!linked || account == null || !await db.LedgerEntries.AnyAsync(e => e.LedgerTransactionId == postingId && e.LedgerAccountId == account, token))
                throw new ForbiddenException("posting_scope_denied", "A stored fund reference and fund-account entry are required.");
            var posting = await db.LedgerTransactions.AsNoTracking().SingleAsync(t => t.Id == postingId, token);
            var entries = await db.LedgerEntries.AsNoTracking().Where(e => e.LedgerTransactionId == postingId).OrderBy(e => e.Id)
                .Select(e => new A3Entry(e.Id, e.LedgerAccountId, e.Direction.ToString(), e.Amount, e.Currency)).Take(101).ToListAsync(token);
            return new A3Posting(posting.Id, posting.Reference, posting.Status.ToString(), posting.BusinessType, posting.BusinessId,
                posting.CompletedAt is {} at ? Utc(at) : null, entries.Take(100).ToList(), entries.Count > 100);
        }, ct);

    public Task<A2Page<A3Money>> FinanceAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, string source, A3FeedQuery q, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, q.Reason, "FINANCE", async (_, fund, token) =>
        {
            ValidateFeed(q); if (q.ParentId != null) throw new AppException("invalid_finance_filter", "Use source and status filters.");
            IQueryable<A3Money> rows;
            if (fund.Code != null)
            {
                rows = source switch {
                    "Contributions" => db.V1FundContributions.AsNoTracking().Where(c => c.FundId == fundId).Select(c => new A3Money { Id = c.Id, Kind = "Contribution", Amount = c.AmountVnd, UserId = c.UserId, WalletId = c.WalletId, LedgerTransactionId = c.LedgerTransactionId, At = c.CreatedAt, State = "StoredContribution" }),
                    "Requests" => db.V1FundRequests.AsNoTracking().Where(r => r.FundId == fundId).Select(r => new A3Money { Id = r.Id, Kind = r.Kind, Amount = r.AmountVnd, UserId = r.RecipientUserId, RequestedByUserId = r.RequestedByUserId, ReviewedByUserId = r.ReviewedByUserId, ReviewedAt = r.ReviewedAt, CancelledAt = r.CancelledAt, At = r.CreatedAt, State = r.State, Verification = "FINANCE_BLOCKED" }),
                    _ => throw new AppException("invalid_finance_source", "V1 sources: Contributions or Requests.") };
            }
            else rows = source switch {
                "Transactions" => db.FundTransactions.AsNoTracking().Where(t => t.GroupId == fundId).Select(t => new A3Money { Id = t.Id, Kind = t.Type.ToString(), Amount = t.Amount, Currency = db.Groups.Where(g => g.Id == fundId).Select(g => g.Currency).First(), UserId = t.UserId, WalletId = t.WalletId, LedgerTransactionId = t.LedgerTransactionId, At = t.Date, State = "StoredLegacyHistory" }),
                "Refunds" => db.GroupRefunds.AsNoTracking().Where(r => db.GroupDeletionRequests.Any(d => d.Id == r.GroupDeletionRequestId && d.GroupId == fundId)).Select(r => new A3Money { Id = r.Id, Kind = "Refund", Amount = r.RefundAmount, UserId = r.UserId, WalletId = r.DestinationWalletId, LedgerTransactionId = r.LedgerTransactionId, At = r.CompletedAt ?? r.CreatedAt, State = r.Status.ToString(), Currency = db.Groups.Where(g => g.Id == fundId).Select(g => g.Currency).First() }),
                _ => throw new AppException("invalid_finance_source", "Legacy sources: Transactions or Refunds.") };
            if (q.Status is {} state) rows = rows.Where(r => r.State == state);
            var total = await rows.CountAsync(token); var page = await rows.OrderByDescending(r => r.At).ThenBy(r => r.Id).Skip(Skip(q)).Take(q.PageSize).ToListAsync(token);
            page = page.Select(r => r with { ReviewedAt = r.ReviewedAt is {} reviewed ? Utc(reviewed) : null, CancelledAt = r.CancelledAt is {} cancelled ? Utc(cancelled) : null }).ToList();
            var ids = page.Where(r => r.LedgerTransactionId != null).Select(r => r.LedgerTransactionId!.Value).Distinct().ToArray();
            var postings = await db.LedgerTransactions.AsNoTracking().Include(t => t.Entries).Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, token);
            var walletIds = page.Where(r => r.WalletId != null).Select(r => r.WalletId!.Value).Distinct().ToArray();
            var wallets = await db.Wallets.AsNoTracking().Where(w => walletIds.Contains(w.Id))
                .Select(w => new { w.Id, w.OwnerUserId, AccountId = w.LedgerAccount.Id }).ToDictionaryAsync(w => w.Id, token);
            Guid? account = fund.Code != null ? await db.V1FundTreasuries.Where(t => t.FundId == fundId).Select(t => (Guid?)t.LedgerAccountId).SingleOrDefaultAsync(token)
                : await db.SharedFunds.Where(t => t.GroupId == fundId).Select(t => (Guid?)t.LedgerAccountId).SingleOrDefaultAsync(token);
            var result = page.Select(row => {
                if (row.LedgerTransactionId is not {} id || !postings.TryGetValue(id, out var posting)) return row with { At = Utc(row.At), Verification = row.Verification == "FINANCE_BLOCKED" ? "FINANCE_BLOCKED" : "MISSING_LEDGER" };
                var entries = posting.Entries;
                var balanced = entries.Count >= 2 && entries.All(e => e.Amount > 0 && e.Currency == row.Currency && e.Direction is LedgerEntryDirection.Debit or LedgerEntryDirection.Credit)
                    && entries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount) == entries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);
                var fundEntry = account != null && entries.Any(e => e.LedgerAccountId == account && e.Amount == row.Amount);
                var binding = fund.Code == null || posting.BusinessId == row.Id && posting.BusinessType == "V1FundContribution"
                    && entries.Any(e => e.LedgerAccountId == account && e.Direction == LedgerEntryDirection.Debit && e.Amount == row.Amount)
                    && row.WalletId is {} walletId && wallets.TryGetValue(walletId, out var wallet) && wallet.OwnerUserId == row.UserId
                    && entries.Any(e => e.Direction == LedgerEntryDirection.Credit && wallet.AccountId == e.LedgerAccountId && e.Amount == row.Amount);
                return row with { At = Utc(row.At), LedgerReference = posting.Reference,
                    Verification = balanced && fundEntry && binding && posting.Status == LedgerTransactionStatus.Completed
                        ? fund.Code != null ? "LEDGER_COMPLETED" : "LEDGER_POSTING_BALANCED_BUSINESS_BINDING_UNVERIFIED" : "LEDGER_REQUIRES_REVIEW" };
            }).ToList();
            return new A2Page<A3Money>(clock.UtcNow, q.Page, q.PageSize, total, result);
        }, ct);

    public Task<A3Treasury> TreasuryAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, string reason, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, reason, "TREASURY", async (_, fund, token) =>
        {
            Guid? id = fund.Code != null ? await db.V1FundTreasuries.Where(t => t.FundId == fundId).Select(t => (Guid?)t.LedgerAccountId).SingleOrDefaultAsync(token)
                : await db.SharedFunds.Where(t => t.GroupId == fundId).Select(t => (Guid?)t.LedgerAccountId).SingleOrDefaultAsync(token);
            var account = await db.LedgerAccounts.AsNoTracking().Where(a => a.Id == id).Select(a => new { a.Id, a.Currency, a.Balance }).SingleOrDefaultAsync(token);
            if (account == null) return new A3Treasury(null, null, null, null, false, "NO_HOLD_EXECUTION_CONTRACT", "FINANCE_BLOCKED");
            var entries = db.LedgerEntries.AsNoTracking().Where(e => e.LedgerAccountId == account.Id && e.LedgerTransaction.Status == LedgerTransactionStatus.Completed);
            var net = (await entries.Where(e => e.Direction == LedgerEntryDirection.Debit).SumAsync(e => (decimal?)e.Amount, token) ?? 0)
                - (await entries.Where(e => e.Direction == LedgerEntryDirection.Credit).SumAsync(e => (decimal?)e.Amount, token) ?? 0);
            var hasEntries = await entries.AnyAsync(token);
            var invalid = await entries.AnyAsync(e => e.Currency != account.Currency || e.Amount <= 0, token)
                || await db.LedgerEntries.AnyAsync(e => e.LedgerAccountId == account.Id && e.LedgerTransaction.Status == LedgerTransactionStatus.Pending, token);
            return new A3Treasury(account.Id, account.Currency, account.Balance, net,
                hasEntries && !invalid && account.Balance == 0 && net == 0,
                "NO_HOLD_EXECUTION_CONTRACT; no hold or release inferred", "FINANCE_BLOCKED except existing contribution posting; zero evidence is not proof all refunds completed");
        }, ct);
}
