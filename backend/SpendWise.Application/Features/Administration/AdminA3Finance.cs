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
    public Guid? BusinessId { get; init; }
    public string BusinessSource { get; init; } = "NOT_RECORDED";
    public IReadOnlyList<Guid> FinancialTransactionIds { get; init; } = [];
    public string FinancialHistoryVerification { get; init; } = "NOT_RECORDED";
    public int? PostingCandidateCount { get; init; }
    public string UserProvenance { get; init; } = "NOT_RECORDED";
}
public sealed record A3Treasury(Guid? LedgerAccountId, string? Currency, decimal? StoredBalance, decimal? CompletedLedgerNet,
    bool ZeroLedgerEvidence, string HoldAvailability, string ExecutionAvailability);
public sealed record A3Entry(Guid Id, Guid AccountId, string Direction, decimal Amount, string Currency);
public sealed record A3Posting(Guid Id, string Reference, string Status, string BusinessType, Guid BusinessId, DateTime? CompletedAt,
    IReadOnlyList<A3Entry> Entries, bool Truncated);
public sealed record A3PostingCandidate(Guid Id, string Reference, string Status, DateTime CreatedAt, DateTime? CompletedAt);

public sealed partial class AdminA3Service
{
    public Task<A2Page<A3PostingCandidate>> ExpensePostingsAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, Guid expenseId, A3FeedQuery q, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, q.Reason, "EXPENSE_POSTINGS", async (_, fund, token) =>
        {
            ValidateFeed(q); if (q.ParentId != null) throw new AppException("invalid_posting_filter", "Expense is selected by route.");
            if (fund.Code != null || !await db.GroupExpenses.AnyAsync(e => e.Id == expenseId && e.GroupId == fundId && e.PaidByType == SpendWise.Domain.Groups.GroupExpensePaidByType.Fund, token))
                throw new ForbiddenException("expense_scope_denied", "A stored Legacy fund expense is required.");
            var rows = db.LedgerTransactions.AsNoTracking().Where(p => p.BusinessType == "GroupFundExpense" && p.BusinessId == expenseId);
            if (q.Status != null)
            {
                if (!Enum.TryParse<LedgerTransactionStatus>(q.Status, out var state) || !Enum.IsDefined(state) || state.ToString() != q.Status) throw new AppException("invalid_posting_status", "Use a stored ledger status.");
                rows = rows.Where(p => p.Status == state);
            }
            var total = await rows.CountAsync(token);
            var page = await rows.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip(Skip(q)).Take(q.PageSize)
                .Select(p => new A3PostingCandidate(p.Id, p.Reference, p.Status.ToString(), p.CreatedAt, p.CompletedAt)).ToListAsync(token);
            return new A2Page<A3PostingCandidate>(clock.UtcNow, q.Page, q.PageSize, total,
                page.Select(p => p with { CreatedAt = Utc(p.CreatedAt), CompletedAt = p.CompletedAt is {} at ? Utc(at) : null }).ToList());
        }, ct);

    public Task<A3Posting> PostingAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, Guid postingId, string reason, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, reason, "POSTING", async (_, fund, token) =>
        {
            var linked = fund.Code != null
                ? await db.V1FundContributions.AnyAsync(c => c.FundId == fundId && c.LedgerTransactionId == postingId, token)
                : await db.FundTransactions.AnyAsync(t => t.GroupId == fundId && t.LedgerTransactionId == postingId, token)
                    || await db.GroupContributions.AnyAsync(c => c.GroupId == fundId && c.LedgerTransactionId == postingId, token)
                    || await db.LedgerTransactions.AnyAsync(p => p.Id == postingId && p.BusinessType == "GroupFundExpense" && db.GroupExpenses.Any(e => e.Id == p.BusinessId && e.GroupId == fundId && e.PaidByType == SpendWise.Domain.Groups.GroupExpensePaidByType.Fund), token)
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
                "Contributions" => db.GroupContributions.AsNoTracking().Where(c => c.GroupId == fundId).Select(c => new A3Money { Id = c.Id, Kind = "Contribution", BusinessSource = "GroupContribution", Amount = c.Amount, UserId = c.UserId, WalletId = c.WalletId, LedgerTransactionId = c.LedgerTransactionId, At = c.CreatedAt, State = "StoredLegacyContribution", Currency = db.Groups.Where(g => g.Id == fundId).Select(g => g.Currency).First() }),
                "Expenses" => db.GroupExpenses.AsNoTracking().Where(e => e.GroupId == fundId && e.PaidByType == SpendWise.Domain.Groups.GroupExpensePaidByType.Fund).Select(e => new A3Money { Id = e.Id, Kind = "Expense", BusinessSource = "GroupExpense", Amount = e.Amount, At = e.Date, State = e.Status.ToString(),
                    Currency = db.Groups.Where(g => g.Id == fundId).Select(g => g.Currency).First(), LedgerTransactionId = db.LedgerTransactions.Where(t => t.BusinessType == "GroupFundExpense" && t.BusinessId == e.Id).OrderBy(t => t.Id).Select(t => (Guid?)t.Id).FirstOrDefault(),
                    PostingCandidateCount = db.LedgerTransactions.Count(t => t.BusinessType == "GroupFundExpense" && t.BusinessId == e.Id) }),
                "Transactions" => db.FundTransactions.AsNoTracking().Where(t => t.GroupId == fundId).Select(t => new A3Money { Id = t.Id, Kind = t.Type.ToString(), Amount = t.Amount, Currency = db.Groups.Where(g => g.Id == fundId).Select(g => g.Currency).First(), UserId = t.UserId, WalletId = t.WalletId, LedgerTransactionId = t.LedgerTransactionId, At = t.Date, State = "StoredLegacyHistory" }),
                "Refunds" => db.GroupRefunds.AsNoTracking().Where(r => db.GroupDeletionRequests.Any(d => d.Id == r.GroupDeletionRequestId && d.GroupId == fundId)).Select(r => new A3Money { Id = r.Id, Kind = "Refund", Amount = r.RefundAmount, UserId = r.UserId, WalletId = r.DestinationWalletId, LedgerTransactionId = r.LedgerTransactionId, At = r.CompletedAt ?? r.CreatedAt, State = r.Status.ToString(), Currency = db.Groups.Where(g => g.Id == fundId).Select(g => g.Currency).First() }),
                _ => throw new AppException("invalid_finance_source", "Legacy sources: Contributions, Expenses, Transactions or Refunds.") };
            if (q.Status is {} state) rows = rows.Where(r => r.State == state);
            var total = await rows.CountAsync(token); var page = await rows.OrderByDescending(r => r.At).ThenBy(r => r.Id).Skip(Skip(q)).Take(q.PageSize).ToListAsync(token);
            page = page.Select(r => r with { ReviewedAt = r.ReviewedAt is {} reviewed ? Utc(reviewed) : null, CancelledAt = r.CancelledAt is {} cancelled ? Utc(cancelled) : null }).ToList();
            var result = await VerifyMoneyAsync(fundId, fund.Code != null, page, token);
            return new A2Page<A3Money>(clock.UtcNow, q.Page, q.PageSize, total, result);
        }, ct);

    public Task<A3Treasury> TreasuryAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, string reason, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, reason, "TREASURY", (_, fund, token) => ReadTreasuryAsync(fundId, fund.Code != null, token), ct);

    private async Task<A3Treasury> ReadTreasuryAsync(Guid fundId, bool v1, CancellationToken token)
        {
            Guid? id = v1 ? await db.V1FundTreasuries.Where(t => t.FundId == fundId).Select(t => (Guid?)t.LedgerAccountId).SingleOrDefaultAsync(token)
                : await db.SharedFunds.Where(t => t.GroupId == fundId).Select(t => (Guid?)t.LedgerAccountId).SingleOrDefaultAsync(token);
            var account = await db.LedgerAccounts.AsNoTracking().Where(a => a.Id == id).Select(a => new { a.Id, a.Currency, a.Balance }).SingleOrDefaultAsync(token);
            if (account == null) return new A3Treasury(null, null, null, null, false, "NO_HOLD_EXECUTION_CONTRACT", "FINANCE_BLOCKED");
            var entries = db.LedgerEntries.AsNoTracking().Where(e => e.LedgerAccountId == account.Id && e.LedgerTransaction.Status == LedgerTransactionStatus.Completed);
            var net = (await entries.Where(e => e.Direction == LedgerEntryDirection.Debit).SumAsync(e => (decimal?)e.Amount, token) ?? 0)
                - (await entries.Where(e => e.Direction == LedgerEntryDirection.Credit).SumAsync(e => (decimal?)e.Amount, token) ?? 0);
            var hasEntries = await entries.AnyAsync(token);
            var invalid = await entries.AnyAsync(e => e.Currency != account.Currency || e.Amount <= 0 || e.Direction != LedgerEntryDirection.Credit && e.Direction != LedgerEntryDirection.Debit, token)
                || await db.LedgerEntries.AnyAsync(e => e.LedgerAccountId == account.Id && e.LedgerTransaction.Status == LedgerTransactionStatus.Pending, token);
            return new A3Treasury(account.Id, account.Currency, account.Balance, net,
                hasEntries && !invalid && account.Balance == 0 && net == 0,
                "NO_HOLD_EXECUTION_CONTRACT; no hold or release inferred", "FINANCE_BLOCKED except existing contribution posting; zero evidence is not proof all refunds completed");
        }
}
