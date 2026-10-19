using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Transactions.Queries.GetTransactionDetails;

// Handler lấy một giao dịch mà người dùng có quyền xem.

public sealed class GetTransactionDetailsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetTransactionDetailsQuery, TransactionDto>
{
    public async Task<TransactionDto> Handle(
        GetTransactionDetailsQuery query,
        CancellationToken cancellationToken)
    {
        // Tìm giao dịch theo Id và OwnerUserId trước khi trả chi tiết.
        var transaction = await db.FinancialTransactions
            .AsNoTracking()
            .Include(item => item.Wallet)
            .Include(item => item.Category)
            .Include(item => item.Evidence)
            .Include(item => item.Corrections)
                .ThenInclude(item => item.Evidence)
            .Include(item => item.Corrections)
                .ThenInclude(item => item.WalletAllocations)
                    .ThenInclude(item => item.Wallet)
            .Include(item => item.WalletAllocations)
                .ThenInclude(item => item.Wallet)
            .SingleOrDefaultAsync(item => item.Id == query.TransactionId
                && item.OwnerUserId == query.UserId, cancellationToken)
            ?? throw new NotFoundException(
                "transaction_not_found",
                "Transaction not found.");

        // Nạp tên/vai trò người sửa và lịch sử cấp quyền từ đúng SupportCase của chủ giao dịch.
        var actorIds = transaction.Corrections.Select(item => item.ActorUserId).ToList();
        var grants = await db.SupportCases.AsNoTracking()
            .Where(item => item.LinkedUserId == query.UserId
                && item.LinkedTransactionId == transaction.Id
                && item.CorrectionGranted)
            .ToListAsync(cancellationToken);
        actorIds.AddRange(grants.Where(item => item.CorrectionGrantedByAdminId != null)
            .Select(item => item.CorrectionGrantedByAdminId!.Value));
        var actors = await db.Users.AsNoTracking()
            .Where(item => actorIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var grantDtos = grants.Select(item => new TransactionCorrectionGrantDto(
            item.Id,
            item.CorrectionGrantedByAdminId ?? Guid.Empty,
            item.CorrectionGrantedByAdminId is Guid adminId && actors.TryGetValue(adminId, out var admin) ? admin.FullName : string.Empty,
            item.Id,
            item.CorrectionGrantReason ?? string.Empty,
            item.CorrectionGrantedAt ?? item.CreatedAt)).ToList();

        // Nạp tên danh mục/ví của cả snapshot trước và sau để frontend không phải hiển thị GUID thô.
        var categoryIds = transaction.Corrections
            .SelectMany(item => new[] { item.OriginalCategoryId, item.CorrectedCategoryId })
            .Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToList();
        var walletIds = transaction.Corrections
            .SelectMany(item => new[] { item.OriginalWalletId, item.CorrectedWalletId }).Distinct().ToList();
        var categoryNames = await db.Categories.AsNoTracking().Where(item => categoryIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var concreteWalletIds = walletIds.Where(item => item.HasValue).Select(item => item!.Value).ToList();
        var walletNames = await db.Wallets.AsNoTracking().Where(item => concreteWalletIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        return TransactionMappings.ToDto(transaction, actors, grantDtos, categoryNames, walletNames);
    }
}
