import React, { useState, useEffect } from 'react';
import {
  CheckCircle2,
  XCircle,
  Search,
  AlertCircle,
  ChevronRight,
  Filter,
  FileQuestion,
  X,
  Inbox,
  Scale,
  RefreshCw,
} from 'lucide-react';
import { useAuth } from '../../providers/AuthProvider';
import { useI18n } from '../../i18n/I18nContext';
import { useSupportDisputes } from '../../services/supportDisputeService';
import { DisputeRecord, DisputeStatus, DisputeReason } from '../../types';
import { Button } from '../../components/ui/Button';
import { Select } from '../../components/ui/Select';
import { EmptyState } from '../../components/ui/EmptyState';
import { AttachmentList } from '../../components/support/AttachmentList';
import { formatMoney } from '../../utils/money';
import { getLocalizedErrorMessage } from '../../utils/localizationHelpers';

export const AdminDisputesPage: React.FC = () => {
  const { currentUser } = useAuth();
  const { t, locale } = useI18n();

  const {
    disputes,
    isLoading,
    loadError,
    refresh,
    adminReplyDispute,
    adminUpdateDisputeStatus,
    loadAttachmentBlob,
  } = useSupportDisputes(currentUser);

  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [selectedDispute, setSelectedDispute] = useState<DisputeRecord | null>(null);

  // Keep selectedDispute fresh when store updates
  useEffect(() => {
    if (selectedDispute) {
      const fresh = disputes.find((d) => d.id === selectedDispute.id);
      if (fresh) setSelectedDispute(fresh);
    }
  }, [disputes]);

  // Admin action form states
  const [adminMessage, setAdminMessage] = useState('');
  const [targetStatus, setTargetStatus] = useState<DisputeStatus | ''>('');
  const [actionSubmitting, setActionSubmitting] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3500);
  };

  const filteredDisputes = disputes.filter((d) => {
    const matchesSearch =
      (d.disputeCode || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
      (d.id || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
      (d.transactionRef || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
      (d.userName || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
      (d.userEmail || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
      (d.description || '').toLowerCase().includes(searchQuery.toLowerCase());

    const matchesStatus =
      statusFilter === 'all' ||
      (statusFilter === 'open_group' && (d.status === 'pending' || d.status === 'submitted' || d.status === 'in_review' || d.status === 'under_review')) ||
      d.status === statusFilter;

    return matchesSearch && matchesStatus;
  });

  const getReasonLabel = (reason: DisputeReason) => {
    if (locale === 'vi-VN') {
      switch (reason) {
        case 'unrecognized_charge':
          return 'Giao dịch lạ / Nghi vấn gian lận';
        case 'duplicate_deduction':
        case 'duplicate_transaction':
          return 'Bị trừ tiền 2 lần / Trùng lặp';
        case 'incorrect_amount':
          return 'Sai số tiền giao dịch';
        case 'goods_not_received':
          return 'Chưa nhận được hàng hóa / dịch vụ';
        case 'merchant_dispute':
          return 'Bất đồng với bên bán / Lỗi giao dịch';
        case 'other':
        default:
          return 'Lý do khác';
      }
    } else {
      switch (reason) {
        case 'unrecognized_charge':
          return 'Unrecognized Charge / Fraud Suspected';
        case 'duplicate_deduction':
        case 'duplicate_transaction':
          return 'Duplicate Deduction';
        case 'incorrect_amount':
          return 'Incorrect Amount';
        case 'goods_not_received':
          return 'Goods / Services Not Received';
        case 'merchant_dispute':
          return 'Merchant Dispute / Transaction Error';
        case 'other':
        default:
          return 'Other Reason';
      }
    }
  };

  const getStatusBadge = (status: string) => {
    const isVi = locale === 'vi-VN';
    switch (status) {
      case 'pending':
      case 'submitted':
        return {
          label: isVi ? 'Chờ tiếp nhận' : 'Pending',
          className: 'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 border border-amber-200/60 dark:border-amber-800/40',
        };
      case 'in_review':
      case 'under_review':
        return {
          label: isVi ? 'Đang rà soát' : 'In Review',
          className: 'bg-sky-50 text-sky-700 dark:bg-sky-950/40 dark:text-sky-400 border border-sky-200/60 dark:border-sky-800/40',
        };
      case 'need_more_info':
        return {
          label: isVi ? 'Cần thêm thông tin' : 'Need More Info',
          className: 'bg-orange-50 text-orange-700 dark:bg-orange-950/40 dark:text-orange-400 border border-orange-200/60 dark:border-orange-800/40',
        };
      case 'resolved':
        return {
          label: isVi ? 'Đã giải quyết (Chấp nhận)' : 'Resolved (Approved)',
          className: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40',
        };
      case 'rejected':
      default:
        return {
          label: isVi ? 'Từ chối khiếu nại' : 'Rejected',
          className: 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60 dark:border-rose-800/40',
        };
    }
  };

  const formatDate = (dateStr: string) => {
    try {
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return dateStr;
      return d.toLocaleDateString(locale === 'vi-VN' ? 'vi-VN' : 'en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return dateStr;
    }
  };

  const handleAdminAction = async (forcedStatus?: DisputeStatus) => {
    if (!selectedDispute) return;
    const finalNewStatus = forcedStatus || (targetStatus ? (targetStatus as DisputeStatus) : undefined);

    if (finalNewStatus === 'rejected' && !adminMessage.trim()) {
      setActionError(
        locale === 'vi-VN'
          ? 'Vui lòng nhập lý do từ chối khiếu nại.'
          : 'Please enter the dispute rejection reason.'
      );
      return;
    }

    if (finalNewStatus === 'resolved' && !adminMessage.trim()) {
      setActionError(
        locale === 'vi-VN'
          ? 'Vui lòng nhập nội dung giải quyết khiếu nại.'
          : 'Please enter the dispute resolution message.'
      );
      return;
    }

    if (!adminMessage.trim() && !finalNewStatus) {
      setActionError(
        locale === 'vi-VN'
          ? 'Vui lòng nhập ghi chú hoặc chọn trạng thái cập nhật.'
          : 'Please enter a note or select an updated status.'
      );
      return;
    }

    setActionSubmitting(true);
    setActionError(null);

    try {
      let res: { success: boolean; dispute?: DisputeRecord; error?: string };

      if (finalNewStatus) {
        // Changing dispute status via POST /admin/cases/disputes/{id}/status
        res = await adminUpdateDisputeStatus({
          disputeId: selectedDispute.id,
          status: finalNewStatus,
          message: adminMessage.trim() || (
            finalNewStatus === 'need_more_info'
              ? 'Yêu cầu người dùng bổ sung tài liệu đối soát'
              : 'Cập nhật trạng thái đối soát'
          ),
        });
      } else {
        // Sending admin reply via POST /admin/cases/disputes/{id}/messages
        res = await adminReplyDispute({
          disputeId: selectedDispute.id,
          message: adminMessage.trim(),
        });
      }

      if (res.success) {
        showToast(
          locale === 'vi-VN'
            ? finalNewStatus === 'need_more_info'
              ? 'Đã yêu cầu người dùng bổ sung thông tin!'
              : finalNewStatus === 'resolved'
              ? 'Đã giải quyết khiếu nại thành công!'
              : finalNewStatus === 'rejected'
              ? 'Đã từ chối khiếu nại!'
              : 'Đã cập nhật khiếu nại thành công!'
            : finalNewStatus === 'need_more_info'
            ? 'Requested additional information from user!'
            : finalNewStatus === 'resolved'
            ? 'Dispute resolved successfully!'
            : finalNewStatus === 'rejected'
            ? 'Dispute rejected!'
            : 'Dispute record updated successfully!'
        );
        setAdminMessage('');
        setTargetStatus('');
        // Update local selectedDispute directly from res.dispute
        if (res.dispute) {
          setSelectedDispute(res.dispute);
        }
      } else {
        const errorMsg = getLocalizedErrorMessage(res.error, locale, t, 'common.errorOccurred');
        setActionError(errorMsg);
      }
    } finally {
      setActionSubmitting(false);
    }
  };

  const isVi = locale === 'vi-VN';

  return (
    <div id="admin-disputes-page" className="space-y-6 pb-12">
      {/* Toast */}
      {toastMessage && (
        <div className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-xl bg-slate-900 text-white shadow-xl border border-emerald-500/50 text-xs font-semibold animate-in fade-in slide-in-from-bottom-2">
          <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            {t('adminDisputes.title')}
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
            {t('adminDisputes.subtitle')}
          </p>
        </div>

        <div className="flex items-center gap-2">
          <span className="px-3 py-1.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-700 dark:text-slate-300 text-xs font-medium">
            {isVi ? `Tổng số hồ sơ: ${disputes.length}` : `Total Cases: ${disputes.length}`}
          </span>
        </div>
      </div>

      {/* Filters & Search */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        <div className="relative flex-1 max-w-md">
          <input
            type="text"
            placeholder={isVi ? "Tìm mã khiếu nại, TxRef, tên, email..." : "Search dispute #, TxRef, name, email..."}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full h-9 pl-9 pr-3 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-800 dark:text-slate-200 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
          />
          <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
        </div>

        <div className="flex items-center gap-2">
          <Select
            value={statusFilter}
            onChange={(val) => setStatusFilter(val)}
            size="sm"
            variant="compact"
            className="w-48"
          >
            <option value="all">{isVi ? 'Tất cả trạng thái' : 'All Statuses'}</option>
            <option value="open_group">{isVi ? 'Cần rà soát' : 'Requires Review'}</option>
            <option value="pending">{isVi ? 'Chờ tiếp nhận' : 'Pending'}</option>
            <option value="in_review">{isVi ? 'Đang rà soát' : 'In Review'}</option>
            <option value="need_more_info">{isVi ? 'Cần thêm thông tin' : 'Need More Info'}</option>
            <option value="resolved">{isVi ? 'Đã giải quyết' : 'Resolved'}</option>
            <option value="rejected">{isVi ? 'Từ chối' : 'Rejected'}</option>
          </Select>
        </div>
      </div>

      {/* Disputes List */}
      <div className="space-y-3">
        {isLoading ? (
          <div className="flex items-center justify-center py-12 gap-2 text-slate-500 text-xs">
            <RefreshCw className="h-4 w-4 animate-spin text-purple-600" />
            <span>{isVi ? 'Đang tải danh sách khiếu nại...' : 'Loading transaction disputes...'}</span>
          </div>
        ) : loadError ? (
          <div className="p-8 text-center bg-rose-50/50 dark:bg-rose-950/20 rounded-2xl border border-rose-200 dark:border-rose-900/50 space-y-3">
            <div className="w-10 h-10 mx-auto rounded-xl bg-rose-100 dark:bg-rose-900/40 text-rose-600 dark:text-rose-400 flex items-center justify-center">
              <AlertCircle className="w-5 h-5" />
            </div>
            <h3 className="text-sm font-bold text-rose-900 dark:text-rose-200">
              {isVi ? 'Không thể tải danh sách khiếu nại' : 'Failed to load transaction disputes'}
            </h3>
            <p className="text-xs text-rose-700 dark:text-rose-400 max-w-md mx-auto">
              {loadError}
            </p>
            <Button
              size="sm"
              variant="outline"
              onClick={() => refresh()}
              className="text-xs font-bold gap-1.5 cursor-pointer mt-2 border-rose-300 dark:border-rose-800 text-rose-800 dark:text-rose-300 hover:bg-rose-100/50"
            >
              <RefreshCw className="w-3.5 h-3.5" />
              <span>{isVi ? 'Thử lại' : 'Retry'}</span>
            </Button>
          </div>
        ) : filteredDisputes.length === 0 ? (
          <EmptyState
            icon={Inbox}
            title={isVi ? 'Chưa có khiếu nại giao dịch nào' : 'No transaction disputes yet'}
            description={
              isVi
                ? 'Khi người dùng gửi hồ sơ khiếu nại giao dịch, danh sách sẽ hiển thị tại đây để ban quản trị xử lý.'
                : 'When users submit transaction disputes, the list will appear here for administrative review.'
            }
          />
        ) : (
          filteredDisputes.map((d) => {
            const badge = getStatusBadge(d.status);

            return (
              <div
                key={d.id}
                onClick={() => setSelectedDispute(d)}
                className="p-4 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs hover:bg-slate-50/50 dark:hover:bg-slate-800/40 transition cursor-pointer space-y-2.5"
              >
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-xs font-semibold text-slate-800 dark:text-slate-200">
                      {d.disputeCode || d.id}
                    </span>
                    <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-purple-50 text-purple-700 dark:bg-purple-950/40 dark:text-purple-300 border border-purple-200/60 dark:border-purple-800/40">
                      {getReasonLabel(d.reason)}
                    </span>
                    <span className={`px-2.5 py-0.5 rounded-full text-xs font-medium border ${badge.className}`}>
                      {badge.label}
                    </span>
                    {d.attachments && d.attachments.length > 0 && (
                      <span className="text-xs text-slate-400">
                        • {d.attachments.length} {isVi ? 'chứng từ' : 'attachments'}
                      </span>
                    )}
                  </div>

                  <span className="font-mono text-xs text-slate-400">
                    {formatDate(d.createdAt)}
                  </span>
                </div>

                <div className="flex items-center justify-between">
                  <div className="space-y-0.5 min-w-0 flex-1 pr-4">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-sm text-slate-900 dark:text-white">
                        Tx #{d.transactionRef}
                      </span>
                      {d.transactionTitle && (
                        <span className="text-xs text-slate-500 dark:text-slate-400 truncate">
                          ({d.transactionTitle})
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-slate-500 dark:text-slate-400 line-clamp-1">
                      "{d.description}"
                    </p>
                  </div>

                  <span className="text-sm font-semibold text-rose-600 dark:text-rose-400 font-mono tabular-nums shrink-0">
                    {formatMoney(d.transactionAmount, locale)}
                  </span>
                </div>

                <div className="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-slate-100 dark:border-slate-800 text-xs text-slate-500">
                  <div className="flex items-center gap-3">
                    <span>
                      {isVi ? 'Người gửi: ' : 'Sender: '}
                      <strong className="text-slate-700 dark:text-slate-300 font-mono">
                        {d.userName ? `${d.userName} (${d.userId})` : `User ID: ${d.userId}`}
                      </strong>
                    </span>
                  </div>

                  <div className="flex items-center gap-1 text-slate-600 dark:text-slate-300 font-medium text-xs">
                    <span>{isVi ? 'Xem & xử lý' : 'Review & Process'}</span>
                    <ChevronRight className="w-3.5 h-3.5" />
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* DETAIL & RESOLUTION MODAL */}
      {selectedDispute && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-3xl bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-200 dark:border-slate-800 shadow-xl space-y-5 max-h-[90vh] overflow-y-auto">
            {/* Header */}
            <div className="flex items-start justify-between gap-3 pb-3 border-b border-slate-100 dark:border-slate-800">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <span className="font-mono text-xs font-semibold text-slate-500">
                    {selectedDispute.disputeCode || selectedDispute.id}
                  </span>
                  <span className={`px-2.5 py-0.5 rounded-full text-xs font-medium border ${getStatusBadge(selectedDispute.status).className}`}>
                    {getStatusBadge(selectedDispute.status).label}
                  </span>
                  <span className="text-xs text-purple-700 dark:text-purple-300">
                    • {getReasonLabel(selectedDispute.reason)}
                  </span>
                </div>
                <h2 className="text-base font-bold text-slate-900 dark:text-white">
                  {isVi
                    ? `Chi tiết khiếu nại giao dịch #${selectedDispute.transactionRef}`
                    : `Dispute Details for Transaction #${selectedDispute.transactionRef}`}
                </h2>
                <div className="flex flex-wrap items-center gap-3 text-[11px] text-slate-500 dark:text-slate-400 font-mono mt-1">
                  <span>{isVi ? 'Người dùng (ID):' : 'User (ID):'} <strong>{selectedDispute.userName ? `${selectedDispute.userName} (${selectedDispute.userId})` : selectedDispute.userId}</strong></span>
                  <span>{isVi ? 'Ngày nộp: ' : 'Submitted: '}{formatDate(selectedDispute.createdAt)}</span>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setSelectedDispute(null)}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Dispute Info & Timeline */}
            <div className="space-y-4 text-xs">
              {/* Real Transaction Card */}
              <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/60 dark:border-slate-800 space-y-1.5">
                <div className="flex items-center justify-between text-xs text-slate-500 font-medium">
                  <span>{isVi ? 'Dữ liệu giao dịch đối ứng' : 'Counterparty Transaction Record'}</span>
                  <span className="font-mono">Tx ID: {selectedDispute.transactionId}</span>
                </div>
                <div className="flex items-center justify-between">
                  <div>
                    <span className="font-semibold text-sm text-slate-900 dark:text-white block">
                      {selectedDispute.transactionTitle ||
                        (isVi ? 'Giao dịch SpendWise' : 'SpendWise Transaction')}
                    </span>
                    <span className="text-xs text-slate-500 font-mono">
                      Ref: #{selectedDispute.transactionRef} • {isVi ? 'Ngày' : 'Date'}: {selectedDispute.transactionDate}
                    </span>
                  </div>
                  <span className="text-sm font-semibold text-rose-600 dark:text-rose-400 font-mono tabular-nums">
                    {formatMoney(selectedDispute.transactionAmount, locale)}
                  </span>
                </div>
              </div>

              {/* History Timeline */}
              <div className="space-y-3">
                <div className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {isVi
                    ? `Lịch sử trao đổi (${selectedDispute.history?.length || 1})`
                    : `Conversation History (${selectedDispute.history?.length || 1})`}
                </div>

                <div className="space-y-3">
                  {(selectedDispute.history || []).map((h, idx) => {
                    const isAdmin = h.actorRole === 'SystemAdmin';
                    return (
                      <div
                        key={h.id || idx}
                        className={`p-3.5 rounded-xl border space-y-2 ${
                          isAdmin
                            ? 'bg-blue-50/50 dark:bg-blue-950/20 border-blue-100 dark:border-blue-900/40 text-blue-950 dark:text-blue-200'
                            : 'bg-slate-50 dark:bg-slate-800/40 border-slate-200/60 dark:border-slate-800 text-slate-900 dark:text-slate-200'
                        }`}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <div className="flex items-center gap-2">
                            <span
                              className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                                isAdmin
                                  ? 'bg-blue-600 text-white'
                                  : 'bg-purple-600 text-white'
                              }`}
                            >
                              {isAdmin
                                ? (isVi ? 'Quản trị viên' : 'Admin')
                                : (isVi ? 'Người dùng' : 'User')}
                            </span>
                            <span className="font-semibold text-xs">{h.actorName}</span>
                            {h.newStatus && (
                              <span className="text-xs font-mono text-slate-400">
                                ({isVi ? 'Trạng thái: ' : 'Status: '}{getStatusBadge(h.newStatus).label})
                              </span>
                            )}
                          </div>
                          <span className="text-xs text-slate-400 font-mono">
                            {formatDate(h.timestamp)}
                          </span>
                        </div>

                        <p className="text-xs leading-relaxed whitespace-pre-wrap">
                          {h.message}
                        </p>

                        {/* Attachments */}
                        {h.attachments && h.attachments.length > 0 && (
                          <AttachmentList
                            attachments={h.attachments}
                            loadAttachmentBlob={loadAttachmentBlob}
                          />
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* Attachments if not in history */}
              {(!selectedDispute.history || selectedDispute.history.length === 0) && selectedDispute.attachments && (
                <AttachmentList
                  attachments={selectedDispute.attachments}
                  loadAttachmentBlob={loadAttachmentBlob}
                />
              )}

              {/* ADMIN ACTION PANEL */}
              <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-900/80 border border-slate-200 dark:border-slate-800 space-y-3">
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-xs text-slate-800 dark:text-slate-200 flex items-center gap-1.5">
                    <Scale className="w-4 h-4 text-purple-600" />
                    {isVi ? 'Xử lý khiếu nại' : 'Process Dispute'}
                  </span>
                  <span className="text-xs text-slate-400 font-mono">
                    {isVi ? 'Quản trị viên:' : 'Admin:'} {currentUser?.name || 'SystemAdmin'}
                  </span>
                </div>

                {actionError && (
                  <div className="p-2.5 rounded-xl bg-rose-50 text-rose-700 text-xs font-semibold flex items-center gap-2">
                    <AlertCircle className="w-4 h-4 shrink-0" />
                    <span>{actionError}</span>
                  </div>
                )}

                <textarea
                  rows={3}
                  placeholder={
                    isVi
                      ? "Nhập nội dung xử lý, yêu cầu bổ sung thông tin hoặc phản hồi cho người khiếu nại..."
                      : "Enter dispute response, request for information, or notes..."
                  }
                  value={adminMessage}
                  onChange={(e) => setAdminMessage(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-normal resize-none text-xs focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
                />

                <div className="flex flex-wrap items-center justify-between gap-3 pt-1">
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-slate-500 font-medium">
                      {isVi ? 'Chuyển trạng thái:' : 'Update Status:'}
                    </span>
                    <Select
                      value={targetStatus}
                      onChange={(val) => setTargetStatus(val as DisputeStatus)}
                      className="text-xs py-1"
                    >
                      <option value="">{isVi ? '-- Giữ nguyên --' : '-- Keep Current --'}</option>
                      <option value="in_review">{isVi ? 'Đang rà soát' : 'In Review'}</option>
                      <option value="need_more_info">{isVi ? 'Yêu cầu bổ sung thông tin' : 'Need More Info'}</option>
                      <option value="resolved">{isVi ? 'Chấp nhận khiếu nại' : 'Resolved'}</option>
                      <option value="rejected">{isVi ? 'Từ chối khiếu nại' : 'Rejected'}</option>
                    </Select>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => handleAdminAction('need_more_info')}
                      disabled={actionSubmitting}
                      className="text-orange-700 dark:text-orange-300 border-orange-200 dark:border-orange-800 hover:bg-orange-50 dark:hover:bg-orange-950/40 text-xs font-bold cursor-pointer"
                    >
                      <FileQuestion className="w-3.5 h-3.5 mr-1" />
                      {isVi ? 'Yêu cầu bổ sung thông tin' : 'Request Info'}
                    </Button>

                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => handleAdminAction('rejected')}
                      disabled={actionSubmitting}
                      className="text-rose-700 dark:text-rose-300 border-rose-200 dark:border-rose-800 hover:bg-rose-50 dark:hover:bg-rose-950/40 text-xs font-bold cursor-pointer"
                    >
                      <XCircle className="w-3.5 h-3.5 mr-1" />
                      {isVi ? 'Từ chối khiếu nại' : 'Reject Dispute'}
                    </Button>

                    <Button
                      size="sm"
                      onClick={() => handleAdminAction('resolved')}
                      disabled={actionSubmitting}
                      className="bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold gap-1 cursor-pointer"
                    >
                      <CheckCircle2 className="w-3.5 h-3.5" />
                      <span>{isVi ? 'Chấp thuận & Giải quyết' : 'Approve & Resolve'}</span>
                    </Button>
                  </div>
                </div>
              </div>
            </div>

            <div className="flex items-center justify-end pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                variant="outline"
                onClick={() => setSelectedDispute(null)}
                className="cursor-pointer text-xs"
              >
                {t('common.close')}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
