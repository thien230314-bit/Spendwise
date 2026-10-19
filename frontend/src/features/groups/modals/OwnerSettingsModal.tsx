import React, { useState } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { Group, GroupCategory } from '../../../types/groups';
import {
  X,
  Settings,
  ShieldAlert,
  Trash2,
  ArrowRight,
  CheckCircle,
  AlertCircle,
  AlertTriangle,
  Clock,
  CheckCircle2,
  XCircle,
  ShieldCheck,
  Coins,
  Wallet,
  Loader2,
  Check,
} from 'lucide-react';
import { cn } from '../../../utils/cn';
import { Select } from '../../../components/ui/Select';
import { useAuth } from '../../../providers/AuthProvider';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';

interface OwnerSettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  group: Group;
  onUpdateInfo: (
    groupId: string,
    updates: { name?: string; description?: string; type?: GroupCategory }
  ) => { success: boolean; error?: string } | Promise<{ success: boolean; error?: string }>;
  onTransferOwnership: (
    groupId: string,
    newOwnerUserId: string
  ) => { success: boolean; error?: string } | Promise<{ success: boolean; error?: string }>;
  onCloseGroup?: (groupId: string) => { success: boolean; error?: string } | Promise<{ success: boolean; error?: string }>;
  onRequestDeletion?: (groupId: string) => { success: boolean; error?: string } | Promise<{ success: boolean; error?: string }>;
  onCancelDeletion?: (groupId: string) => { success: boolean; error?: string } | Promise<{ success: boolean; error?: string }>;
  onFinalizeDeletion?: (groupId: string) => { success: boolean; error?: string } | Promise<{ success: boolean; error?: string }>;
}

