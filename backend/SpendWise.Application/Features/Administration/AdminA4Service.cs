using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.Administration;

public sealed class AdminA4Service(
    ISpendWiseDbContext db,
    PlatformPermissionService permissions,
    V1CaseWorkflow workflow,
    AdminAuditWriter audit,
    IClock clock)
{
    // A4.1 - Tất cả giao dịch
    public Task<A2Page<A4TransactionItem>> TransactionsAsync(ClaimsPrincipal principal, Guid caseId, A4Query query, CancellationToken ct) =>
        WithCase(principal, caseId, query.Reason, "TRANSACTIONS_LIST", async (ticket, token) =>
        {
            Validate(query);
            var subjectId = ticket.LinkedUserId;

            // 1. Authoritative FinancialTransactions
            var ftQuery = db.FinancialTransactions.AsNoTracking()
                .Include(t => t.Wallet)
                .Include(t => t.LedgerTransaction)
                .Where(t => t.OwnerUserId == subjectId);

            if (query.FromUtc is { } from) ftQuery = ftQuery.Where(t => t.OccurredAt >= from);
            if (query.ToUtc is { } to) ftQuery = ftQuery.Where(t => t.OccurredAt < to);

            var ftItems = await ftQuery.Select(t => new A4TransactionItem
            {
                Id = t.Id,
                BusinessType = t.Type.ToString(),
                Status = t.Status.ToString(),
                Amount = t.Amount,
                Currency = t.Currency,
                OccurredAt = DateTime.SpecifyKind(t.OccurredAt, DateTimeKind.Utc),
                WalletId = t.WalletId,
                WalletName = t.Wallet != null ? t.Wallet.Name : null,
                LedgerTransactionId = t.LedgerTransactionId,
                LedgerStatus = t.LedgerTransaction != null ? t.LedgerTransaction.Status.ToString() : null,
                LedgerReference = t.LedgerTransaction != null ? t.LedgerTransaction.Reference : null,
                RelatedBusinessId = t.RelatedBusinessId,
                RelatedBusinessSource = t.RelatedBusinessId != null ? "LinkedBusinessEntity" : null,
                Counterparty = MaskSensitive(t.CounterpartyName),
                Description = MaskSensitive(t.Description)
            }).ToListAsync(token);

            var existingBusinessIds = ftItems
                .Where(t => t.RelatedBusinessId != null)
                .Select(t => t.RelatedBusinessId!.Value)
                .ToHashSet();

            var existingLedgerIds = ftItems
                .Where(t => t.LedgerTransactionId != null)
                .Select(t => t.LedgerTransactionId!.Value)
                .ToHashSet();

            var unifiedList = new List<A4TransactionItem>(ftItems);

            // 2. Legacy Group Contributions belonging to subject not in FinancialTransaction
            var groupContribs = await db.GroupContributions.AsNoTracking()
                .Where(c => c.UserId == subjectId && !existingBusinessIds.Contains(c.Id) && !existingLedgerIds.Contains(c.LedgerTransactionId))
                .ToListAsync(token);

            // 3. V1 Fund Contributions belonging to subject not in FinancialTransaction
            var v1Contribs = await db.V1FundContributions.AsNoTracking()
                .Where(c => c.UserId == subjectId && !existingBusinessIds.Contains(c.Id) && !existingLedgerIds.Contains(c.LedgerTransactionId))
                .ToListAsync(token);

            // Batch-fetch ledger transactions and wallets for all legacy contributions to eliminate N+1 queries
            var neededLedgerIds = groupContribs.Select(c => c.LedgerTransactionId)
                .Concat(v1Contribs.Select(c => c.LedgerTransactionId))
                .Distinct()
                .ToList();
            var neededWalletIds = groupContribs.Select(c => c.WalletId)
                .Concat(v1Contribs.Select(c => c.WalletId))
                .Distinct()
                .ToList();

            var ledgerMap = await db.LedgerTransactions.AsNoTracking()
                .Where(l => neededLedgerIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, token);
            var walletMap = await db.Wallets.AsNoTracking()
                .Where(w => neededWalletIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, token);

            foreach (var gc in groupContribs)
            {
                if (query.FromUtc is { } f && gc.CreatedAt < f) continue;
                if (query.ToUtc is { } t && gc.CreatedAt >= t) continue;

                ledgerMap.TryGetValue(gc.LedgerTransactionId, out var ledger);
                walletMap.TryGetValue(gc.WalletId, out var wallet);

                unifiedList.Add(new A4TransactionItem
                {
                    Id = gc.Id,
                    BusinessType = "GroupContribution",
                    Status = ledger?.Status == LedgerTransactionStatus.Completed ? "Completed" : "Recorded",
                    Amount = gc.Amount,
                    Currency = "VND",
                    OccurredAt = DateTime.SpecifyKind(gc.CreatedAt, DateTimeKind.Utc),
                    WalletId = gc.WalletId,
                    WalletName = wallet?.Name,
                    LedgerTransactionId = gc.LedgerTransactionId,
                    LedgerStatus = ledger?.Status.ToString(),
                    LedgerReference = ledger?.Reference,
                    RelatedBusinessId = gc.Id,
                    RelatedBusinessSource = "GroupContribution",
                    Counterparty = null,
                    Description = MaskSensitive(gc.Purpose)
                });
            }

            foreach (var vc in v1Contribs)
            {
                if (query.FromUtc is { } f && vc.CreatedAt < f) continue;
                if (query.ToUtc is { } t && vc.CreatedAt >= t) continue;

                ledgerMap.TryGetValue(vc.LedgerTransactionId, out var ledger);
                walletMap.TryGetValue(vc.WalletId, out var wallet);

                unifiedList.Add(new A4TransactionItem
                {
                    Id = vc.Id,
                    BusinessType = "V1FundContribution",
                    Status = ledger?.Status == LedgerTransactionStatus.Completed ? "Completed" : "Recorded",
                    Amount = vc.AmountVnd,
                    Currency = "VND",
                    OccurredAt = DateTime.SpecifyKind(vc.CreatedAt, DateTimeKind.Utc),
                    WalletId = vc.WalletId,
                    WalletName = wallet?.Name,
                    LedgerTransactionId = vc.LedgerTransactionId,
                    LedgerStatus = ledger?.Status.ToString(),
                    LedgerReference = ledger?.Reference,
                    RelatedBusinessId = vc.Id,
                    RelatedBusinessSource = "V1FundContribution",
                    Counterparty = null,
                    Description = MaskSensitive(vc.Purpose)
                });
            }

            // 4. Group Refunds belonging to subject not in FinancialTransaction
            var groupRefunds = await db.GroupRefunds.AsNoTracking()
                .Where(r => r.UserId == subjectId && !existingBusinessIds.Contains(r.Id))
                .ToListAsync(token);

            foreach (var gr in groupRefunds)
            {
                if (query.FromUtc is { } f && gr.CreatedAt < f) continue;
                if (query.ToUtc is { } t && gr.CreatedAt >= t) continue;

                unifiedList.Add(new A4TransactionItem
                {
                    Id = gr.Id,
                    BusinessType = "GroupRefund",
                    Status = gr.Status == GroupRefundStatus.Completed ? "Completed" : gr.Status.ToString(),
                    Amount = gr.RefundAmount,
                    Currency = "VND",
                    OccurredAt = DateTime.SpecifyKind(gr.CreatedAt, DateTimeKind.Utc),
                    WalletId = null,
                    WalletName = null,
                    LedgerTransactionId = null,
                    LedgerStatus = null,
                    LedgerReference = null,
                    RelatedBusinessId = gr.Id,
                    RelatedBusinessSource = "GroupRefund",
                    Counterparty = null,
                    Description = "Group liquidation refund"
                });
            }

            // Filter by Type if provided
            IEnumerable<A4TransactionItem> filtered = unifiedList;
            if (!string.IsNullOrWhiteSpace(query.Type))
            {
                filtered = filtered.Where(x => string.Equals(x.BusinessType, query.Type, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by Status if provided
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                filtered = filtered.Where(x => string.Equals(x.Status, query.Status, StringComparison.OrdinalIgnoreCase));
            }

            var ordered = filtered.OrderByDescending(t => t.OccurredAt).ThenBy(t => t.Id).ToList();
            var total = ordered.Count;
            var pagedItems = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

            return new A2Page<A4TransactionItem>(clock.UtcNow, query.Page, query.PageSize, total, pagedItems);
        }, ct);

    // A4.2 - Tra cứu mã giao dịch
    public Task<A4LookupResult> LookupAsync(ClaimsPrincipal principal, Guid caseId, string code, string reason, CancellationToken ct) =>
        WithCase(principal, caseId, reason, "TRANSACTION_LOOKUP", async (ticket, token) =>
        {
            var cleanCode = InputBoundary.RequiredText(code, 200, "Code").Trim();
            var subjectId = ticket.LinkedUserId;

            FinancialTransaction? tx = null;
            string matchedBy = "";

            if (Guid.TryParse(cleanCode, out var guid))
            {
                tx = await db.FinancialTransactions.AsNoTracking()
                    .Include(t => t.Wallet)
                    .Include(t => t.LedgerTransaction)
                    .SingleOrDefaultAsync(t => t.Id == guid && t.OwnerUserId == subjectId, token);
                if (tx != null) matchedBy = "FinancialTransactionId";

                if (tx == null)
                {
                    tx = await db.FinancialTransactions.AsNoTracking()
                        .Include(t => t.Wallet)
                        .Include(t => t.LedgerTransaction)
                        .SingleOrDefaultAsync(t => t.LedgerTransactionId == guid && t.OwnerUserId == subjectId, token);
                    if (tx != null) matchedBy = "LedgerTransactionId";
                }

                if (tx == null)
                {
                    tx = await db.FinancialTransactions.AsNoTracking()
                        .Include(t => t.Wallet)
                        .Include(t => t.LedgerTransaction)
                        .SingleOrDefaultAsync(t => t.RelatedBusinessId == guid && t.OwnerUserId == subjectId, token);
                    if (tx != null) matchedBy = "RelatedBusinessId";
                }
            }

            if (tx == null)
            {
                tx = await db.FinancialTransactions.AsNoTracking()
                    .Include(t => t.Wallet)
                    .Include(t => t.LedgerTransaction)
                    .SingleOrDefaultAsync(t => t.LedgerTransaction.Reference == cleanCode && t.OwnerUserId == subjectId, token);
                if (tx != null) matchedBy = "LedgerReference";
            }

            if (tx == null)
            {
                var topUp = await db.WalletTopUps.AsNoTracking()
                    .SingleOrDefaultAsync(u => (u.Reference == cleanCode || u.ProviderTransactionId == cleanCode) && u.UserId == subjectId, token);
                if (topUp?.FinancialTransactionId != null)
                {
                    tx = await db.FinancialTransactions.AsNoTracking()
                        .Include(t => t.Wallet)
                        .Include(t => t.LedgerTransaction)
                        .SingleOrDefaultAsync(t => t.Id == topUp.FinancialTransactionId && t.OwnerUserId == subjectId, token);
                    if (tx != null) matchedBy = "TopUpProviderReference";
                }
            }

            if (tx == null)
            {
                var bankTx = await db.BankTransactions.AsNoTracking()
                    .SingleOrDefaultAsync(b => (b.ExternalTransactionId == cleanCode || b.ReferenceCode == cleanCode || b.PaymentCode == cleanCode) && b.UserId == subjectId, token);
                if (bankTx != null)
                {
                    // Check if linked topup exists
                    var linkedTopUp = await db.WalletTopUps.AsNoTracking()
                        .SingleOrDefaultAsync(u => u.ProviderTransactionId == bankTx.ExternalTransactionId && u.UserId == subjectId, token);
                    if (linkedTopUp?.FinancialTransactionId != null)
                    {
                        tx = await db.FinancialTransactions.AsNoTracking()
                            .Include(t => t.Wallet)
                            .Include(t => t.LedgerTransaction)
                            .SingleOrDefaultAsync(t => t.Id == linkedTopUp.FinancialTransactionId && t.OwnerUserId == subjectId, token);
                        if (tx != null) matchedBy = "BankTransactionMatch";
                    }
                }
            }

            if (tx == null)
            {
                // Verify whether code belongs to another user (cross-user protection check)
                bool belongsToOther = false;
                if (Guid.TryParse(cleanCode, out var otherGuid))
                {
                    belongsToOther = await db.FinancialTransactions.AsNoTracking().AnyAsync(t => t.Id == otherGuid && t.OwnerUserId != subjectId, token)
                        || await db.FinancialTransactions.AsNoTracking().AnyAsync(t => t.LedgerTransactionId == otherGuid && t.OwnerUserId != subjectId, token)
                        || await db.FinancialTransactions.AsNoTracking().AnyAsync(t => t.RelatedBusinessId == otherGuid && t.OwnerUserId != subjectId, token);
                }
                if (!belongsToOther)
                {
                    belongsToOther = await db.FinancialTransactions.AsNoTracking().AnyAsync(t => t.LedgerTransaction.Reference == cleanCode && t.OwnerUserId != subjectId, token)
                        || await db.WalletTopUps.AsNoTracking().AnyAsync(u => (u.Reference == cleanCode || u.ProviderTransactionId == cleanCode) && u.UserId != subjectId, token);
                }

                if (belongsToOther)
                    throw new ForbiddenException("cross_user_transaction_denied", "Access to transaction outside case scope is forbidden.");

                throw new NotFoundException("transaction_not_found", "Transaction code not found for case subject.");
            }

            string? businessSource = null;
            if (tx.RelatedBusinessId is { } busId)
            {
                if (await db.V1FundContributions.AnyAsync(c => c.Id == busId, token)) businessSource = "V1FundContribution";
                else if (await db.GroupContributions.AnyAsync(c => c.Id == busId, token)) businessSource = "GroupContribution";
                else if (await db.Transfers.AnyAsync(tr => tr.Id == busId, token)) businessSource = "InternalTransfer";
                else if (await db.WalletTopUps.AnyAsync(wt => wt.Id == busId, token)) businessSource = "WalletTopUp";
                else if (await db.GroupExpenses.AnyAsync(ge => ge.Id == busId, token)) businessSource = "GroupExpense";
                else if (await db.GroupRefunds.AnyAsync(gr => gr.Id == busId, token)) businessSource = "GroupRefund";
                else businessSource = "StoredBusinessReference";
            }

            return new A4LookupResult
            {
                Id = tx.Id,
                QueryCode = cleanCode,
                MatchedBy = matchedBy,
                BusinessType = tx.Type.ToString(),
                Status = tx.Status.ToString(),
                Amount = tx.Amount,
                Currency = tx.Currency,
                OccurredAt = DateTime.SpecifyKind(tx.OccurredAt, DateTimeKind.Utc),
                WalletId = tx.WalletId,
                WalletName = tx.Wallet?.Name,
                WalletType = tx.Wallet?.Type.ToString(),
                LedgerTransactionId = tx.LedgerTransactionId,
                LedgerStatus = tx.LedgerTransaction?.Status.ToString(),
                LedgerReference = tx.LedgerTransaction?.Reference,
                RelatedBusinessId = tx.RelatedBusinessId,
                RelatedBusinessSource = businessSource,
                MaskedCounterparty = MaskSensitive(tx.CounterpartyName) ?? "NOT_SPECIFIED",
                MaskedDescription = MaskSensitive(tx.Description) ?? "NOT_SPECIFIED"
            };
        }, ct);

    // A4.3 - Ví người dùng
    public Task<A4UserWalletsResult> WalletsAsync(ClaimsPrincipal principal, Guid caseId, string reason, CancellationToken ct) =>
        WithCase(principal, caseId, reason, "USER_WALLETS", async (ticket, token) =>
        {
            var subjectId = ticket.LinkedUserId;

            // Load SpendWise wallets (Main + up to 2 secondary)
            var storedWallets = await db.Wallets.AsNoTracking()
                .Where(w => w.OwnerUserId == subjectId && w.Status != WalletStatus.Closed)
                .OrderBy(w => w.Type)
                .ThenBy(w => w.Id)
                .Take(4)
                .ToListAsync(token);

            if (storedWallets.Count > 3 || storedWallets.Count(w => w.Type == WalletType.Main) > 1 || storedWallets.Count(w => w.Type != WalletType.Main) > 2)
            {
                throw new ConflictException("wallet_structure_requires_review", "Stored wallet structure requires review; no money was changed.");
            }

            var walletItems = storedWallets.Select(w => new A4WalletItem(
                w.Id,
                w.Name,
                w.Type.ToString(),
                w.Status.ToString(),
                w.Currency,
                w.AvailableBalance,
                w.IsDefault,
                w.LedgerAccount?.Id,
                "READ_ONLY; Balance adjustments, state changes and postings are strictly prohibited."
            )).ToList();

            // Load Linked Bank connections (che số tài khoản và thông tin PII)
            var bankConnections = await db.BankConnections.AsNoTracking()
                .Where(b => b.UserId == subjectId && b.Status != BankConnectionStatus.Unlinked)
                .OrderByDescending(b => b.LinkedAt)
                .ToListAsync(token);

            var linkedBankItems = bankConnections.Select(b => new A4LinkedBankItem(
                b.Id,
                b.BankBrandName,
                MaskAccountNumber(b.MaskedAccountNumber),
                MaskSensitive(b.AccountHolderName),
                b.Status.ToString(),
                b.LinkedAt is { } la ? DateTime.SpecifyKind(la, DateTimeKind.Utc) : null
            )).ToList();

            return new A4UserWalletsResult(walletItems, linkedBankItems);
        }, ct);

    // A4.4 - Giao dịch chờ / lỗi
    public Task<A2Page<A4PendingFailedTransaction>> PendingAndFailedAsync(ClaimsPrincipal principal, Guid caseId, A4Query query, CancellationToken ct) =>
        WithCase(principal, caseId, query.Reason, "PENDING_FAILED_TRANSACTIONS", async (ticket, token) =>
        {
            Validate(query);
            var subjectId = ticket.LinkedUserId;

            var rows = db.FinancialTransactions.AsNoTracking()
                .Include(t => t.LedgerTransaction)
                .Where(t => t.OwnerUserId == subjectId
                    && (t.Status == FinancialTransactionStatus.Pending
                        || t.Status == FinancialTransactionStatus.Failed
                        || t.Status == FinancialTransactionStatus.Cancelled
                        || t.Status == FinancialTransactionStatus.Processing
                        || (t.LedgerTransaction != null && (t.LedgerTransaction.Status == LedgerTransactionStatus.Pending || t.LedgerTransaction.Status == LedgerTransactionStatus.Failed))));

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                if (!Enum.TryParse<FinancialTransactionStatus>(query.Status, out var statusVal) || !Enum.IsDefined(statusVal))
                    throw new AppException("invalid_transaction_status", "Invalid transaction status filter.");
                rows = rows.Where(t => t.Status == statusVal);
            }

            var total = await rows.CountAsync(token);
            var list = await rows
                .OrderByDescending(t => t.OccurredAt)
                .ThenBy(t => t.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(token);

            var resultItems = new List<A4PendingFailedTransaction>();

            foreach (var tx in list)
            {
                var retries = new List<A4RetryHistoryItem>();
                string? reasonStr = null;

                // Check TopUp provider webhook retry events
                if (tx.RelatedBusinessId is { } busId)
                {
                    var topUp = await db.WalletTopUps.AsNoTracking().SingleOrDefaultAsync(u => u.Id == busId, token);
                    if (topUp != null && !string.IsNullOrWhiteSpace(topUp.ProviderTransactionId))
                    {
                        var integrationEvents = await db.BankIntegrationEvents.AsNoTracking()
                            .Where(e => e.ExternalTransactionId == topUp.ProviderTransactionId)
                            .OrderBy(e => e.ReceivedAt)
                            .ToListAsync(token);

                        foreach (var evt in integrationEvents)
                        {
                            retries.Add(new A4RetryHistoryItem(
                                DateTime.SpecifyKind(evt.ReceivedAt, DateTimeKind.Utc),
                                evt.EventType,
                                evt.ProcessingStatus.ToString(),
                                $"DeliveryDuplicates: {evt.DuplicateDeliveryCount}, FailureCode: {evt.FailureCode ?? "None"}"
                            ));
                            if (!string.IsNullOrWhiteSpace(evt.FailureCode)) reasonStr = evt.FailureCode;
                        }
                    }
                }

                // Check corrections
                var corrections = await db.TransactionCorrections.AsNoTracking()
                    .Where(c => c.TransactionId == tx.Id)
                    .OrderBy(c => c.CorrectionNumber)
                    .ToListAsync(token);

                foreach (var c in corrections)
                {
                    retries.Add(new A4RetryHistoryItem(
                        DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc),
                        "TransactionCorrection",
                        $"Correction #{c.CorrectionNumber}",
                        c.Reason
                    ));
                }

                string note = tx.Status switch
                {
                    FinancialTransactionStatus.Pending => "Transaction is PENDING; do not deduce as failed. Retry payment execution without contract is prohibited.",
                    FinancialTransactionStatus.Processing => "Transaction is PROCESSING; waiting for provider reconciliation.",
                    FinancialTransactionStatus.Failed => "Transaction has FAILED; investigate cause. Provider errors must not be marked completed.",
                    FinancialTransactionStatus.Cancelled => "Transaction was CANCELLED.",
                    _ => "Requires investigation."
                };

                resultItems.Add(new A4PendingFailedTransaction(
                    tx.Id,
                    "FinancialTransaction",
                    tx.Type.ToString(),
                    tx.Status.ToString(),
                    tx.Amount,
                    tx.Currency,
                    DateTime.SpecifyKind(tx.CreatedAt, DateTimeKind.Utc),
                    tx.Status is FinancialTransactionStatus.Failed or FinancialTransactionStatus.Cancelled ? DateTime.SpecifyKind(tx.OccurredAt, DateTimeKind.Utc) : null,
                    reasonStr ?? (tx.Status == FinancialTransactionStatus.Failed ? "ExecutionFailed" : null),
                    note,
                    retries
                ));
            }

            return new A2Page<A4PendingFailedTransaction>(clock.UtcNow, query.Page, query.PageSize, total, resultItems);
        }, ct);

    // A4.5 - Chi tiết sổ cái
    public Task<A4LedgerDetailResult> LedgerDetailAsync(ClaimsPrincipal principal, Guid caseId, Guid ledgerTransactionId, string reason, CancellationToken ct) =>
        WithCase(principal, caseId, reason, "LEDGER_DETAIL", async (ticket, token) =>
        {
            var subjectId = ticket.LinkedUserId;

            // Kiểm tra xem ledgerTransaction này có thuộc phạm vi của case subject không
            var belongsToSubject = await db.FinancialTransactions.AsNoTracking()
                .AnyAsync(t => t.LedgerTransactionId == ledgerTransactionId && t.OwnerUserId == subjectId, token)
                || await db.Wallets.AsNoTracking().AnyAsync(w => w.OwnerUserId == subjectId && db.LedgerEntries.Any(e => e.LedgerTransactionId == ledgerTransactionId && e.LedgerAccountId == w.LedgerAccount.Id), token);

            if (!belongsToSubject)
            {
                var existsOther = await db.LedgerTransactions.AsNoTracking().AnyAsync(l => l.Id == ledgerTransactionId, token);
                if (existsOther)
                    throw new ForbiddenException("cross_user_ledger_denied", "Ledger transaction belongs to outside case subject.");
                throw new NotFoundException("ledger_transaction_not_found", "Ledger transaction not found.");
            }

            var ledger = await db.LedgerTransactions.AsNoTracking()
                .SingleAsync(l => l.Id == ledgerTransactionId, token);

            var rawEntries = await db.LedgerEntries.AsNoTracking()
                .Where(e => e.LedgerTransactionId == ledgerTransactionId)
                .Include(e => e.LedgerAccount)
                .OrderBy(e => e.Id)
                .ToListAsync(token);

            var debitTotal = rawEntries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
            var creditTotal = rawEntries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);

            // A4-R1: Multi-currency balance check
            var currencyGroups = rawEntries.GroupBy(e => e.Currency).ToList();
            bool hasEntries = rawEntries.Count > 0;
            bool allAmountsPositive = rawEntries.All(e => e.Amount > 0);
            bool accountsMatchCurrency = rawEntries.All(e => e.LedgerAccount == null || string.Equals(e.LedgerAccount.Currency, e.Currency, StringComparison.OrdinalIgnoreCase));
            bool eachCurrencyBalanced = currencyGroups.Count > 0 && currencyGroups.All(g =>
            {
                var d = g.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
                var c = g.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);
                return d == c && d > 0;
            });

            bool isBalanced = hasEntries && allAmountsPositive && accountsMatchCurrency && eachCurrencyBalanced;

            string balanceStatus;
            if (!hasEntries || (debitTotal == 0 && creditTotal == 0))
            {
                balanceStatus = "EMPTY_OR_ZERO";
            }
            else if (currencyGroups.Count > 1 && !eachCurrencyBalanced)
            {
                balanceStatus = "MULTICURRENCY_MISMATCH";
            }
            else if (!accountsMatchCurrency)
            {
                balanceStatus = "ACCOUNT_CURRENCY_MISMATCH";
            }
            else if (isBalanced)
            {
                balanceStatus = "BALANCED";
            }
            else
            {
                balanceStatus = "UNBALANCED_MISMATCH";
            }

            // A4-R2: Verify actual business binding
            string bindingStatus = "UNVERIFIED";
            if (ledger.BusinessType == "GroupContribution")
            {
                var contrib = await db.GroupContributions.AsNoTracking().SingleOrDefaultAsync(c => c.Id == ledger.BusinessId, token);
                if (contrib == null)
                    bindingStatus = "MISSING_ORIGINAL_BUSINESS";
                else if (contrib.LedgerTransactionId != ledger.Id || contrib.Amount != debitTotal || contrib.UserId != subjectId)
                    bindingStatus = "BUSINESS_BINDING_MISMATCH";
                else
                    bindingStatus = "BOUND_ORIGINAL_GROUP_CONTRIBUTION";
            }
            else if (ledger.BusinessType == "V1FundContribution")
            {
                var contrib = await db.V1FundContributions.AsNoTracking().SingleOrDefaultAsync(c => c.Id == ledger.BusinessId, token);
                if (contrib == null)
                    bindingStatus = "MISSING_ORIGINAL_BUSINESS";
                else if (contrib.LedgerTransactionId != ledger.Id || contrib.AmountVnd != debitTotal || contrib.UserId != subjectId)
                    bindingStatus = "BUSINESS_BINDING_MISMATCH";
                else
                    bindingStatus = "BOUND_ORIGINAL_V1_CONTRIBUTION";
            }
            else if (ledger.BusinessType == "GroupFundExpense")
            {
                var exp = await db.GroupExpenses.AsNoTracking().SingleOrDefaultAsync(e => e.Id == ledger.BusinessId, token);
                if (exp == null)
                    bindingStatus = "MISSING_ORIGINAL_BUSINESS";
                else if (exp.Amount != creditTotal || exp.Status != GroupExpenseStatus.Recorded)
                    bindingStatus = "BUSINESS_BINDING_MISMATCH";
                else
                    bindingStatus = "BOUND_ORIGINAL_GROUP_EXPENSE";
            }
            else if (ledger.BusinessType == "GroupRefund")
            {
                var refnd = await db.GroupRefunds.AsNoTracking().SingleOrDefaultAsync(r => r.Id == ledger.BusinessId, token);
                if (refnd == null)
                    bindingStatus = "MISSING_ORIGINAL_BUSINESS";
                else if (refnd.RefundAmount != debitTotal)
                    bindingStatus = "BUSINESS_BINDING_MISMATCH";
                else
                    bindingStatus = "BOUND_ORIGINAL_GROUP_REFUND";
            }
            else if (ledger.BusinessType == "WalletTopUp")
            {
                var topup = await db.WalletTopUps.AsNoTracking().SingleOrDefaultAsync(u => u.Id == ledger.BusinessId, token);
                if (topup == null)
                    bindingStatus = "MISSING_ORIGINAL_BUSINESS";
                else if (topup.Amount != debitTotal || topup.UserId != subjectId)
                    bindingStatus = "BUSINESS_BINDING_MISMATCH";
                else
                    bindingStatus = "BOUND_ORIGINAL_TOPUP";
            }
            else if (ledger.BusinessType is "PersonalExpense" or "PersonalIncome" or "InternalTransfer")
            {
                var ft = await db.FinancialTransactions.AsNoTracking().FirstOrDefaultAsync(t => t.LedgerTransactionId == ledger.Id && t.OwnerUserId == subjectId, token);
                if (ft == null)
                    bindingStatus = "UNVERIFIED";
                else if (ft.Amount != debitTotal && ft.Amount != creditTotal || ft.Status != FinancialTransactionStatus.Completed)
                    bindingStatus = "BUSINESS_BINDING_MISMATCH";
                else
                    bindingStatus = "BOUND_PERSONAL_FINANCE";
            }

            var entries = rawEntries.Select(e => new A4LedgerEntryDetail(
                e.Id,
                e.LedgerAccountId,
                e.LedgerAccount?.Name ?? "UNKNOWN",
                e.LedgerAccount?.Code ?? "UNKNOWN",
                e.LedgerAccount?.Type.ToString() ?? "UNKNOWN",
                e.Direction.ToString(),
                e.Amount,
                e.Currency,
                MaskSensitive(e.Memo) ?? ""
            )).ToList();

            return new A4LedgerDetailResult(
                ledger.Id,
                ledger.Reference,
                ledger.BusinessType,
                ledger.BusinessId,
                ledger.Status.ToString(),
                DateTime.SpecifyKind(ledger.CreatedAt, DateTimeKind.Utc),
                ledger.CompletedAt is { } cat ? DateTime.SpecifyKind(cat, DateTimeKind.Utc) : null,
                balanceStatus,
                debitTotal,
                creditTotal,
                isBalanced,
                bindingStatus,
                entries
            );
        }, ct);

    // A4.6 - Truy vết giao dịch
    public Task<A4TraceResult> TraceAsync(ClaimsPrincipal principal, Guid caseId, Guid transactionId, string reason, CancellationToken ct) =>
        WithCase(principal, caseId, reason, "TRANSACTION_TRACE", async (ticket, token) =>
        {
            var subjectId = ticket.LinkedUserId;

            var tx = await db.FinancialTransactions.AsNoTracking()
                .Include(t => t.Wallet)
                .Include(t => t.LedgerTransaction)
                .SingleOrDefaultAsync(t => t.Id == transactionId && t.OwnerUserId == subjectId, token);

            if (tx == null)
            {
                var existsOther = await db.FinancialTransactions.AsNoTracking().AnyAsync(t => t.Id == transactionId, token);
                if (existsOther)
                    throw new ForbiddenException("cross_user_trace_denied", "Access to transaction outside case scope is forbidden.");
                throw new NotFoundException("transaction_not_found", "Transaction not found for case subject.");
            }

            var timeline = new List<A4TraceTimelineItem>();
            int step = 1;

            // 1. Stage: Request
            timeline.Add(new A4TraceTimelineItem(
                step++,
                "RequestInitiation",
                DateTime.SpecifyKind(tx.CreatedAt, DateTimeKind.Utc),
                $"User:{subjectId}",
                "Initiated",
                tx.Id.ToString(),
                "FinancialTransaction",
                $"Type: {tx.Type}, Amount: {tx.Amount} {tx.Currency}"
            ));

            // 2. Stage: Authorization / PIN
            // Check if there are user activities or audit logs associated
            var authActivity = await db.V1UserActivities.AsNoTracking()
                .Where(a => a.UserId == subjectId && a.CreatedAt <= tx.CreatedAt.AddMinutes(5) && a.CreatedAt >= tx.CreatedAt.AddMinutes(-5))
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(token);

            timeline.Add(new A4TraceTimelineItem(
                step++,
                "AuthorizationAndSecurity",
                authActivity != null ? DateTime.SpecifyKind(authActivity.CreatedAt, DateTimeKind.Utc) : (DateTime?)null,
                $"User:{subjectId}",
                authActivity != null ? "Authenticated" : "NOT_RECORDED",
                authActivity?.Id.ToString() ?? "NOT_AVAILABLE",
                authActivity != null ? "V1UserActivity" : "NOT_RECORDED",
                "PIN and Session verified prior to execution (sensitive credentials omitted)"
            ));

            // 3. Stage: Processing / Business Record
            string processingDetails = "Direct execution";
            string busRef = "NOT_AVAILABLE";
            string busRecord = "None";

            if (tx.RelatedBusinessId is { } busId)
            {
                busRef = busId.ToString();
                if (await db.V1FundContributions.AnyAsync(c => c.Id == busId, token)) { busRecord = "V1FundContribution"; processingDetails = "Group fund contribution recorded"; }
                else if (await db.GroupContributions.AnyAsync(c => c.Id == busId, token)) { busRecord = "GroupContribution"; processingDetails = "Legacy group contribution recorded"; }
                else if (await db.WalletTopUps.AnyAsync(u => u.Id == busId, token)) { busRecord = "WalletTopUp"; processingDetails = "Bank Hub / SePay Wallet TopUp"; }
                else if (await db.Transfers.AnyAsync(tr => tr.Id == busId, token)) { busRecord = "Transfer"; processingDetails = "Internal wallet transfer"; }
            }

            timeline.Add(new A4TraceTimelineItem(
                step++,
                "BusinessProcessing",
                DateTime.SpecifyKind(tx.OccurredAt, DateTimeKind.Utc),
                "SpendWiseBackend",
                tx.Status.ToString(),
                busRef,
                busRecord,
                processingDetails
            ));

            // 4. Stage: Provider / Webhook Integration
            if (busRecord == "WalletTopUp" && tx.RelatedBusinessId is { } topUpId)
            {
                var topUp = await db.WalletTopUps.AsNoTracking().SingleOrDefaultAsync(u => u.Id == topUpId, token);
                if (topUp != null && !string.IsNullOrWhiteSpace(topUp.ProviderTransactionId))
                {
                    var integrationEvent = await db.BankIntegrationEvents.AsNoTracking()
                        .Where(e => e.ExternalTransactionId == topUp.ProviderTransactionId)
                        .OrderBy(e => e.ReceivedAt)
                        .FirstOrDefaultAsync(token);

                    timeline.Add(new A4TraceTimelineItem(
                        step++,
                        "ProviderWebhook",
                        integrationEvent != null ? DateTime.SpecifyKind(integrationEvent.ReceivedAt, DateTimeKind.Utc) : (DateTime?)null,
                        "SePayBankHub",
                        integrationEvent?.ProcessingStatus.ToString() ?? "NOT_RECORDED",
                        topUp.ProviderTransactionId,
                        "BankIntegrationEvent",
                        $"Provider transaction matched. Raw payload and headers securely omitted."
                    ));
                }
                else
                {
                    timeline.Add(new A4TraceTimelineItem(
                        step++,
                        "ProviderWebhook",
                        null,
                        "Provider",
                        "NOT_RECORDED",
                        "NOT_AVAILABLE",
                        "NOT_RECORDED",
                        "No provider webhook telemetry recorded"
                    ));
                }
            }

            // 5. Stage: Financial & Ledger Outcome
            if (tx.LedgerTransaction != null)
            {
                timeline.Add(new A4TraceTimelineItem(
                    step++,
                    "LedgerOutcome",
                    tx.LedgerTransaction.CompletedAt is { } cat ? DateTime.SpecifyKind(cat, DateTimeKind.Utc) : (DateTime?)null,
                    "LedgerEngine",
                    tx.LedgerTransaction.Status.ToString(),
                    tx.LedgerTransaction.Reference,
                    "LedgerTransaction",
                    $"Reference: {tx.LedgerTransaction.Reference}, Status: {tx.LedgerTransaction.Status}"
                ));
            }
            else
            {
                timeline.Add(new A4TraceTimelineItem(
                    step++,
                    "LedgerOutcome",
                    null,
                    "LedgerEngine",
                    "MISSING_LEDGER",
                    "NOT_AVAILABLE",
                    "NOT_RECORDED",
                    "No ledger transaction linked"
                ));
            }

            return new A4TraceResult(
                tx.Id,
                tx.Type.ToString(),
                tx.Status.ToString(),
                timeline
            );
        }, ct);

    private async Task<T> WithCase<T>(ClaimsPrincipal principal, Guid caseId, string reason, string dataset, Func<SupportCase, CancellationToken, Task<T>> read, CancellationToken ct)
    {
        try { AdminAuditWriter.RequireReason(InputBoundary.RequiredText(reason, 1000, "Reason")); }
        catch (AppException) { await Denied(principal, caseId, ct); throw; }

        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))
        {
            await Denied(principal, caseId, ct);
            throw new ForbiddenException("operations_read_denied", "A current operator session is required.");
        }

        return await workflow.WithCaseAsync(principal, caseId, reason, false, async (ticket, actor, token) =>
        {
            var result = await read(ticket, token);
            audit.Add(actor, "A4_" + dataset + "_READ", "SupportCase", caseId.ToString(), reason,
                new { SubjectUserId = ticket.LinkedUserId, Role = principal.FindFirst(ClaimTypes.Role)!.Value, Dataset = dataset, Outcome = "Allowed" });
            await db.SaveChangesAsync(token);
            return result;
        }, ct);
    }

    private async Task Denied(ClaimsPrincipal principal, Guid caseId, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actor) ? actor : null,
            Action = "A4_ACCESS_DENIED",
            TargetType = "SupportCase",
            TargetId = caseId.ToString(),
            Reason = "Investigation authorization or reason rejected"
        });
        await db.SaveChangesAsync(ct);
    }

    private static void Validate(A4Query q)
    {
        InputBoundary.Page(q.Page, q.PageSize);
        if (q.Page - 1 > int.MaxValue / q.PageSize) throw new AppException("invalid_pagination", "Page too large.");
        if (q.FromUtc is { } from && from.Kind != DateTimeKind.Utc || q.ToUtc is { } to && to.Kind != DateTimeKind.Utc
            || q.FromUtc >= q.ToUtc) throw new AppException("invalid_date_range", "Use increasing UTC half-open boundaries.");
    }

    private static string? MaskSensitive(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        if (input.Length <= 4) return "****";
        return string.Concat(input.AsSpan(0, 2), "****", input.AsSpan(input.Length - 2));
    }

    private static string MaskAccountNumber(string account)
    {
        if (string.IsNullOrWhiteSpace(account)) return "****";
        if (account.Length <= 4) return "****";
        return "****" + account[^4..];
    }
}
