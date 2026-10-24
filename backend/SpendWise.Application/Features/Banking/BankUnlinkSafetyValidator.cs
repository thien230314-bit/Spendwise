using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Banking;

public interface IBankUnlinkSafetyValidator
{
    Task ValidateCanUnlinkAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class BankUnlinkSafetyValidator(ISpendWiseDbContext db) : IBankUnlinkSafetyValidator
{
    public async Task ValidateCanUnlinkAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // 1. Tất cả ví phải có số dư chính xác 0 VND (cả AvailableBalance và LedgerAccount.Balance)
        var wallets = await db.Wallets
            .Include(w => w.LedgerAccount)
            .Where(w => w.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        if (wallets.Any(w => w.AvailableBalance != 0 || (w.LedgerAccount != null && w.LedgerAccount.Balance != 0)))
        {
            throw new ConflictException(
                "unlink_balance_not_zero",
                "Tất cả ví phải có số dư chính xác 0 VND trước khi hủy liên kết ngân hàng.");
        }

        // 2. Không có yêu cầu nạp tiền (Top-up) đang ở trạng thái Pending hoặc Processing
        var hasActiveTopUp = await db.WalletTopUps.AsNoTracking().AnyAsync(t =>
            t.UserId == userId && (t.Status == WalletTopUpStatus.Pending || t.Status == WalletTopUpStatus.Processing),
            cancellationToken);
        if (hasActiveTopUp)
        {
            throw new ConflictException(
                "unlink_pending_topup_exists",
                "Không thể hủy liên kết khi đang có yêu cầu nạp tiền chưa hoàn tất.");
        }

        // 3. Không có yêu cầu rút tiền (Withdrawal) đang ở trạng thái Pending, Reserved, Processing hoặc PendingReconciliation
        var hasActiveWithdrawal = await db.WalletWithdrawals.AsNoTracking().AnyAsync(w =>
            w.UserId == userId && (w.Status == WalletWithdrawalStatus.Pending
                || w.Status == WalletWithdrawalStatus.Reserved
                || w.Status == WalletWithdrawalStatus.Processing
                || w.Status == WalletWithdrawalStatus.PendingReconciliation),
            cancellationToken);
        if (hasActiveWithdrawal)
        {
            throw new ConflictException(
                "unlink_pending_withdrawal_exists",
                "Không thể hủy liên kết khi đang có yêu cầu rút tiền đang chờ xử lý.");
        }

        // 4. Không có giao dịch chuyển tiền (Transfer) đang xử lý
        var hasActiveTransfer = await db.Transfers.AsNoTracking().AnyAsync(t =>
            (t.SenderUserId == userId || t.RecipientUserId == userId) && t.Status == TransferStatus.Processing,
            cancellationToken);
        if (hasActiveTransfer)
        {
            throw new ConflictException(
                "unlink_pending_transfer_exists",
                "Không thể hủy liên kết khi đang có giao dịch chuyển tiền đang xử lý.");
        }

        // 5. Không còn yêu cầu chi/rút quỹ chưa hoàn tất liên quan đến người dùng
        var hasActiveFundRequest = await db.V1FundRequests.AsNoTracking().AnyAsync(r =>
            (r.RecipientUserId == userId || r.RequestedByUserId == userId)
            && r.State != "Completed" && r.State != "Cancelled" && r.State != "Rejected",
            cancellationToken);
        if (hasActiveFundRequest)
        {
            throw new ConflictException(
                "unlink_fund_entitlement_exists",
                "Không thể hủy liên kết khi còn yêu cầu quỹ hoặc nghĩa vụ tài chính chưa tất toán.");
        }

        // 6. Không thể hủy liên kết nếu người dùng vẫn đang là thành viên của quỹ nhóm chưa giải thể
        var hasActiveFundMembership = await db.V1FundMemberships.AsNoTracking().AnyAsync(m =>
            m.UserId == userId && m.LeftAt == null,
            cancellationToken);
        if (hasActiveFundMembership)
        {
            // Kiểm tra xem quỹ đó đã giải thể chưa
            var activeFunds = await (from m in db.V1FundMemberships
                                     join f in db.V1Funds on m.FundId equals f.Id
                                     where m.UserId == userId && m.LeftAt == null && f.DissolvedAt == null
                                     select f.Id).AnyAsync(cancellationToken);
            if (activeFunds)
            {
                throw new ConflictException(
                    "unlink_fund_entitlement_exists",
                    "Người dùng cần rời khỏi hoặc tất toán toàn bộ quỹ nhóm đang tham gia trước khi hủy liên kết.");
            }
        }
    }
}