export const OwnerSettingsModal: React.FC<OwnerSettingsModalProps> = ({
  isOpen,
  onClose,
  group,
  onUpdateInfo,
  onTransferOwnership,
  onCloseGroup,
  onRequestDeletion,
  onCancelDeletion,
  onFinalizeDeletion,
}) => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();

  const [name, setName] = useState(group.name);
  const [description, setDescription] = useState(group.description);
  const [type, setType] = useState<GroupCategory>(group.type);
  const [newOwnerUserId, setNewOwnerUserId] = useState<string>('');
  const [confirmClose, setConfirmClose] = useState(false);
  const [confirmFinalize, setConfirmFinalize] = useState(false);
  const [isFinalizing, setIsFinalizing] = useState(false);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  if (!isOpen) return null;

  const deletionRequest = group.deletionRequest;
  const isDeletionPending = deletionRequest?.status === 'Pending';
  const isDeletionReady = deletionRequest?.status === 'Ready';
  const isFrozen = isDeletionPending || isDeletionReady;

  const otherMembers = group.members.filter(
    (m) => m.userId !== group.ownerId && m.status === 'active'
  );

  const handleSaveInfo = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    if (!name.trim()) {
      setError(locale === 'vi-VN' ? 'Tên nhóm không được để trống' : 'Group name cannot be empty');
      return;
    }

    const res = await onUpdateInfo(group.id, {
      name: name.trim(),
      description: description.trim(),
      type,
    });

    if (!res.success) {
      setError(getLocalizedErrorMessage(res.error, locale, t));
    } else {
      setSuccessMsg(locale === 'vi-VN' ? 'Đã cập nhật thông tin nhóm!' : 'Group info updated!');
      setTimeout(() => setSuccessMsg(''), 3000);
    }
  };

  const handleTransfer = async () => {
    setError('');
    if (!newOwnerUserId) {
      setError(locale === 'vi-VN' ? 'Vui lòng chọn thành viên nhận quyền' : 'Please select a new owner');
      return;
    }

    const res = await onTransferOwnership(group.id, newOwnerUserId);
    if (!res.success) {
      setError(getLocalizedErrorMessage(res.error, locale, t));
    } else {
      onClose();
    }
  };

  const handleStartDeletionRequest = async () => {
    setError('');
    const handler = onRequestDeletion || onCloseGroup;
    if (!handler) return;

    const res = await handler(group.id);
    if (!res.success) {
      setError(getLocalizedErrorMessage(res.error, locale, t));
    } else {
      setConfirmClose(false);
      setSuccessMsg(
        locale === 'vi-VN'
          ? 'Đã tạo yêu cầu xóa nhóm! Đang chờ các thành viên biểu quyết.'
          : 'Deletion request created! Awaiting member votes.'
      );
      setTimeout(() => setSuccessMsg(''), 4000);
    }
  };

  const handleCancelDeletion = async () => {
    setError('');
    if (!onCancelDeletion) return;
    const res = await onCancelDeletion(group.id);
    if (!res.success) {
      setError(getLocalizedErrorMessage(res.error, locale, t));
    } else {
      setSuccessMsg(locale === 'vi-VN' ? 'Đã hủy yêu cầu xóa nhóm.' : 'Deletion request cancelled.');
      setTimeout(() => setSuccessMsg(''), 3000);
    }
  };

  const approvedVotesCount = deletionRequest?.memberVotes.filter((v) => v.status === 'Approved').length || 0;
  const totalVotesCount = deletionRequest?.memberVotes.length || 0;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fadeIn">
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl w-full max-w-lg shadow-2xl overflow-hidden animate-scaleUp">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-purple-50 dark:bg-purple-950/80 text-purple-600 dark:text-purple-400">
              <Settings className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900 dark:text-slate-100">
                {t('groups.ownerSettings')}
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {group.name} • {locale === 'vi-VN' ? 'Dành riêng cho Chủ nhóm' : 'Owner-only controls'}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-6 max-h-[80vh] overflow-y-auto">
          {error && (
            <div className="flex items-center gap-2 p-3 text-xs text-rose-700 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 rounded-xl">
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {successMsg && (
            <div className="flex items-center gap-2 p-3 text-xs text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/50 border border-emerald-200 dark:border-emerald-800 rounded-xl">
              <CheckCircle className="h-4 w-4 shrink-0" />
              <span>{successMsg}</span>
            </div>
          )}

          {/* Section 1: Basic Information */}
          <form
            onSubmit={handleSaveInfo}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleSaveInfo(e);
              }
            }}
            className="space-y-3"
          >
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN' ? 'Thông tin chung' : 'General Info'}
            </h3>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                {t('groups.changeGroupName')}
              </label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                disabled={isFrozen}
                className="w-full px-3.5 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-xs focus:outline-none focus:ring-2 focus:ring-purple-500 disabled:opacity-60"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                {t('groups.editDescription')}
              </label>
              <input
                type="text"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                disabled={isFrozen}
                className="w-full px-3.5 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-xs focus:outline-none focus:ring-2 focus:ring-purple-500 disabled:opacity-60"
              />
            </div>

            <button
              type="submit"
              disabled={isFrozen}
              className="px-4 py-2 rounded-xl bg-purple-600 hover:bg-purple-700 disabled:opacity-50 text-white text-xs font-semibold shadow-sm transition-all"
            >
              {locale === 'vi-VN' ? 'Lưu thay đổi' : 'Save Changes'}
            </button>
            {isFrozen && (
              <span className="text-[11px] text-amber-600 dark:text-amber-400 block">
                {t('groups.groupFrozenNotice')}
              </span>
            )}
          </form>

          {/* Section 2: Transfer Ownership */}
          <div className="pt-4 border-t border-slate-200 dark:border-slate-800 space-y-3">
            <h3 className="text-xs font-bold uppercase tracking-wider text-amber-600 dark:text-amber-400 flex items-center gap-1.5">
              <ShieldAlert className="h-3.5 w-3.5" />
              <span>{t('groups.transferOwnership')}</span>
            </h3>
            <p className="text-xs text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN'
                ? 'Sau khi chuyển giao, bạn sẽ trở thành Thành viên trong nhóm này.'
                : 'After transferring, your role in this group will become regular Member.'}
            </p>

            <div className="flex items-center gap-2">
              <div className="flex-1">
                <Select
                  value={newOwnerUserId}
                  onChange={(val) => setNewOwnerUserId(val)}
                  disabled={isFrozen}
                  accentColor="amber"
                >
                  <option value="">{locale === 'vi-VN' ? 'Chọn thành viên nhận quyền...' : 'Select member...'}</option>
                  {otherMembers.map((m) => (
                    <option key={m.userId} value={m.userId}>
                      {m.name}{m.phone ? ` (${m.phone})` : m.email ? ` (${m.email})` : ''}
                    </option>
                  ))}
                </Select>
              </div>

              <button
                type="button"
                onClick={handleTransfer}
                disabled={!newOwnerUserId || isFrozen}
                className="px-4 py-2.5 rounded-xl bg-amber-600 hover:bg-amber-700 disabled:opacity-50 text-white text-xs font-semibold shadow-sm transition-all shrink-0"
              >
                {locale === 'vi-VN' ? 'Chuyển quyền' : 'Transfer'}
              </button>
            </div>
          </div>

          {/* Section 3: Group Deletion Request & Approval Flow */}
          <div className="pt-4 border-t border-slate-200 dark:border-slate-800 space-y-3">
            <h3 className="text-xs font-bold uppercase tracking-wider text-rose-600 dark:text-rose-400 flex items-center gap-1.5">
              <Trash2 className="h-3.5 w-3.5" />
              <span>{t('groups.requestGroupDeletion')}</span>
            </h3>

            {/* Sub-state A: Pending Deletion Request */}
            {isDeletionPending && (
              <div className="p-4 bg-amber-50 dark:bg-amber-950/40 rounded-xl border border-amber-300 dark:border-amber-700 space-y-3">
                <div className="flex items-start gap-2.5">
                  <AlertTriangle className="h-5 w-5 text-amber-600 shrink-0 mt-0.5" />
                  <div>
                    <h4 className="text-xs font-bold text-amber-900 dark:text-amber-100">
                      {t('groups.deletionPendingBannerTitle')}
                    </h4>
                    <p className="text-[11px] text-amber-800 dark:text-amber-300 mt-0.5">
                      {locale === 'vi-VN'
                        ? `Đang chờ biểu quyết: ${approvedVotesCount}/${totalVotesCount} thành viên đã đồng ý. Cần 100% thành viên hoạt động phê duyệt.`
                        : `Awaiting votes: ${approvedVotesCount}/${totalVotesCount} approved. Requires 100% unanimous member consensus.`}
                    </p>
                  </div>
                </div>

                {/* Member Vote Status List */}
                <div className="space-y-1 pt-1">
                  {deletionRequest?.memberVotes.map((v) => (
                    <div
                      key={v.userId}
                      className="flex items-center justify-between p-2 rounded-lg bg-white/80 dark:bg-slate-900/80 border border-amber-200/80 dark:border-amber-800/60 text-xs"
                    >
                      <span className="font-semibold text-slate-800 dark:text-slate-200">
                        {v.userName} {v.userId === group.ownerId ? `(${locale === 'vi-VN' ? 'Chủ nhóm' : 'Owner'})` : ''}
                      </span>
                      <div className="flex items-center gap-1">
                        {v.status === 'Approved' && (
                          <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-emerald-600 dark:text-emerald-400">
                            <CheckCircle2 className="h-3.5 w-3.5" />
                            <span>{locale === 'vi-VN' ? 'Đã đồng ý' : 'Approved'}</span>
                          </span>
                        )}
                        {v.status === 'Rejected' && (
                          <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-rose-600 dark:text-rose-400">
                            <XCircle className="h-3.5 w-3.5" />
                            <span>{locale === 'vi-VN' ? 'Đã từ chối' : 'Rejected'}</span>
                          </span>
                        )}
                        {v.status === 'Pending' && (
                          <span className="inline-flex items-center gap-1 text-[11px] font-medium text-amber-600 dark:text-amber-400">
                            <Clock className="h-3.5 w-3.5" />
                            <span>{locale === 'vi-VN' ? 'Đang chờ' : 'Pending'}</span>
                          </span>
                        )}
                      </div>
                    </div>
                  ))}
                </div>

                {/* Cancel Deletion Request Button */}
                <div className="pt-2">
                  <button
                    type="button"
                    onClick={handleCancelDeletion}
                    className="px-3.5 py-1.5 rounded-lg border border-amber-400 dark:border-amber-700 hover:bg-amber-100 dark:hover:bg-amber-900/50 text-amber-900 dark:text-amber-200 text-xs font-semibold transition-colors"
                  >
                    {t('groups.cancelGroupDeletion')}
                  </button>
                </div>
              </div>
            )}

            {/* Sub-state B: Ready Deletion Request */}
            {isDeletionReady && (
              <div className="p-4 bg-emerald-50 dark:bg-emerald-950/40 rounded-xl border border-emerald-300 dark:border-emerald-700 space-y-3.5">
                <div className="flex items-start gap-2.5">
                  <ShieldCheck className="h-5 w-5 text-emerald-600 dark:text-emerald-400 shrink-0 mt-0.5" />
                  <div>
                    <h4 className="text-xs font-bold text-emerald-950 dark:text-emerald-100">
                      {locale === 'vi-VN' ? 'Tất cả thành viên đã đồng ý.' : 'All members approved.'}
                    </h4>
                    <p className="text-xs text-emerald-800 dark:text-emerald-200 mt-0.5 leading-relaxed">
                      {locale === 'vi-VN'
                        ? 'Tất cả thành viên đã đồng ý xóa nhóm. Nhóm đang chờ hoàn quỹ và xử lý xóa.'
                        : 'All members approved the deletion. The group is waiting for fund refund and final deletion.'}
                    </p>
                  </div>
                </div>

                {/* Refund breakdown */}
                {(() => {
                  const refundPlan = deletionRequest?.refundPlan;
                  const hasRefundPlan = Boolean(refundPlan);
                  const formatCurrency = (val: number) =>
                    new Intl.NumberFormat(locale === 'vi-VN' ? 'vi-VN' : 'en-US', {
                      style: 'currency',
                      currency: 'VND',
                      maximumFractionDigits: 0,
                    }).format(val);

                  const pendingMemberRefunds = (refundPlan?.memberRefunds || []).filter(
                    (m) => m.refundAmount > 0 && m.settlementStatus !== 'completed'
                  );
                  const allRefundsCompleted =
                    hasRefundPlan && pendingMemberRefunds.length === 0;

                  return (
                    <div className="space-y-3 pt-1">
                      {/* Fund Balance */}
                      <div className="flex items-center justify-between text-xs p-2.5 rounded-lg bg-white/80 dark:bg-slate-900/80 border border-emerald-200 dark:border-emerald-800">
                        <span className="font-medium text-slate-700 dark:text-slate-300">
                          {locale === 'vi-VN' ? 'Số dư quỹ còn lại:' : 'Remaining Fund Balance:'}
                        </span>
                        <span className="font-bold text-emerald-700 dark:text-emerald-300">
                          {formatCurrency(group.sharedFund.balance)}
                        </span>
                      </div>

                      {/* Refund Plan list */}
                      {!hasRefundPlan ? (
                        <div className="p-3 rounded-lg bg-white/60 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800 text-xs text-slate-500 dark:text-slate-400">
                          {locale === 'vi-VN'
                            ? 'Đang chờ hệ thống tạo kế hoạch hoàn quỹ.'
                            : 'Waiting for the refund plan.'}
                        </div>
                      ) : (
                        <div className="space-y-1.5">
                          <span className="text-[11px] font-bold text-slate-600 dark:text-slate-400">
                            {locale === 'vi-VN' ? 'Danh sách hoàn tiền (Ví SpendWise):' : 'SpendWise Wallet Refund Recipients:'}
                          </span>
                          <div className="divide-y divide-slate-200/60 dark:divide-slate-800 rounded-lg border border-slate-200 dark:border-slate-800 bg-white/60 dark:bg-slate-900/60 overflow-hidden text-xs">
                            {refundPlan.memberRefunds.map((m) => {
                              const isOwnerMember = m.userId === group.ownerId;
                              return (
                                <div key={m.userId} className="p-2.5 flex items-center justify-between gap-2">
                                  <div>
                                    <div className="flex items-center gap-1.5 flex-wrap">
                                      <span className="font-semibold text-slate-800 dark:text-slate-200">
                                        {m.userName}
                                      </span>
                                      {isOwnerMember && (
                                        <span className="px-1.5 py-0.2 text-[9px] font-semibold rounded bg-indigo-100 text-indigo-800 dark:bg-indigo-900/60 dark:text-indigo-300">
                                          {locale === 'vi-VN' ? 'Chủ nhóm' : 'Owner'}
                                        </span>
                                      )}
                                    </div>
                                    <div className="text-[10px] text-slate-500">
                                      {locale === 'vi-VN' ? 'Đã đóng:' : 'Contributed:'}{' '}
                                      {formatCurrency(m.totalContributed)} ({m.percentage}%)
                                    </div>
                                  </div>

                                  <div className="text-right shrink-0 space-y-1">
                                    <div className="font-bold text-emerald-600 dark:text-emerald-400">
                                      +{formatCurrency(m.refundAmount)}
                                    </div>
                                    {isOwnerMember ? (
                                      <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-indigo-700 dark:text-indigo-300">
                                        <Check className="h-3 w-3" />
                                        {locale === 'vi-VN' ? 'Giữ lại quỹ cá nhân' : 'Retained by Owner'}
                                      </span>
                                    ) : m.settlementStatus === 'completed' ? (
                                      <div className="flex flex-col items-end">
                                        <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-emerald-700 dark:text-emerald-300">
                                          <CheckCircle2 className="h-3 w-3" />
                                          {locale === 'vi-VN' ? 'Đã hoàn về ví' : 'Refunded to Wallet'}
                                        </span>
                                        {m.paymentReference && (
                                          <span className="text-[9px] text-slate-400 font-mono">
                                            Ref: {m.paymentReference}
                                          </span>
                                        )}
                                      </div>
                                    ) : m.settlementStatus === 'payment_pending' ? (
                                      <div className="flex flex-col items-end">
                                        <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-blue-700 dark:text-blue-300">
                                          <Clock className="h-3 w-3" />
                                          {locale === 'vi-VN' ? 'Đang xử lý hoàn tiền' : 'Refund Pending'}
                                        </span>
                                        {m.paymentReference && (
                                          <span className="text-[9px] text-slate-400 font-mono">
                                            Ref: {m.paymentReference}
                                          </span>
                                        )}
                                      </div>
                                    ) : (
                                      <span className="text-[10px] text-amber-600 dark:text-amber-400 font-medium">
                                        {locale === 'vi-VN' ? 'Chưa chuyển hoàn' : 'Pending'}
                                      </span>
                                    )}
                                  </div>
                                </div>
                              );
                            })}
                          </div>

                          {/* Total Refund */}
                          <div className="flex items-center justify-between text-xs pt-1">
                            <span className="font-bold text-slate-700 dark:text-slate-300">
                              {locale === 'vi-VN' ? 'Tổng hoàn:' : 'Total Refund:'}
                            </span>
                            <span className="font-black text-emerald-700 dark:text-emerald-300">
                              {formatCurrency(refundPlan.totalRefund)}
                            </span>
                          </div>
                        </div>
                      )}

                      {!allRefundsCompleted && pendingMemberRefunds.length > 0 && (
                        <div className="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 text-xs text-amber-900 dark:text-amber-200 space-y-1">
                          <p className="font-semibold">
                            {locale === 'vi-VN'
                              ? `Còn ${pendingMemberRefunds.length} thành viên chưa nhận hoàn quỹ về ví SpendWise.`
                              : `${pendingMemberRefunds.length} member(s) have pending wallet refunds.`}
                          </p>
                          <p className="text-[11px] text-amber-800 dark:text-amber-300">
                            {locale === 'vi-VN'
                              ? 'Từng thành viên cần tự nhận tiền hoàn về ví cá nhân tại Bảng giải tán nhóm trên giao diện nhóm.'
                              : 'Each member must claim their refund into their personal wallet from the Group Deletion banner.'}
                          </p>
                        </div>
                      )}

                      {/* Owner Action: Refund Fund & Delete Group */}
                      {!confirmFinalize ? (
                        <div className="pt-2">
                          <button
                            type="button"
                            onClick={() => {
                              if (!allRefundsCompleted) {
                                setError(
                                  locale === 'vi-VN'
                                    ? `Cần hoàn tất hoàn tiền ví cho ${pendingMemberRefunds.length} thành viên trước khi xóa nhóm.`
                                    : `Please wait for all ${pendingMemberRefunds.length} member(s) to complete their wallet refunds before deleting the group.`
                                );
                                return;
                              }
                              setConfirmFinalize(true);
                            }}
                            disabled={!allRefundsCompleted}
                            className={cn(
                              'w-full px-4 py-2.5 rounded-xl text-white text-xs font-bold shadow-sm transition-all flex items-center justify-center gap-2',
                              allRefundsCompleted
                                ? 'bg-emerald-600 hover:bg-emerald-700 cursor-pointer'
                                : 'bg-slate-400 dark:bg-slate-700 cursor-not-allowed opacity-75'
                            )}
                          >
                            <Coins className="h-4 w-4" />
                            <span>{locale === 'vi-VN' ? 'Xác nhận xóa nhóm' : 'Confirm Group Deletion'}</span>
                          </button>
                        </div>
                      ) : (
                        <div className="p-3 bg-emerald-100/70 dark:bg-emerald-950/70 rounded-xl border border-emerald-300 dark:border-emerald-700 space-y-2">
                          <p className="text-xs font-semibold text-emerald-950 dark:text-emerald-100">
                            {locale === 'vi-VN'
                              ? 'Xác nhận giải tán nhóm vĩnh viễn? Toàn bộ các khoản hoàn quỹ về ví đã hoàn tất.'
                              : 'Permanently delete group? All wallet refunds are completed.'}
                          </p>
                          <div className="flex items-center gap-2">
                            <button
                              type="button"
                              disabled={isFinalizing || !allRefundsCompleted}
                              onClick={async () => {
                                if (!onFinalizeDeletion || isFinalizing) return;
                                setIsFinalizing(true);
                                const res = onFinalizeDeletion(group.id);
                                if (!res.success) {
                                  setError(getLocalizedErrorMessage(res.error, locale, t));
                                  setIsFinalizing(false);
                                }
                              }}
                              className="px-3.5 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 text-white text-xs font-bold shadow-sm flex items-center gap-1.5"
                            >
                              {isFinalizing && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
                              <span>{locale === 'vi-VN' ? 'Xác nhận xóa vĩnh viễn' : 'Confirm Permanent Deletion'}</span>
                            </button>
                            <button
                              type="button"
                              disabled={isFinalizing}
                              onClick={() => setConfirmFinalize(false)}
                              className="px-3 py-1.5 text-xs text-slate-500 hover:text-slate-800 dark:hover:text-slate-200"
                            >
                              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
                            </button>
                          </div>
                        </div>
                      )}
                    </div>
                  );
                })()}
              </div>
            )}

            {/* Sub-state C: Idle / Can Request Deletion */}
            {!isDeletionPending && !isDeletionReady && (
              <>
                {!confirmClose ? (
                  <div>
                    <button
                      type="button"
                      onClick={() => setConfirmClose(true)}
                      className="px-4 py-2 rounded-xl border border-rose-300 dark:border-rose-800 text-rose-600 dark:text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-950/40 text-xs font-semibold transition-colors"
                    >
                      {locale === 'vi-VN' ? 'Yêu cầu xóa nhóm (Cần 100% thành viên duyệt)...' : 'Request Group Deletion (Requires 100% Approval)...'}
                    </button>
                    <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-1">
                      {locale === 'vi-VN'
                        ? 'Khi bạn yêu cầu xóa, nhóm sẽ tạm thời bị khóa và tất cả thành viên sẽ cùng biểu quyết.'
                        : 'When initiated, the group becomes read-only and all active members must vote.'}
                    </p>
                  </div>
                ) : (
                  <div className="p-3.5 bg-rose-50 dark:bg-rose-950/50 rounded-xl border border-rose-200 dark:border-rose-800 space-y-2.5">
                    <p className="text-xs text-rose-800 dark:text-rose-200 font-semibold">
                      {locale === 'vi-VN'
                        ? 'Bạn có chắc chắn muốn bắt đầu yêu cầu xóa nhóm? Nhóm sẽ chuyển sang chế độ chỉ đọc cho đến khi tất cả thành viên đồng thuận. Tiền quỹ còn lại sẽ được hoàn trả vào Ví SpendWise của các thành viên theo tỷ lệ đóng góp.'
                        : 'Are you sure you want to request group deletion? The group will become read-only until all members vote. Any remaining fund will be refunded to members\' SpendWise Wallets proportionally.'}
                    </p>
                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        onClick={handleStartDeletionRequest}
                        className="px-3.5 py-1.5 rounded-lg bg-rose-600 hover:bg-rose-700 text-white text-xs font-bold shadow-sm transition-all"
                      >
                        {locale === 'vi-VN' ? 'Gửi yêu cầu xóa nhóm' : 'Send Deletion Request'}
                      </button>
                      <button
                        type="button"
                        onClick={() => setConfirmClose(false)}
                        className="px-3 py-1.5 text-xs text-slate-500 hover:text-slate-800 dark:hover:text-slate-200"
                      >
                        {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
                      </button>
                    </div>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

