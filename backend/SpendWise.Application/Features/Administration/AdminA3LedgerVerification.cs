using Microsoft.EntityFrameworkCore;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Administration;

public sealed partial class AdminA3Service
{
    // Verify persisted provenance only; this never posts money or reconstructs historical entitlements.
    private async Task<IReadOnlyList<A3Money>> VerifyMoneyAsync(Guid fundId, bool v1, IReadOnlyList<A3Money> rows, CancellationToken ct)
    {
        var ids = rows.Where(r => r.LedgerTransactionId != null).Select(r => r.LedgerTransactionId!.Value).Distinct().ToArray();
        var postings = await db.LedgerTransactions.AsNoTracking().Where(t => ids.Contains(t.Id))
            .Include(t => t.Entries.OrderBy(e => e.Id).Take(101)).ToDictionaryAsync(t => t.Id, ct);
        var businessIds = postings.Values.Select(p => p.BusinessId).Distinct().ToArray();
        var duplicateBusinesses = await db.LedgerTransactions.AsNoTracking().Where(p => businessIds.Contains(p.BusinessId)).GroupBy(p => new { p.BusinessType, p.BusinessId })
            .Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync(ct);
        var contributions = await db.GroupContributions.AsNoTracking().Where(c => businessIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var expenses = await db.GroupExpenses.AsNoTracking().Where(e => businessIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var v1Contributions = await db.V1FundContributions.AsNoTracking().Where(c => c.FundId == fundId && businessIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var membershipIds = v1Contributions.Values.Select(c => c.MembershipId).Distinct().ToArray();
        var memberships = v1 ? await db.V1FundMemberships.AsNoTracking().Where(m => m.FundId == fundId && membershipIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct) : [];
        var walletIds = rows.Where(r => r.WalletId != null).Select(r => r.WalletId!.Value).Distinct().ToArray();
        var wallets = await db.Wallets.AsNoTracking().Where(w => walletIds.Contains(w.Id))
            .Select(w => new { w.Id, w.OwnerUserId, AccountId = w.LedgerAccount.Id, w.Currency }).ToDictionaryAsync(w => w.Id, ct);
        Guid? fundAccount = v1 ? await db.V1FundTreasuries.Where(t => t.FundId == fundId).Select(t => (Guid?)t.LedgerAccountId).SingleOrDefaultAsync(ct)
            : await db.SharedFunds.Where(t => t.GroupId == fundId).Select(t => (Guid?)t.LedgerAccountId).SingleOrDefaultAsync(ct);
        var accountIds = postings.Values.SelectMany(p => p.Entries.Select(e => e.LedgerAccountId)).Distinct().ToArray();
        var accounts = await db.LedgerAccounts.AsNoTracking().Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var histories = await db.FinancialTransactions.AsNoTracking().Where(t => ids.Contains(t.LedgerTransactionId))
            .Select(t => new { t.Id, t.LedgerTransactionId, t.OwnerUserId, t.RelatedBusinessId, t.Amount, t.Currency, t.WalletId, t.Status }).ToListAsync(ct);
        var result = new List<A3Money>();
        foreach (var original in rows)
        {
            var row = original with { At = Utc(original.At) };
            if (row.Verification == "FINANCE_BLOCKED") { result.Add(row); continue; }
            if (row.PostingCandidateCount > 1) { result.Add(row with { LedgerTransactionId = null, Verification = "UNVERIFIED" }); continue; }
            if (row.LedgerTransactionId is not {} id || !postings.TryGetValue(id, out var posting))
            { result.Add(row with { Verification = "MISSING_LEDGER" }); continue; }
            var entries = posting.Entries;
            bool Entry(Guid? account, LedgerEntryDirection direction) => account != null && entries.Count(e => e.LedgerAccountId == account && e.Direction == direction && e.Amount == row.Amount && e.Currency == row.Currency) == 1;
            bool WalletEntry(LedgerEntryDirection direction) => row.WalletId is {} w && wallets.TryGetValue(w, out var wallet)
                && wallet.OwnerUserId == row.UserId && wallet.Currency == row.Currency && Entry(wallet.AccountId, direction);
            var balanced = row.Amount > 0 && entries.Count == 2 && entries.All(e => e.Amount == row.Amount && e.Currency == row.Currency
                && accounts.TryGetValue(e.LedgerAccountId, out var a) && a.Currency == row.Currency)
                && entries.Count(e => e.Direction == LedgerEntryDirection.Debit) == 1 && entries.Count(e => e.Direction == LedgerEntryDirection.Credit) == 1
                && entries.Select(e => e.LedgerAccountId).Distinct().Count() == 2
                && fundAccount is {} fa && accounts.TryGetValue(fa, out var fundLedger) && fundLedger.Type == LedgerAccountType.SharedFund;
            var missing = false; var binding = false; var source = "NOT_RECORDED";
            if (v1 && posting.BusinessType == "V1FundContribution")
            {
                source = "V1FundContribution";
                binding = posting.BusinessId == row.Id && v1Contributions.TryGetValue(row.Id, out var c) && c.UserId == row.UserId
                    && c.WalletId == row.WalletId && c.AmountVnd == row.Amount && c.LedgerTransactionId == id
                    && memberships.TryGetValue(c.MembershipId, out var m) && m.UserId == row.UserId
                    && Entry(fundAccount, LedgerEntryDirection.Debit) && WalletEntry(LedgerEntryDirection.Credit);
            }
            else if (!v1 && row.Kind == "Contribution" && posting.BusinessType == "GroupContribution")
            {
                source = "GroupContribution"; missing = !contributions.TryGetValue(posting.BusinessId, out var c);
                binding = c != null && (row.BusinessSource != "GroupContribution" || row.Id == c.Id) && c.GroupId == fundId && c.UserId == row.UserId && c.WalletId == row.WalletId && c.Amount == row.Amount
                    && c.LedgerTransactionId == id && Entry(fundAccount, LedgerEntryDirection.Debit) && WalletEntry(LedgerEntryDirection.Credit);
            }
            else if (!v1 && row.Kind == "Expense" && posting.BusinessType == "GroupFundExpense")
            {
                source = "GroupExpense"; missing = !expenses.TryGetValue(posting.BusinessId, out var e);
                binding = e != null && (row.BusinessSource != "GroupExpense" || row.Id == e.Id) && e.GroupId == fundId && e.Amount == row.Amount && e.PaidByType == GroupExpensePaidByType.Fund && e.Status == GroupExpenseStatus.Recorded
                    && Entry(fundAccount, LedgerEntryDirection.Credit) && entries.Any(x => x.Direction == LedgerEntryDirection.Debit
                        && accounts.TryGetValue(x.LedgerAccountId, out var a) && a.Type == LedgerAccountType.ExpenseClearing && a.IsSystem && a.Code == "SYSTEM:GroupExpenseClearing:VND");
            }
            else if (!v1 && row.Kind == "Refund" && posting.BusinessType == "GroupRefund")
            {
                source = "GroupRefund"; binding = posting.BusinessId == row.Id && row.State == "Completed"
                    && Entry(fundAccount, LedgerEntryDirection.Credit) && WalletEntry(LedgerEntryDirection.Debit);
            }
            else missing = true;
            var related = histories.Where(h => h.LedgerTransactionId == id && h.OwnerUserId == row.UserId).ToList();
            var historyOk = related.All(h => h.Amount == row.Amount && h.Currency == row.Currency
                && h.Status == FinancialTransactionStatus.Completed && (h.RelatedBusinessId == null || h.RelatedBusinessId == posting.BusinessId) && (row.WalletId == null || h.WalletId == row.WalletId));
            result.Add(row with { LedgerReference = posting.Reference, BusinessId = posting.BusinessId, BusinessSource = source,
                UserProvenance = row.Kind == "Expense" ? row.UserId == null ? "NOT_RECORDED; original expense creator not stored" : "FundTransaction.UserId; expense creator not independently stored"
                    : binding ? "Business user and wallet owner matched" : "NOT_VERIFIED",
                FinancialTransactionIds = related.Select(h => h.Id).Order().ToArray(), FinancialHistoryVerification = related.Count == 0 ? "NOT_RECORDED" : historyOk ? "MATCHED" : "REQUIRES_REVIEW",
                Verification = !balanced || !historyOk || duplicateBusinesses.Any(b => b.BusinessId == posting.BusinessId && b.BusinessType == posting.BusinessType) || posting.Status != LedgerTransactionStatus.Completed ? "LEDGER_REQUIRES_REVIEW"
                    : missing ? "UNVERIFIED" : binding ? "LEDGER_COMPLETED" : "LEDGER_REQUIRES_REVIEW" });
        }
        return result;
    }
}
