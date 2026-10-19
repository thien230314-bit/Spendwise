using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Evidence;
using SpendWise.Application.Finance;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Transactions.Commands.UpdateTransaction;

// PATCH tương thích route cũ nhưng luôn đi qua workflow correction bất biến.
public sealed class UpdateTransactionCommandHandler(
    ISpendWiseDbContext db,
    ILedgerService ledger,
    FinancialSecurityGuard security,
    EvidenceSupport evidence) : IRequestHandler<UpdateTransactionCommand, TransactionDto>
{
    public async Task<TransactionDto> Handle(UpdateTransactionCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        var reason = InputBoundary.RequiredText(command.Request.Reason, 1000, "Correction reason");
        InputBoundary.OptionalText(command.Request.Description, 500, "Description");
        InputBoundary.OptionalText(command.Request.Counterparty, 160, "Counterparty");

        return await db.ExecuteInTransactionAsync(async token =>
        {
            var transaction = await db.FinancialTransactions
                .Include(item => item.Wallet).ThenInclude(wallet => wallet!.LedgerAccount)
                .Include(item => item.WalletAllocations).ThenInclude(item => item.Wallet)
                    .ThenInclude(wallet => wallet.LedgerAccount)
                .Include(item => item.Category)
                .Include(item => item.Evidence)
                .Include(item => item.Corrections).ThenInclude(item => item.Evidence)
                .Include(item => item.Corrections).ThenInclude(item => item.WalletAllocations)
                    .ThenInclude(item => item.Wallet)
                .SingleOrDefaultAsync(item => item.Id == command.TransactionId
                    && item.OwnerUserId == command.UserId, token)
                ?? throw new NotFoundException("transaction_not_found", "Transaction not found.");

            if (!transaction.IsManual
                || transaction.Status != FinancialTransactionStatus.Completed
                || transaction.Type is not (FinancialTransactionType.Income or FinancialTransactionType.Expense))
            {
                throw new ConflictException(
                    "immutable_financial_transaction",
                    "Finalized transaction can only be changed through an eligible manual correction.");
            }
            if (transaction.CorrectionCount >= transaction.CorrectionAllowance)
            {
                throw new ConflictException(
                    "CORRECTION_ALLOWANCE_EXHAUSTED",
                    "No direct transaction correction allowance remains.");
            }

            var amount = command.Request.Amount ?? transaction.Amount;
            InputBoundary.PositiveMoney(amount);
            var categoryId = command.Request.CategoryId ?? transaction.CategoryId
                ?? throw new AppException("invalid_category", "Category is required.");
            var occurredAt = command.Request.OccurredAt?.ToUniversalTime() ?? transaction.OccurredAt;
            var description = command.Request.Description?.Trim() ?? transaction.Description;
            var counterparty = command.Request.Counterparty is null
                ? transaction.CounterpartyName
                : command.Request.Counterparty.Trim();
            var expectedKind = transaction.Type == FinancialTransactionType.Income
                ? CategoryKind.Income
                : CategoryKind.Expense;

            var originalAllocations = CurrentAllocations(transaction);
            var correctedAllocations = await ResolveCorrectedAllocationsAsync(
                command, transaction, amount, originalAllocations, token);
            var correctedCategory = categoryId == transaction.CategoryId
                ? transaction.Category
                : await db.Categories.SingleOrDefaultAsync(
                    category => category.Id == categoryId
                        && category.Kind == expectedKind
                        && category.IsActive
                        && (category.IsSystem || category.OwnerUserId == command.UserId), token);
            if (correctedCategory is null || correctedCategory.Kind != expectedKind)
            {
                throw new AppException("invalid_category", "Correction must preserve Income/Expense semantics.");
            }

            var attachments = await evidence.LoadOwnedAsync(
                command.UserId, command.Request.EvidenceIds, required: false,
                requireUnlinked: true, cancellationToken: token);
            var originalSingle = originalAllocations.Count == 1 ? originalAllocations[0].Wallet : null;
            var correctedSingle = correctedAllocations.Count == 1 ? correctedAllocations[0].Wallet : null;
            var correction = new TransactionCorrection
            {
                TransactionId = transaction.Id,
                ActorUserId = command.UserId,
                CorrectionNumber = transaction.CorrectionCount + 1,
                Reason = reason,
                TransactionType = transaction.Type,
                OriginalCategoryId = transaction.CategoryId,
                OriginalWalletId = originalSingle?.Id,
                CorrectedWalletId = correctedSingle?.Id,
                OriginalAmount = transaction.Amount,
                CorrectedAmount = amount,
                OriginalCounterpartyName = transaction.CounterpartyName,
                CorrectedCounterpartyName = counterparty,
                OriginalOccurredAt = transaction.OccurredAt,
                CorrectedOccurredAt = occurredAt,
                CorrectedCategoryId = correctedCategory.Id,
                CorrectedCategory = correctedCategory,
                OriginalDescription = transaction.Description,
                CorrectedDescription = description,
                OriginalLedgerTransactionId = transaction.LedgerTransactionId,
                OriginalValuesJson = Snapshot(transaction.Amount, transaction.CategoryId,
                    transaction.Category?.Name, transaction.Description, transaction.CounterpartyName,
                    transaction.OccurredAt, transaction.Type, originalAllocations),
                CorrectedValuesJson = Snapshot(amount, correctedCategory.Id, correctedCategory.Name,
                    description, counterparty, occurredAt, transaction.Type, correctedAllocations)
            };
            AddCorrectionAllocations(correction, originalAllocations, true);
            AddCorrectionAllocations(correction, correctedAllocations, false);

            // Đảo toàn bộ entry của posting hiện hành, kể cả khi có nhiều ví nguồn.
            var reversal = await ledger.ReverseAsync(
                transaction.LedgerTransactionId, correction.Id,
                $"correction-reversal:{correction.Id:N}", reason, token);
            correction.ReversalLedgerTransactionId = reversal.LedgerTransactionId;

            var clearingType = expectedKind == CategoryKind.Income
                ? LedgerAccountType.ExternalSource
                : LedgerAccountType.ExpenseClearing;
            var clearing = await GetSystemAccountAsync(clearingType, token);
            LedgerPostingResult correctedPosting;
            if (expectedKind == CategoryKind.Income)
            {
                correctedPosting = await ledger.PostAsync(
                    clearing.Id, correctedAllocations[0].Wallet.LedgerAccount.Id, amount,
                    transaction.Currency, "TransactionCorrection", correction.Id,
                    $"transaction:{transaction.Id:N}:correction", correction.Id.ToString("N"),
                    description, token);
            }
            else
            {
                var entries = correctedAllocations.Select(allocation => new LedgerPostingEntry(
                        allocation.Wallet.LedgerAccount.Id, LedgerEntryDirection.Credit, allocation.Amount))
                    .Append(new LedgerPostingEntry(clearing.Id, LedgerEntryDirection.Debit, amount))
                    .ToList();
                correctedPosting = await ledger.PostEntriesAsync(
                    entries, transaction.Currency, "TransactionCorrection", correction.Id,
                    $"transaction:{transaction.Id:N}:correction", correction.Id.ToString("N"),
                    description, token);
            }
            correction.CorrectedLedgerTransactionId = correctedPosting.LedgerTransactionId;

            // Chỉ thay effective allocation sau khi reversal và posting mới đều thành công.
            var replacedAllocations = transaction.WalletAllocations.ToList();
            db.TransactionWalletAllocations.RemoveRange(replacedAllocations);
            if (expectedKind == CategoryKind.Expense)
            {
                foreach (var allocation in correctedAllocations)
                {
                    db.TransactionWalletAllocations.Add(new TransactionWalletAllocation
                    {
                        FinancialTransactionId = transaction.Id,
                        WalletId = allocation.Wallet.Id,
                        Wallet = allocation.Wallet,
                        Amount = allocation.Amount
                    });
                }
            }
            transaction.WalletId = correctedSingle?.Id;
            transaction.Wallet = correctedSingle;
            transaction.CategoryId = correctedCategory.Id;
            transaction.Category = correctedCategory;
            transaction.Amount = amount;
            transaction.Description = description;
            transaction.CounterpartyName = counterparty;
            transaction.OccurredAt = occurredAt;
            transaction.LedgerTransactionId = correctedPosting.LedgerTransactionId;
            transaction.CorrectionCount++;
            transaction.Corrections.Add(correction);
            db.TransactionCorrections.Add(correction);
            foreach (var attachment in attachments)
            {
                attachment.TransactionCorrectionId = correction.Id;
                correction.Evidence.Add(attachment);
            }

            db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = command.UserId,
                Action = "CORRECT_MANUAL_TRANSACTION",
                TargetType = "FinancialTransaction",
                TargetId = transaction.Id.ToString(),
                Reason = reason,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    correction.CorrectionNumber,
                    correction.OriginalLedgerTransactionId,
                    correction.ReversalLedgerTransactionId,
                    correction.CorrectedLedgerTransactionId
                })
            });
            await db.SaveChangesAsync(token);

            // Nạp lại effective view để collection không còn các allocation vừa được thay thế.
            db.ClearTrackedEntities();
            var updated = await db.FinancialTransactions.AsNoTracking()
                .Include(item => item.Wallet)
                .Include(item => item.Category)
                .Include(item => item.Evidence)
                .Include(item => item.WalletAllocations).ThenInclude(item => item.Wallet)
                .Include(item => item.Corrections).ThenInclude(item => item.Evidence)
                .Include(item => item.Corrections).ThenInclude(item => item.WalletAllocations)
                    .ThenInclude(item => item.Wallet)
                .SingleAsync(item => item.Id == transaction.Id, token);
            return TransactionMappings.ToDto(updated);
        }, cancellationToken);
    }

    private async Task<List<ResolvedAllocation>> ResolveCorrectedAllocationsAsync(
        UpdateTransactionCommand command,
        FinancialTransaction transaction,
        decimal amount,
        IReadOnlyList<ResolvedAllocation> originalAllocations,
        CancellationToken token)
    {
        if (transaction.Type == FinancialTransactionType.Income)
        {
            if (command.Request.WalletAllocations is not null)
                throw new AppException("invalid_wallet_allocations", "Wallet allocations apply only to expenses.");
            var walletId = command.Request.WalletId ?? transaction.WalletId
                ?? throw new AppException("wallet_required", "A wallet is required.");
            var wallet = await LoadActiveWalletAsync(command.UserId, walletId, token);
            return [new ResolvedAllocation(wallet, amount)];
        }

        IReadOnlyList<WalletAllocationRequest> requested;
        if (command.Request.WalletAllocations is not null)
        {
            if (command.Request.WalletId is not null)
                throw new AppException("ambiguous_wallet_selection", "Use WalletId or WalletAllocations, not both.");
            requested = command.Request.WalletAllocations;
        }
        else if (command.Request.WalletId is Guid requestedWalletId)
        {
            requested = [new WalletAllocationRequest(requestedWalletId, amount)];
        }
        else if (originalAllocations.Count > 1)
        {
            if (amount != transaction.Amount)
                throw new AppException("wallet_allocations_required",
                    "Changing a combined expense amount requires explicit wallet allocations.");
            requested = originalAllocations
                .Select(item => new WalletAllocationRequest(item.Wallet.Id, item.Amount)).ToList();
        }
        else
        {
            requested = [new WalletAllocationRequest(originalAllocations[0].Wallet.Id, amount)];
        }

        ValidateAllocations(requested, amount);
        var ids = requested.Select(item => item.WalletId).ToList();
        var wallets = await db.Wallets.Include(item => item.LedgerAccount)
            .Where(item => item.OwnerUserId == command.UserId && ids.Contains(item.Id))
            .OrderBy(item => item.Id).ToListAsync(token);
        if (wallets.Count != ids.Count)
            throw new NotFoundException("wallet_not_found", "One or more wallets were not found.");
        if (wallets.Any(wallet => wallet.Status != WalletStatus.Active))
            throw new ConflictException("wallet_unavailable", "Every selected wallet must be active.");
        return requested.Select(item => new ResolvedAllocation(
            wallets.Single(wallet => wallet.Id == item.WalletId), item.Amount)).ToList();
    }

    private async Task<Wallet> LoadActiveWalletAsync(Guid userId, Guid walletId, CancellationToken token)
    {
        var wallet = await db.Wallets.Include(item => item.LedgerAccount)
            .SingleOrDefaultAsync(item => item.Id == walletId && item.OwnerUserId == userId, token)
            ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");
        if (wallet.Status != WalletStatus.Active)
            throw new ConflictException("wallet_unavailable", $"Wallet is {wallet.Status}.");
        return wallet;
    }

    private static List<ResolvedAllocation> CurrentAllocations(FinancialTransaction transaction)
    {
        if (transaction.WalletAllocations.Count > 0)
            return transaction.WalletAllocations
                .Select(item => new ResolvedAllocation(item.Wallet, item.Amount)).ToList();
        if (transaction.Wallet is null)
            throw new ConflictException("transaction_allocation_missing", "Transaction wallet allocation is missing.");
        return [new ResolvedAllocation(transaction.Wallet, transaction.Amount)];
    }

    private static void ValidateAllocations(IReadOnlyList<WalletAllocationRequest> allocations, decimal total)
    {
        if (allocations.Count is < 1 or > 3)
            throw new AppException("invalid_wallet_allocations", "Between one and three wallet allocations are required.");
        foreach (var allocation in allocations)
            InputBoundary.PositiveMoney(allocation.Amount, "Wallet allocation amount");
        if (allocations.Select(item => item.WalletId).Distinct().Count() != allocations.Count)
            throw new AppException("duplicate_wallet_allocation", "Each wallet can appear only once.");
        if (allocations.Sum(item => item.Amount) != total)
            throw new AppException("allocation_sum_mismatch", "Wallet allocations must equal the total amount.");
    }

    private static void AddCorrectionAllocations(
        TransactionCorrection correction,
        IEnumerable<ResolvedAllocation> allocations,
        bool isOriginal)
    {
        foreach (var allocation in allocations)
        {
            correction.WalletAllocations.Add(new TransactionCorrectionWalletAllocation
            {
                IsOriginal = isOriginal,
                WalletId = allocation.Wallet.Id,
                Wallet = allocation.Wallet,
                Amount = allocation.Amount
            });
        }
    }

    private async Task<LedgerAccount> GetSystemAccountAsync(LedgerAccountType type, CancellationToken token)
    {
        var code = $"SYSTEM:{type}:VND";
        return await db.LedgerAccounts.SingleOrDefaultAsync(account => account.Code == code, token)
            ?? throw new NotFoundException("ledger_account_not_found", "Required system ledger account not found.");
    }

    private static string Snapshot(
        decimal amount,
        Guid? categoryId,
        string? categoryName,
        string description,
        string? counterparty,
        DateTime occurredAt,
        FinancialTransactionType type,
        IReadOnlyList<ResolvedAllocation> allocations) => JsonSerializer.Serialize(new
        {
            amount,
            walletId = allocations.Count == 1 ? allocations[0].Wallet.Id : (Guid?)null,
            walletName = allocations.Count == 1 ? allocations[0].Wallet.Name : null,
            walletAllocations = allocations.Select(item => new
            {
                walletId = item.Wallet.Id,
                walletName = item.Wallet.Name,
                item.Amount
            }),
            categoryId,
            categoryName,
            description,
            counterparty,
            occurredAt,
            type = type.ToString()
        });

    private sealed record ResolvedAllocation(Wallet Wallet, decimal Amount);
}
