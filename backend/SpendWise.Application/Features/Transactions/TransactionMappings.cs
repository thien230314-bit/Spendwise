using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Transactions;

// Lớp này chuyển lịch sử tài chính sang DTO với dấu tiền phù hợp từng loại giao dịch.

internal static class TransactionMappings
{
    public static TransactionDto ToDto(FinancialTransaction transaction,
        IReadOnlyDictionary<Guid, User>? actors = null,
        IReadOnlyList<TransactionCorrectionGrantDto>? grants = null,
        IReadOnlyDictionary<Guid, string>? categoryNames = null,
        IReadOnlyDictionary<Guid, string>? walletNames = null)
    {
        // Xác định dấu tiền theo loại giao dịch trước khi tạo TransactionDto.
        var hasPositiveSign = transaction.Type is FinancialTransactionType.Income
            or FinancialTransactionType.TransferIn
            or FinancialTransactionType.Deposit
            or FinancialTransactionType.GoalWithdrawal
            or FinancialTransactionType.Refund
            or FinancialTransactionType.BankTopUp;

        var allocations = transaction.WalletAllocations.Count > 0
            ? transaction.WalletAllocations
                .OrderBy(item => item.WalletId)
                .Select(item => new WalletAllocationDto(item.WalletId, item.Wallet.Name, item.Amount))
                .ToList()
            : transaction.WalletId is Guid singleWalletId && transaction.Wallet is not null
                ? [new WalletAllocationDto(singleWalletId, transaction.Wallet.Name, transaction.Amount)]
                : [];

        return new TransactionDto(
            transaction.Id,
            transaction.WalletId,
            transaction.Wallet?.Name,
            transaction.CategoryId,
            transaction.Category?.Name,
            transaction.Type.ToString(),
            transaction.Status.ToString(),
            transaction.Amount,
            hasPositiveSign ? transaction.Amount : -transaction.Amount,
            transaction.Currency,
            transaction.Description,
            transaction.CounterpartyName,
            transaction.OccurredAt,
            transaction.LedgerTransactionId,
            transaction.RelatedBusinessId,
            transaction.CorrectionAllowance,
            transaction.CorrectionCount,
            transaction.IsManual && transaction.CorrectionCount < transaction.CorrectionAllowance,
            transaction.Corrections
                .OrderBy(item => item.CorrectionNumber)
                .Select(item => new TransactionCorrectionDto(
                    item.Id,
                    item.CorrectionNumber,
                    item.Reason,
                    item.ActorUserId,
                    item.OriginalValuesJson,
                    item.CorrectedValuesJson,
                    item.OriginalLedgerTransactionId,
                    item.ReversalLedgerTransactionId,
                    item.CorrectedLedgerTransactionId,
                    item.CreatedAt,
                    item.Evidence.Select(evidence => new EvidenceDto(
                        evidence.Id,
                        evidence.OriginalFileName,
                        evidence.ContentType,
                        evidence.Length,
                        evidence.ContentHash,
                        evidence.CreatedAt)).ToList(),
                    actors?.GetValueOrDefault(item.ActorUserId)?.SystemRole.ToString() ?? "User",
                    actors?.GetValueOrDefault(item.ActorUserId)?.FullName ?? string.Empty,
                    new CorrectionSnapshotDto(item.OriginalAmount,
                        item.OriginalCategoryId is Guid originalCategoryId ? categoryNames?.GetValueOrDefault(originalCategoryId) : null,
                        item.OriginalDescription, item.OriginalWalletId,
                        item.OriginalWalletId is Guid originalWalletId
                            ? walletNames?.GetValueOrDefault(originalWalletId) ?? transaction.Wallet?.Name
                            : null,
                        item.OriginalOccurredAt, item.OriginalCounterpartyName,
                        item.WalletAllocations.Where(allocation => allocation.IsOriginal)
                            .OrderBy(allocation => allocation.WalletId)
                            .Select(allocation => new WalletAllocationDto(
                                allocation.WalletId,
                                allocation.Wallet.Name,
                                allocation.Amount)).ToList()),
                    new CorrectionSnapshotDto(item.CorrectedAmount,
                        item.CorrectedCategoryId is Guid correctedCategoryId ? categoryNames?.GetValueOrDefault(correctedCategoryId) : null,
                        item.CorrectedDescription, item.CorrectedWalletId,
                        item.CorrectedWalletId is Guid correctedWalletId
                            ? walletNames?.GetValueOrDefault(correctedWalletId) ?? transaction.Wallet?.Name
                            : null,
                        item.CorrectedOccurredAt, item.CorrectedCounterpartyName,
                        item.WalletAllocations.Where(allocation => !allocation.IsOriginal)
                            .OrderBy(allocation => allocation.WalletId)
                            .Select(allocation => new WalletAllocationDto(
                                allocation.WalletId,
                                allocation.Wallet.Name,
                                allocation.Amount)).ToList())))
                .ToList(),
            transaction.Evidence
                .Select(evidence => new EvidenceDto(
                    evidence.Id,
                    evidence.OriginalFileName,
                    evidence.ContentType,
                    evidence.Length,
                    evidence.ContentHash,
                    evidence.CreatedAt))
                .ToList(),
            grants,
            allocations);
    }
}
