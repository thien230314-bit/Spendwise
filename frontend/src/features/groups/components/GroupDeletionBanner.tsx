import React, { useState, useRef } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { Group, GroupDeletionRefundPlan } from '../../../types/groups';
import {
  AlertTriangle,
  CheckCircle2,
  XCircle,
  Clock,
  Ban,
  ShieldCheck,
  Wallet,
  ArrowRight,
  AlertCircle,
  Loader2,
  Check,
} from 'lucide-react';
import { cn } from '../../../utils/cn';
import { useGroup } from '../../../providers/GroupProvider';
import { useFinancial } from '../../../providers/FinancialProvider';
import { useSecurityPinChallenge } from '../../../providers/SecurityPinChallengeProvider';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';

interface GroupDeletionBannerProps {
  group: Group;
  currentUserId?: string;
  isOwner: boolean;
  onVote: (decision: 'Approve' | 'Reject') => void;
  onCancel: () => void;
  onFinalize?: () => void | Promise<void>;
  className?: string;
}

export const GroupDeletionBanner: React.FC<GroupDeletionBannerProps> = ({
  group,
  currentUserId,
  isOwner,
  onVote,
  onCancel,
  onFinalize,
  className,
}) => {
  const { t, locale, formatCurrency } = useI18n();
  const { challengePinWithValue } = useSecurityPinChallenge();
  const { executeRefund, finalizeGroupDeletionAndRefund } = useGroup();
  const { wallets } = useFinancial();
  const request = group.deletionRequest;

  const [showConfirmModal, setShowConfirmModal] = useState(false);
  const [isFinalizing, setIsFinalizing] = useState(false);
  const [finalizeError, setFinalizeError] = useState<string | null>(null);
  const [refundingItemId, setRefundingItemId] = useState<string | null>(null);
  const [refundActionError, setRefundActionError] = useState<string | null>(null);
  const [selectedWalletId, setSelectedWalletId] = useState<string>('');

  if (!request) return null;
  if (request.status === 'Cancelled') return null;

  const totalVotes = request.memberVotes.length;
  const approvedCount = request.memberVotes.filter((v) => v.status === 'Approved').length;
  const myVote = request.memberVotes.find((v) => v.userId === currentUserId);
  const canCurrentUserVote = request.status === 'Pending' && myVote?.status === 'Pending';

  const refundPlan = request.refundPlan;
  const hasRefundPlan = Boolean(refundPlan);
  const memberRefunds = refundPlan?.memberRefunds || [];
  const pendingMemberRefunds = memberRefunds.filter(
    (m) => m.refundAmount > 0 && m.settlementStatus !== 'completed'
  );
  const allRefundsCompleted = hasRefundPlan && pendingMemberRefunds.length === 0;

  // Active wallets for refund destination
  const activeWallets = (wallets || []).filter((w) => Boolean(w && w.id));
  const defaultWallet =
    activeWallets.find((w) => w.isDefault || w.type === 'spending' || (w as any).type === 'main') ||
    activeWallets[0];
  const effectiveWalletId =
    selectedWalletId && activeWallets.some((w) => w.id === selectedWalletId)
      ? selectedWalletId
      : (defaultWallet?.id || '');

  // Stable idempotency key tracking per refund item attempt
  const refundAttemptKeysRef = useRef<Map<string, string>>(new Map());
  const getStableAttemptKey = (refundId: string) => {
    if (!refundAttemptKeysRef.current.has(refundId)) {
      refundAttemptKeysRef.current.set(refundId, `grp-refund-${refundId}-${Date.now()}`);
    }
    return refundAttemptKeysRef.current.get(refundId)!;
  };

  const handleVoteClick = async (decision: 'Approve' | 'Reject') => {
    onVote(decision);
  };

  const handleExecuteRefund = async (refundItem: { id?: string; userId: string; refundAmount: number }) => {
    if (!refundItem.id) return;
    if (refundItem.userId !== currentUserId) return;
    setRefundActionError(null);

    const targetWalletId = effectiveWalletId;
    if (!targetWalletId) {
      setRefundActionError(
        locale === 'vi-VN'
          ? 'Không tìm thấy ví cá nhân để nhận tiền hoàn'
          : 'No personal wallet found to receive refund'
      );
      return;
    }

    const pin = await challengePinWithValue({
      actionName:
        locale === 'vi-VN'
          ? 'Xác nhận hoàn tiền quỹ về ví cá nhân'
          : 'Confirm fund refund to personal wallet',
      amount: refundItem.refundAmount,
    });
    if (pin === null) return;

    setRefundingItemId(refundItem.id);
    const idempotencyKey = getStableAttemptKey(refundItem.id);
    try {
      const res = await executeRefund({
        groupId: group.id,
        requestId: request.id,
        refundId: refundItem.id,
        walletId: targetWalletId,
        securityPin: pin,
        idempotencyKey,
      });

      if (res.success) {
        refundAttemptKeysRef.current.delete(refundItem.id);
      } else {
        setRefundActionError(getLocalizedErrorMessage(res.error, locale, t));
      }
    } catch (err: any) {
      setRefundActionError(
        getLocalizedErrorMessage(err?.message, locale, t, 'common.errorOccurred')
      );
    } finally {
      setRefundingItemId(null);
    }
  };

  const handleConfirmFinalize = async () => {
    if (isFinalizing) return;
    if (!allRefundsCompleted) {
      setFinalizeError(
        !hasRefundPlan
          ? (locale === 'vi-VN'
              ? 'Đang chờ hệ thống tạo kế hoạch hoàn quỹ trước khi đóng nhóm.'
              : 'Waiting for the refund plan before closing group.')
          : (locale === 'vi-VN'
              ? `Vui lòng hoàn tất hoàn trả quỹ cho ${pendingMemberRefunds.length} thành viên trước khi đóng nhóm.`
              : `Please complete refunds for all ${pendingMemberRefunds.length} member(s) before closing group.`)
      );
      return;
    }

    const pin = await challengePinWithValue({
      actionName:
        locale === 'vi-VN'
          ? 'Hoàn tất giải tán nhóm và chốt quỹ'
          : 'Finalize group deletion and shared fund',
      amount: refundPlan?.totalRefund || 0,
    });
    if (pin === null) return;

    setIsFinalizing(true);
    setFinalizeError(null);
    try {
      if (onFinalize) {
        await onFinalize();
      } else {
        const res = await finalizeGroupDeletionAndRefund(group.id, pin);
        if (!res.success) {
          throw new Error(res.error);
        }
      }
      setShowConfirmModal(false);
    } catch (err: any) {
      setFinalizeError(
        getLocalizedErrorMessage(err?.message, locale, t, 'common.errorOccurred')
      );
    } finally {
      setIsFinalizing(false);
    }
  };

  return (
    <div
      className={cn(
        'rounded-2xl border p-5 transition-all shadow-sm space-y-4',
        request.status === 'Pending' &&
          'bg-amber-50/70 dark:bg-amber-950/30 border-amber-300/80 dark:border-amber-700/60 text-slate-900 dark:text-slate-100',
        request.status === 'Ready' &&
          'bg-emerald-50/80 dark:bg-emerald-950/30 border-emerald-300/80 dark:border-emerald-700/60 text-slate-900 dark:text-slate-100',
        request.status === 'Rejected' &&
          'bg-rose-50/70 dark:bg-rose-950/30 border-rose-200 dark:border-rose-800 text-slate-900 dark:text-slate-100',
        className
      )}
    >
      {/* Header Row */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div className="flex items-start gap-3">
          <div
            className={cn(
              'p-2.5 rounded-xl shrink-0 mt-0.5',
              request.status === 'Pending' && 'bg-amber-500/20 text-amber-600 dark:text-amber-400',
              request.status === 'Ready' && 'bg-emerald-500/20 text-emerald-600 dark:text-emerald-400',
              request.status === 'Rejected' && 'bg-rose-500/20 text-rose-600 dark:text-rose-400'
            )}
          >
            {request.status === 'Pending' && <AlertTriangle className="h-5 w-5" />}
            {request.status === 'Ready' && <ShieldCheck className="h-5 w-5" />}
            {request.status === 'Rejected' && <Ban className="h-5 w-5" />}
          </div>

          <div>
            <div className="flex items-center gap-2 flex-wrap">
              <span
                className={cn(
                  'px-2 py-0.5 rounded-md text-[10px] font-black uppercase tracking-wider',
                  request.status === 'Pending' &&
                    'bg-amber-200/80 text-amber-900 dark:bg-amber-900/60 dark:text-amber-200',
                  request.status === 'Ready' &&
                    'bg-emerald-200/80 text-emerald-900 dark:bg-emerald-900/60 dark:text-emerald-200',
                  request.status === 'Rejected' &&
                    'bg-rose-200/80 text-rose-900 dark:bg-rose-900/60 dark:text-rose-200'
                )}
              >
                {request.status === 'Pending' &&
                  (locale === 'vi-VN'
                    ? 'Chế độ chỉ đọc • Đang chờ biểu quyết'
                    : 'Read-only • Pending Approval')}
                {request.status === 'Ready' &&
                  (locale === 'vi-VN'
                    ? '100% Đồng thuận • Sẵn sàng hoàn quỹ & xóa nhóm'
                    : '100% Approved • Ready to Refund')}
                {request.status === 'Rejected' &&
                  (locale === 'vi-VN' ? 'Yêu cầu bị từ chối' : 'Request Rejected')}
              </span>
            </div>

            <h4 className="text-sm font-bold text-slate-900 dark:text-slate-100 mt-1">
              {request.status === 'Pending' &&
                (locale === 'vi-VN'
                  ? 'Yêu cầu giải tán nhóm đang chờ tất cả thành viên biểu quyết'
                  : 'Group deletion request is pending member votes')}
              {request.status === 'Ready' &&
                (locale === 'vi-VN'
                  ? 'Kế hoạch hoàn quỹ tự động từ backend đã sẵn sàng'
                  : 'Refund plan is ready for execution')}
              {request.status === 'Rejected' &&
                (locale === 'vi-VN'
                  ? 'Yêu cầu xóa nhóm đã bị từ chối'
                  : 'Group deletion request was rejected')}
            </h4>
          </div>
        </div>

        {/* Voting & Actions for Header */}
        <div className="flex items-center gap-2 self-start sm:self-center">
          {canCurrentUserVote && (
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => handleVoteClick('Approve')}
                className="px-3.5 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-sm transition-all flex items-center gap-1.5"
              >
                <CheckCircle2 className="h-3.5 w-3.5" />
                <span>{locale === 'vi-VN' ? 'Đồng ý' : 'Approve'}</span>
              </button>
              <button
                type="button"
                onClick={() => handleVoteClick('Reject')}
                className="px-3.5 py-1.5 rounded-xl bg-rose-600 hover:bg-rose-700 text-white text-xs font-semibold shadow-sm transition-all flex items-center gap-1.5"
              >
                <XCircle className="h-3.5 w-3.5" />
                <span>{locale === 'vi-VN' ? 'Từ chối' : 'Reject'}</span>
              </button>
            </div>
          )}

          {isOwner && request.status === 'Pending' && (
            <button
              type="button"
              onClick={onCancel}
              className="px-3 py-1.5 rounded-xl border border-slate-300 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 text-xs font-semibold transition-all"
            >
              {locale === 'vi-VN' ? 'Hủy yêu cầu' : 'Cancel Request'}
            </button>
          )}

          {isOwner && request.status === 'Ready' && (
            <button
              type="button"
              onClick={() => setShowConfirmModal(true)}
              disabled={!allRefundsCompleted}
              className={cn(
                'px-4 py-2 rounded-xl text-white text-xs font-bold transition-all flex items-center gap-1.5',
                allRefundsCompleted
                  ? 'bg-rose-600 hover:bg-rose-700 shadow-md shadow-rose-600/20 cursor-pointer'
                  : 'bg-slate-400 dark:bg-slate-700 cursor-not-allowed opacity-75'
              )}
            >
              <Ban className="h-3.5 w-3.5" />
              <span>{locale === 'vi-VN' ? 'Hoàn tất xóa nhóm' : 'Finalize Deletion'}</span>
            </button>
          )}
        </div>
      </div>

      {/* Member Votes List in Pending Status */}
      {request.status === 'Pending' && (
        <div className="pt-2 border-t border-amber-200 dark:border-amber-800/60 space-y-2">
          <p className="text-xs font-semibold text-slate-700 dark:text-slate-300">
            {locale === 'vi-VN'
              ? `Tiến độ biểu quyết: ${approvedCount}/${totalVotes} thành viên đã đồng ý`
              : `Voting progress: ${approvedCount}/${totalVotes} approved`}
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2">
            {request.memberVotes.map((v) => (
              <div
                key={v.userId}
                className="p-2.5 rounded-xl bg-white/70 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800 flex items-center justify-between text-xs"
              >
                <span className="font-medium text-slate-800 dark:text-slate-200 truncate">
                  {v.userName}
                </span>
                <span
                  className={cn(
                    'text-[10px] font-bold px-2 py-0.5 rounded-md flex items-center gap-1',
                    v.status === 'Approved' && 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300',
                    v.status === 'Rejected' && 'bg-rose-100 text-rose-800 dark:bg-rose-950/60 dark:text-rose-300',
                    v.status === 'Pending' && 'bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300'
                  )}
                >
                  {v.status === 'Approved' && <CheckCircle2 className="h-3 w-3" />}
                  {v.status === 'Rejected' && <XCircle className="h-3 w-3" />}
                  {v.status === 'Pending' && <Clock className="h-3 w-3" />}
                  <span>
                    {v.status === 'Approved'
                      ? locale === 'vi-VN'
                        ? 'Đồng ý'
                        : 'Approved'
                      : v.status === 'Rejected'
                      ? locale === 'vi-VN'
                        ? 'Từ chối'
                        : 'Rejected'
                      : locale === 'vi-VN'
                      ? 'Đang chờ'
                      : 'Pending'}
                  </span>
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Refund Plan when Ready */}
      {request.status === 'Ready' && (
        <div className="pt-2 border-t border-emerald-200 dark:border-emerald-800/60 space-y-3">
          <div className="flex items-center justify-between text-xs">
            <span className="font-semibold text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN'
                ? `Kế hoạch hoàn quỹ (${formatCurrency(refundPlan?.fundBalance || 0)})`
                : `Fund Refund Plan (${formatCurrency(refundPlan?.fundBalance || 0)})`}
            </span>
            <span className="text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN'
                ? 'Hoàn trả nội bộ về Ví SpendWise'
                : 'Credited internally to SpendWise Wallets'}
            </span>
          </div>

          {!hasRefundPlan ? (
            <div className="p-3 text-xs text-slate-500 dark:text-slate-400 bg-white/70 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800 rounded-xl">
              {locale === 'vi-VN'
                ? 'Đang chờ hệ thống tạo kế hoạch hoàn quỹ.'
                : 'Waiting for the refund plan.'}
            </div>
          ) : (
            <>
              {refundActionError && (
                <div className="p-3 text-xs text-rose-700 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 rounded-xl flex items-center gap-2">
                  <AlertCircle className="h-4 w-4 shrink-0" />
                  <span>{refundActionError}</span>
                </div>
              )}

              <div className="space-y-2">
                {memberRefunds.length === 0 ? (
                  <div className="p-3 text-xs text-slate-500 dark:text-slate-400 bg-white/70 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800 rounded-xl text-center">
                    {locale === 'vi-VN'
                      ? 'Không có khoản tiền hoàn nào cần xử lý.'
                      : 'No refunds required.'}
                  </div>
                ) : (
                  memberRefunds.map((item) => {
                    const isCurrentUserItem = item.userId === currentUserId;
                    const canRefund =
                      isCurrentUserItem &&
                      item.settlementStatus !== 'completed' &&
                      item.refundAmount > 0;
                    const isProcessing = refundingItemId === item.id;

                    return (
                      <div
                        key={item.userId}
                        className="p-3 rounded-xl bg-white/80 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800 flex items-center justify-between gap-3 text-xs"
                      >
                        <div>
                          <div className="flex items-center gap-2">
                            <span className="font-bold text-slate-900 dark:text-slate-100">
                              {item.userName}
                            </span>
                            {isCurrentUserItem && (
                              <span className="px-1.5 py-0.5 rounded text-[10px] bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 font-semibold">
                                {locale === 'vi-VN' ? 'Bạn' : 'You'}
                              </span>
                            )}
                          </div>
                          <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">
                            {locale === 'vi-VN' ? 'Đã đóng góp:' : 'Contributed:'}{' '}
                            <span className="font-semibold text-slate-700 dark:text-slate-300">
                              {formatCurrency(item.totalContributed)}
                            </span>
                          </p>
                        </div>

                        <div className="flex items-center gap-3">
                          <span className="font-bold text-emerald-600 dark:text-emerald-400 text-sm">
                            +{formatCurrency(item.refundAmount)}
                          </span>

                          {item.settlementStatus === 'completed' ? (
                            <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/60 px-2 py-0.5 rounded-md border border-emerald-200/60 dark:border-emerald-800/60">
                              <CheckCircle2 className="h-3 w-3" />
                              <span>{locale === 'vi-VN' ? 'Đã hoàn về ví' : 'Refunded to Wallet'}</span>
                            </span>
                          ) : item.refundAmount === 0 ? (
                            <span className="inline-flex items-center gap-1 text-[10px] font-medium text-slate-500 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 rounded-md">
                              <Check className="h-3 w-3" />
                              <span>{locale === 'vi-VN' ? 'Không có hoàn' : 'No refund'}</span>
                            </span>
                          ) : canRefund ? (
                            activeWallets.length === 0 ? (
                              <span className="inline-flex items-center gap-1 text-[10px] font-medium text-amber-700 dark:text-amber-300 bg-amber-50 dark:bg-amber-950/60 px-2 py-0.5 rounded-md">
                                <Clock className="h-3 w-3" />
                                <span>
                                  {locale === 'vi-VN'
                                    ? 'Chưa có ví cá nhân khả dụng'
                                    : 'No personal wallet available'}
                                </span>
                              </span>
                            ) : (
                              <div className="flex items-center gap-1.5">
                                <select
                                  value={effectiveWalletId}
                                  onChange={(e) => setSelectedWalletId(e.target.value)}
                                  disabled={isProcessing || refundingItemId !== null}
                                  className="h-7 px-2 text-[11px] rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-200 focus:outline-none focus:ring-1 focus:ring-emerald-500 max-w-[140px] truncate"
                                  title={locale === 'vi-VN' ? 'Chọn ví nhận tiền hoàn' : 'Select refund destination wallet'}
                                >
                                  {activeWallets.map((w) => (
                                    <option key={w.id} value={w.id}>
                                      {w.name} ({formatCurrency(w.balance)})
                                    </option>
                                  ))}
                                </select>
                                <button
                                  type="button"
                                  onClick={() => handleExecuteRefund(item)}
                                  disabled={isProcessing || refundingItemId !== null || !effectiveWalletId}
                                  className="px-2.5 py-1 rounded-lg bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 text-white text-[11px] font-bold shadow-sm transition-all flex items-center gap-1 shrink-0"
                                >
                                  {isProcessing ? (
                                    <Loader2 className="h-3 w-3 animate-spin" />
                                  ) : (
                                    <Wallet className="h-3 w-3" />
                                  )}
                                  <span>{locale === 'vi-VN' ? 'Nhận tiền hoàn' : 'Refund to Wallet'}</span>
                                </button>
                              </div>
                            )
                          ) : (
                            <span className="inline-flex items-center gap-1 text-[10px] font-medium text-amber-700 dark:text-amber-300 bg-amber-50 dark:bg-amber-950/60 px-2 py-0.5 rounded-md">
                              <Clock className="h-3 w-3" />
                              <span>
                                {locale === 'vi-VN'
                                  ? 'Chờ thành viên tự nhận về ví'
                                  : 'Waiting for member to claim in session'}
                              </span>
                            </span>
                          )}
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            </>
          )}
        </div>
      )}

      {/* Confirm Finalize Modal */}
      {showConfirmModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fadeIn">
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl w-full max-w-md shadow-2xl overflow-hidden p-6 space-y-4 animate-scaleUp">
            <div className="flex items-center gap-3 text-rose-600 dark:text-rose-400">
              <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/80">
                <Ban className="h-6 w-6" />
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-slate-100">
                  {locale === 'vi-VN' ? 'Xác nhận xóa và giải tán nhóm' : 'Finalize Group Deletion'}
                </h3>
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  {group.name}
                </p>
              </div>
            </div>

            <p className="text-xs text-slate-600 dark:text-slate-400 leading-relaxed">
              {locale === 'vi-VN'
                ? 'Hành động này sẽ đóng nhóm vĩnh viễn trên hệ thống. Tất cả các khoản hoàn quỹ phải được hoàn tất trước khi tiếp tục. Yêu cầu nhập mã PIN bảo mật của Chủ nhóm.'
                : 'This action will permanently close the group. All member refunds must be completed before proceeding. Requires Group Owner Security PIN.'}
            </p>

            {finalizeError && (
              <div className="p-3 text-xs text-rose-700 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 rounded-xl flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{finalizeError}</span>
              </div>
            )}

            <div className="flex items-center justify-end gap-3 pt-2">
              <button
                type="button"
                onClick={() => setShowConfirmModal(false)}
                className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
              >
                {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
              </button>
              <button
                type="button"
                onClick={handleConfirmFinalize}
                disabled={isFinalizing || !allRefundsCompleted}
                className="px-4 py-2 rounded-xl text-xs font-bold text-white bg-rose-600 hover:bg-rose-700 disabled:opacity-50 disabled:cursor-not-allowed shadow-md shadow-rose-600/20 transition-all flex items-center gap-1.5"
              >
                {isFinalizing ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Ban className="h-4 w-4" />
                )}
                <span>
                  {isFinalizing
                    ? locale === 'vi-VN'
                      ? 'Đang xử lý...'
                      : 'Processing...'
                    : locale === 'vi-VN'
                    ? 'Xác nhận xóa nhóm'
                    : 'Confirm Deletion'}
                </span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
