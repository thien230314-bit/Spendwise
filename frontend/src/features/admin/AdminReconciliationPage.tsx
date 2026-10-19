import React, { useState, useMemo, useEffect } from 'react';
import { NavLink } from 'react-router-dom';
import {
  CheckCircle2,
  AlertTriangle,
  Building2,
  Search,
  X,
  Copy,
  Check,
  ArrowUpRight,
  Inbox,
  AlertCircle,
  HelpCircle,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useFinancial } from '../../providers/FinancialProvider';
import { formatDate } from '../../utils/date';
import { formatMoney } from '../../utils/money';
import { Select } from '../../components/ui/Select';
import { EmptyState } from '../../components/ui/EmptyState';
import { AdminBankIntegrationEvent } from '../../types/admin';

export const AdminReconciliationPage: React.FC = () => {
  const { t, locale } = useI18n();
  const isVi = locale === 'vi-VN';
  const {
    bankReconciliationSummary,
    adminBankIntegrationEvents,
    refreshAdminBanking,
  } = useFinancial();

  useEffect(() => {
    void refreshAdminBanking();
  }, [refreshAdminBanking]);

  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [selectedEvent, setSelectedEvent] = useState<AdminBankIntegrationEvent | null>(null);
  const [copiedId, setCopiedId] = useState(false);

  // Map status to: Đã khớp / Chưa khớp / Cần kiểm tra
  const getNormalizedStatus = (evt: AdminBankIntegrationEvent): 'matched' | 'unmatched' | 'review' => {
    if (evt.processingStatus === 'Processed') return 'matched';
    if (evt.processingStatus === 'Unmatched') return 'unmatched';
    return 'review';
  };

  const filteredEvents = useMemo(() => {
    return adminBankIntegrationEvents.filter((evt) => {
      const q = searchQuery.toLowerCase().trim();
      const matchesSearch =
        !q ||
        evt.id.toLowerCase().includes(q) ||
        (evt.provider && evt.provider.toLowerCase().includes(q)) ||
        (evt.externalTransactionId && evt.externalTransactionId.toLowerCase().includes(q)) ||
        (evt.details && evt.details.toLowerCase().includes(q));

      const norm = getNormalizedStatus(evt);
      const matchesStatus =
        statusFilter === 'all' ||
        statusFilter === norm;

      return matchesSearch && matchesStatus;
    });
  }, [adminBankIntegrationEvents, searchQuery, statusFilter]);

  const copyEventId = (id: string) => {
    navigator.clipboard.writeText(id);
    setCopiedId(true);
    setTimeout(() => setCopiedId(false), 2000);
  };

  const getStatusBadge = (norm: 'matched' | 'unmatched' | 'review') => {
    switch (norm) {
      case 'matched':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40">
            <CheckCircle2 className="w-3 h-3" />
            {isVi ? 'Đã khớp' : 'Matched'}
          </span>
        );
      case 'unmatched':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 border border-amber-200/60 dark:border-amber-800/40">
            <AlertTriangle className="w-3 h-3" />
            {isVi ? 'Chưa khớp' : 'Unmatched'}
          </span>
        );
      case 'review':
      default:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-sky-50 text-sky-700 dark:bg-sky-950/40 dark:text-sky-400 border border-sky-200/60 dark:border-sky-800/40">
            <AlertCircle className="w-3 h-3" />
            {isVi ? 'Cần kiểm tra' : 'Needs Review'}
          </span>
        );
    }
  };

  const getEventAmount = (evt: AdminBankIntegrationEvent): number | null => {
    const payload = evt.payload as Record<string, any> | undefined;
    if (payload && typeof payload.amount === 'number') return payload.amount;
    if (payload && typeof payload.transferAmount === 'number') return payload.transferAmount;
    return null;
  };

  const matchedCount = useMemo(() => {
    return adminBankIntegrationEvents.filter((e) => getNormalizedStatus(e) === 'matched').length;
  }, [adminBankIntegrationEvents]);

  const unmatchedCount = useMemo(() => {
    return adminBankIntegrationEvents.filter((e) => getNormalizedStatus(e) === 'unmatched').length;
  }, [adminBankIntegrationEvents]);

  const reviewCount = useMemo(() => {
    return adminBankIntegrationEvents.filter((e) => getNormalizedStatus(e) === 'review').length;
  }, [adminBankIntegrationEvents]);

  return (
    <div id="admin-reconciliation-page" className="space-y-6">
      {/* Page Title & Plain-language Description */}
      <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            {t('adminReconciliation.title')}
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 max-w-2xl leading-relaxed">
            {isVi
              ? 'Kiểm tra và đối soát các giao dịch đồng bộ từ ngân hàng với hệ thống SpendWise.'
              : 'Verify and reconcile bank transactions with SpendWise records.'}
          </p>
        </div>

        <NavLink
          to="/admin/banking"
          className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-700 dark:text-slate-300 text-xs font-medium hover:bg-slate-50 dark:hover:bg-slate-800 transition shrink-0"
        >
          <Building2 className="w-4 h-4 text-slate-400" />
          <span>{isVi ? 'Quản lý ngân hàng' : 'Banking Hub'}</span>
          <ArrowUpRight className="w-3.5 h-3.5 text-slate-400" />
        </NavLink>
      </div>

      {/* Honest Summary Strip: Đã khớp / Chưa khớp / Cần kiểm tra */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <div className="p-3.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          <span className="text-xs text-slate-500 dark:text-slate-400 block">
            {isVi ? 'Tổng số sự kiện' : 'Total Events'}
          </span>
          <span className="text-lg font-bold text-slate-900 dark:text-white mt-0.5 block">
            {adminBankIntegrationEvents.length}
          </span>
        </div>

        <div className="p-3.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          <span className="text-xs text-slate-500 dark:text-slate-400 block">
            {isVi ? 'Đã khớp' : 'Matched'}
          </span>
          <span className="text-lg font-bold text-emerald-600 dark:text-emerald-400 mt-0.5 block">
            {matchedCount}
          </span>
        </div>

        <div className="p-3.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          <span className="text-xs text-slate-500 dark:text-slate-400 block">
            {isVi ? 'Chưa khớp' : 'Unmatched'}
          </span>
          <span className="text-lg font-bold text-amber-600 dark:text-amber-400 mt-0.5 block">
            {unmatchedCount}
          </span>
        </div>

        <div className="p-3.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          <span className="text-xs text-slate-500 dark:text-slate-400 block">
            {isVi ? 'Cần kiểm tra' : 'Needs Review'}
          </span>
          <span className="text-lg font-bold text-sky-600 dark:text-sky-400 mt-0.5 block">
            {reviewCount}
          </span>
        </div>
      </div>

      {/* Search & Filter Bar */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        <div className="relative flex-1 max-w-md">
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={isVi ? 'Tìm theo mã tham chiếu, ngân hàng, nội dung...' : 'Search by reference, bank, content...'}
            className="w-full h-9 pl-9 pr-3 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-800 dark:text-slate-200 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
          />
          <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
          {searchQuery && (
            <button
              onClick={() => setSearchQuery('')}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-xs"
            >
              ✕
            </button>
          )}
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
            <option value="matched">{isVi ? 'Đã khớp' : 'Matched'}</option>
            <option value="unmatched">{isVi ? 'Chưa khớp' : 'Unmatched'}</option>
            <option value="review">{isVi ? 'Cần kiểm tra' : 'Needs Review'}</option>
          </Select>
        </div>
      </div>

      {/* Events Table / Empty State */}
      {filteredEvents.length === 0 ? (
        <EmptyState
          icon={Inbox}
          title={isVi ? 'Không tìm thấy sự kiện đối soát nào' : 'No reconciliation events found'}
          description={
            searchQuery || statusFilter !== 'all'
              ? (isVi ? 'Không có sự kiện nào khớp với bộ lọc hiện tại. Thử xóa tìm kiếm.' : 'No events match your current filter criteria.')
              : (isVi ? 'Hệ thống chưa ghi nhận sự kiện giao dịch ngân hàng nào cần đối soát.' : 'No bank reconciliation events have been recorded yet.')
          }
        />
      ) : (
        <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-xs">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 dark:bg-slate-800/50 border-b border-slate-200 dark:border-slate-800 text-slate-600 dark:text-slate-400 font-medium">
                <tr>
                  <th className="px-4 py-3">{isVi ? 'Số tiền' : 'Amount'}</th>
                  <th className="px-4 py-3">{isVi ? 'Mã tham chiếu' : 'Reference'}</th>
                  <th className="px-4 py-3">{isVi ? 'Ngân hàng xác nhận' : 'Bank Confirmation'}</th>
                  <th className="px-4 py-3">{isVi ? 'Giao dịch SpendWise' : 'SpendWise Transaction'}</th>
                  <th className="px-4 py-3">{isVi ? 'Trạng thái' : 'Status'}</th>
                  <th className="px-4 py-3 text-right">{isVi ? 'Thời gian' : 'Time'}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800 text-slate-700 dark:text-slate-300">
                {filteredEvents.map((evt) => {
                  const norm = getNormalizedStatus(evt);
                  const amount = getEventAmount(evt);

                  return (
                    <tr
                      key={evt.id}
                      onClick={() => setSelectedEvent(evt)}
                      className="hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer transition"
                    >
                      <td className="px-4 py-3 font-semibold text-slate-900 dark:text-white whitespace-nowrap">
                        {amount !== null ? formatMoney(amount, locale) : '—'}
                      </td>
                      <td className="px-4 py-3 font-mono text-slate-700 dark:text-slate-300">
                        {evt.externalTransactionId || evt.externalEventId || '—'}
                      </td>
                      <td className="px-4 py-3 font-medium text-slate-800 dark:text-slate-200">
                        {evt.provider || (isVi ? 'Ngân hàng' : 'Bank')}
                      </td>
                      <td className="px-4 py-3 text-slate-600 dark:text-slate-400 max-w-xs truncate">
                        {evt.details || (norm === 'matched' ? (isVi ? 'Đã ghi nhận vào hệ thống' : 'Recorded in system') : '—')}
                      </td>
                      <td className="px-4 py-3">
                        {getStatusBadge(norm)}
                      </td>
                      <td className="px-4 py-3 text-right text-slate-500 font-mono whitespace-nowrap">
                        {evt.receivedAt ? formatDate(evt.receivedAt, locale) : '—'}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div className="px-4 py-2.5 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{isVi ? 'Bấm vào từng dòng để xem thông tin chi tiết' : 'Click any row to view details'}</span>
            <span>{filteredEvents.length} / {adminBankIntegrationEvents.length} {isVi ? 'sự kiện' : 'events'}</span>
          </div>
        </div>
      )}

      {/* Event Details Modal */}
      {selectedEvent && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-xl bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-200 dark:border-slate-800 shadow-xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-800">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {isVi ? 'Chi tiết giao dịch đối soát' : 'Reconciliation Details'}
              </h3>
              <button
                type="button"
                onClick={() => setSelectedEvent(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 p-1"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-3 text-xs">
              {/* Primary details emphasizing: amount, reference, bank confirmation, SpendWise transaction, status, time */}
              <div className="grid grid-cols-2 gap-3 p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800">
                <div>
                  <span className="text-slate-400 block">{isVi ? 'Số tiền' : 'Amount'}</span>
                  <span className="text-base font-bold text-slate-900 dark:text-white mt-0.5 block">
                    {getEventAmount(selectedEvent) !== null ? formatMoney(getEventAmount(selectedEvent)!, locale) : '—'}
                  </span>
                </div>
                <div>
                  <span className="text-slate-400 block">{isVi ? 'Trạng thái' : 'Status'}</span>
                  <div className="mt-1">{getStatusBadge(getNormalizedStatus(selectedEvent))}</div>
                </div>
                <div>
                  <span className="text-slate-400 block">{isVi ? 'Mã tham chiếu' : 'Reference'}</span>
                  <span className="font-mono text-slate-800 dark:text-slate-200 mt-0.5 block">
                    {selectedEvent.externalTransactionId || selectedEvent.externalEventId || '—'}
                  </span>
                </div>
                <div>
                  <span className="text-slate-400 block">{isVi ? 'Ngân hàng xác nhận' : 'Bank Confirmation'}</span>
                  <span className="font-semibold text-slate-800 dark:text-slate-200 mt-0.5 block">
                    {selectedEvent.provider}
                  </span>
                </div>
                <div className="col-span-2">
                  <span className="text-slate-400 block">{isVi ? 'Giao dịch SpendWise' : 'SpendWise Transaction'}</span>
                  <span className="text-slate-700 dark:text-slate-300 mt-0.5 block">
                    {selectedEvent.details || (isVi ? 'Đã đồng bộ vào sổ giao dịch hệ thống' : 'Synced into system transaction ledger')}
                  </span>
                </div>
                <div className="col-span-2">
                  <span className="text-slate-400 block">{isVi ? 'Thời gian nhận' : 'Received At'}</span>
                  <span className="font-mono text-slate-700 dark:text-slate-300 mt-0.5 block">
                    {selectedEvent.receivedAt ? formatDate(selectedEvent.receivedAt, locale) : '—'}
                  </span>
                </div>
              </div>

              {/* Collapsed Technical Details Section */}
              <details className="border border-slate-200 dark:border-slate-800 rounded-xl overflow-hidden bg-slate-50/50 dark:bg-slate-800/30">
                <summary className="px-4 py-2.5 text-xs font-semibold text-slate-700 dark:text-slate-300 cursor-pointer select-none flex items-center justify-between hover:bg-slate-100 dark:hover:bg-slate-800/60">
                  <span>{isVi ? 'Thông tin kỹ thuật' : 'Technical Details'}</span>
                  <span className="text-[11px] text-slate-400 font-normal">
                    {isVi ? 'Mã sự kiện, số lần thử lại & dữ liệu thô' : 'Event ID, retries & raw payload'}
                  </span>
                </summary>

                <div className="p-3 border-t border-slate-200 dark:border-slate-800 space-y-3">
                  <div className="flex items-center justify-between">
                    <div>
                      <span className="text-slate-400 block text-[11px]">{isVi ? 'Mã sự kiện webhook' : 'Webhook Event ID'}</span>
                      <span className="font-mono text-slate-700 dark:text-slate-300 text-xs">{selectedEvent.id}</span>
                    </div>
                    <button
                      type="button"
                      onClick={() => copyEventId(selectedEvent.id)}
                      className="inline-flex items-center gap-1 text-[11px] text-slate-500 hover:text-slate-800 dark:hover:text-slate-200"
                    >
                      {copiedId ? <Check className="w-3 h-3 text-emerald-500" /> : <Copy className="w-3 h-3" />}
                      {copiedId ? (isVi ? 'Đã chép' : 'Copied') : (isVi ? 'Sao chép mã' : 'Copy ID')}
                    </button>
                  </div>

                  {selectedEvent.duplicateDeliveryCount > 0 && (
                    <div className="p-2.5 rounded-lg bg-amber-50/60 dark:bg-amber-950/20 border border-amber-200/50 dark:border-amber-800/30 text-xs text-amber-800 dark:text-amber-300">
                      <span className="font-medium">{isVi ? 'Gửi trùng lặp: ' : 'Duplicate Deliveries: '}</span>
                      {isVi
                        ? `Nhận lặp ${selectedEvent.duplicateDeliveryCount} lần. Đã áp dụng cơ chế idempotent để bảo vệ dữ liệu.`
                        : `Received ${selectedEvent.duplicateDeliveryCount} times. Handled by idempotency logic.`}
                    </div>
                  )}

                  {selectedEvent.payload && (
                    <div>
                      <span className="text-slate-400 block text-[11px] mb-1">
                        {isVi ? 'Bản tin webhook thô (Payload)' : 'Raw Webhook Payload'}
                      </span>
                      <div className="p-3 rounded-lg bg-slate-950 text-emerald-400 font-mono text-[11px] overflow-x-auto border border-slate-800 max-h-48">
                        <pre>{JSON.stringify(selectedEvent.payload, null, 2)}</pre>
                      </div>
                    </div>
                  )}
                </div>
              </details>
            </div>

            <div className="pt-3 border-t border-slate-200 dark:border-slate-800 flex justify-end">
              <button
                type="button"
                onClick={() => setSelectedEvent(null)}
                className="px-4 py-2 text-xs font-medium rounded-xl bg-slate-900 text-white dark:bg-white dark:text-slate-900 hover:opacity-90 transition"
              >
                {isVi ? 'Đóng' : 'Close'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminReconciliationPage;
