import React, { useState, useMemo } from 'react';
import {
  Search,
  X,
  ShieldCheck,
  Inbox,
  RefreshCw,
  AlertCircle,
  KeyRound,
  FileText,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { Select } from '../../components/ui/Select';
import { Button } from '../../components/ui/Button';
import { EmptyState } from '../../components/ui/EmptyState';
import { casesApiService, AdminTransactionDto } from '../../services/casesApiService';
import { ApiError } from '../../lib/apiError';

export const AdminTransactionsPage: React.FC = () => {
  const { t, locale } = useI18n();
  const isVi = locale === 'vi-VN';
  const { currentUser } = useAuth();

  const [transactions, setTransactions] = useState<AdminTransactionDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [hasSearched, setHasSearched] = useState(false);

  // Sensitive reason modal & state
  const [isReasonModalOpen, setIsReasonModalOpen] = useState(false);
  const [accessReason, setAccessReason] = useState('');
  const [reasonError, setReasonError] = useState<string | null>(null);

  // Table filters & view
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [selectedTx, setSelectedTx] = useState<AdminTransactionDto | null>(null);

  const formatCurrency = (val: number, currency = 'VND') => {
    return new Intl.NumberFormat(locale === 'vi-VN' ? 'vi-VN' : 'en-US', {
      style: 'currency',
      currency: currency || 'VND',
      maximumFractionDigits: 0,
    }).format(val);
  };

  const formatDate = (dateStr: string) => {
    try {
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return dateStr;
      return d.toLocaleString(locale === 'vi-VN' ? 'vi-VN' : 'en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      });
    } catch {
      return dateStr;
    }
  };

  const handleOpenSearchModal = () => {
    setReasonError(null);
    setIsReasonModalOpen(true);
  };

  const executeSensitiveSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedReason = accessReason.trim();
    if (!trimmedReason || trimmedReason.length < 5) {
      setReasonError(
        isVi
          ? 'Lý do truy cập nhạy cảm phải có độ dài từ 5 đến 1000 ký tự (theo quy định kiểm toán hệ thống).'
          : 'A meaningful access justification of at least 5 characters is required for audit logs.'
      );
      return;
    }

    if (!currentUser || currentUser.role !== 'SystemAdmin') {
      setReasonError(isVi ? 'Yêu cầu quyền Quản trị viên hệ thống.' : 'SystemAdmin role required.');
      return;
    }

    setIsLoading(true);
    setReasonError(null);
    setLoadError(null);

    try {
      const data = await casesApiService.searchAdminTransactions(trimmedReason);
      setTransactions(data || []);
      setHasSearched(true);
      setIsReasonModalOpen(false);
    } catch (err) {
      console.error('Failed to search sensitive transactions:', err);
      const apiErr = err as ApiError;
      const msg =
        apiErr?.detail ||
        apiErr?.title ||
        (isVi ? 'Lỗi khi tra cứu giao dịch nhạy cảm.' : 'Failed to retrieve sensitive transactions.');
      setReasonError(msg);
      setLoadError(msg);
    } finally {
      setIsLoading(false);
    }
  };

  // Distinct types for filter dropdown
  const transactionTypes = useMemo(() => {
    const set = new Set<string>();
    transactions.forEach((tx) => {
      if (tx.type) set.add(tx.type);
    });
    return Array.from(set).sort();
  }, [transactions]);

  const filtered = useMemo(() => {
    return transactions.filter((tx) => {
      const q = searchQuery.toLowerCase().trim();
      const txTypeStr = String(tx.type ?? '');
      const txStatusStr = String(tx.status ?? '');
      const txIdStr = String(tx.id ?? '');
      const txLedgerStr = String(tx.ledgerTransactionId ?? '');
      const txBizStr = String(tx.relatedBusinessId ?? '');

      const matchesSearch =
        !q ||
        txIdStr.toLowerCase().includes(q) ||
        txLedgerStr.toLowerCase().includes(q) ||
        txBizStr.toLowerCase().includes(q) ||
        txTypeStr.toLowerCase().includes(q) ||
        txStatusStr.toLowerCase().includes(q);

      const matchesStatus =
        statusFilter === 'all' ||
        txStatusStr.toLowerCase() === statusFilter.toLowerCase();

      const matchesType =
        typeFilter === 'all' ||
        txTypeStr.toLowerCase() === typeFilter.toLowerCase();

      return matchesSearch && matchesStatus && matchesType;
    });
  }, [transactions, searchQuery, statusFilter, typeFilter]);

  return (
    <div id="admin-transactions-page" className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            {t('adminTransactions.title')}
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
            {isVi
              ? 'Tra cứu giao dịch tài chính hệ thống qua cơ chế xác thực kiểm toán nhạy cảm (POST /api/admin/transactions/search).'
              : 'Search system financial transactions via sensitive access audit compliance.'}
          </p>
        </div>

        <Button
          size="sm"
          onClick={handleOpenSearchModal}
          disabled={isLoading}
          className="bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold gap-1.5 cursor-pointer self-start sm:self-auto shadow-xs"
        >
          {isLoading ? (
            <RefreshCw className="w-3.5 h-3.5 animate-spin" />
          ) : (
            <KeyRound className="w-3.5 h-3.5" />
          )}
          <span>
            {hasSearched
              ? (isVi ? 'Tra cứu lại (Lý do mới)' : 'New Authorized Search')
              : (isVi ? 'Xác thực & Tra cứu giao dịch' : 'Authorize & Search Transactions')}
          </span>
        </Button>
      </div>

      {/* Immutable Rule Banner */}
      <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-800 text-xs text-slate-700 dark:text-slate-300 flex items-center justify-between gap-2.5">
        <div className="flex items-center gap-2.5">
          <ShieldCheck className="w-4 h-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
          <span>{t('adminTransactions.immutableNotice')}</span>
        </div>
        <span className="text-[11px] font-mono text-slate-400 shrink-0">
          POST /admin/transactions/search
        </span>
      </div>

      {/* Search & Filter Controls (only active when data has been retrieved) */}
      {hasSearched && (
        <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
          <div className="relative flex-1 max-w-md">
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={isVi ? 'Tìm theo ID giao dịch, Ledger ID, Business ID...' : 'Search transaction ID, Ledger ID, Business ID...'}
              className="w-full h-9 pl-9 pr-3 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-800 dark:text-slate-200 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
            />
            <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            {searchQuery && (
              <button
                onClick={() => setSearchQuery('')}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-xs cursor-pointer"
              >
                ✕
              </button>
            )}
          </div>

          <div className="flex items-center gap-2">
            <Select
              value={typeFilter}
              onChange={(val) => setTypeFilter(val)}
              size="sm"
              variant="compact"
              className="w-44"
            >
              <option value="all">{isVi ? 'Tất cả loại giao dịch' : 'All Types'}</option>
              {transactionTypes.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </Select>

            <Select
              value={statusFilter}
              onChange={(val) => setStatusFilter(val)}
              size="sm"
              variant="compact"
              className="w-40"
            >
              <option value="all">{isVi ? 'Tất cả trạng thái' : 'All Statuses'}</option>
              <option value="Completed">{isVi ? 'Completed (Hoàn tất)' : 'Completed'}</option>
              <option value="Pending">{isVi ? 'Pending (Đang chờ)' : 'Pending'}</option>
              <option value="Processing">{isVi ? 'Processing (Đang xử lý)' : 'Processing'}</option>
              <option value="Failed">{isVi ? 'Failed (Thất bại)' : 'Failed'}</option>
              <option value="Cancelled">{isVi ? 'Cancelled (Đã hủy)' : 'Cancelled'}</option>
              <option value="Reversed">{isVi ? 'Reversed (Đã hoàn/đảo)' : 'Reversed'}</option>
            </Select>
          </div>
        </div>
      )}

      {/* Error state */}
      {loadError && (
        <div className="p-4 rounded-2xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/50 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 text-xs">
          <div className="flex items-center gap-2 text-rose-800 dark:text-rose-300 font-medium">
            <AlertCircle className="w-4 h-4 text-rose-600 shrink-0" />
            <span>{loadError}</span>
          </div>
          <Button
            size="sm"
            variant="outline"
            onClick={handleOpenSearchModal}
            className="text-xs font-semibold gap-1.5 border-rose-300 dark:border-rose-800 text-rose-800 dark:text-rose-300 hover:bg-rose-100/50 cursor-pointer shrink-0"
          >
            <KeyRound className="w-3.5 h-3.5" />
            <span>{isVi ? 'Xác thực lại' : 'Re-authenticate'}</span>
          </Button>
        </div>
      )}

      {/* Table / EmptyState */}
      {!hasSearched && !isLoading ? (
        <div className="p-12 text-center bg-white dark:bg-slate-900 rounded-2xl border border-dashed border-slate-200 dark:border-slate-800 space-y-4">
          <div className="w-12 h-12 mx-auto rounded-2xl bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400 flex items-center justify-center">
            <KeyRound className="w-6 h-6 text-emerald-600 dark:text-emerald-400" />
          </div>
          <div className="space-y-1.5 max-w-md mx-auto">
            <h3 className="text-sm font-bold text-slate-900 dark:text-white">
              {isVi ? 'Yêu cầu xác nhận lý do kiểm toán' : 'Audit Justification Required'}
            </h3>
            <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
              {isVi
                ? 'Dữ liệu giao dịch tài chính là thông tin nhạy cảm. Để bảo vệ sổ cái và lưu vết kiểm toán (AuditLog), quản trị viên cần nhập lý do truy cập trước khi nạp dữ liệu.'
                : 'Financial transaction history is sensitive data. An audit reason must be provided to query and review transactions.'}
            </p>
          </div>
          <Button
            size="sm"
            onClick={handleOpenSearchModal}
            className="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs gap-1.5 cursor-pointer shadow-xs"
          >
            <KeyRound className="w-3.5 h-3.5" />
            <span>{isVi ? 'Nhập lý do & Tra cứu ngay' : 'Provide Reason & Search'}</span>
          </Button>
        </div>
      ) : isLoading ? (
        <div className="flex items-center justify-center py-16 gap-2 text-slate-500 text-xs">
          <RefreshCw className="h-4 w-4 animate-spin text-emerald-600" />
          <span>{isVi ? 'Đang xác thực kiểm toán và tra cứu giao dịch...' : 'Auditing access and searching transactions...'}</span>
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={Inbox}
          title={
            locale === 'vi-VN'
              ? 'Không tìm thấy giao dịch nào phù hợp.'
              : 'No transactions found.'
          }
          description={
            locale === 'vi-VN'
              ? 'Thử thay đổi bộ lọc tìm kiếm hoặc kiểm tra lại điều kiện tra cứu.'
              : 'Try adjusting your search query or filter options.'
          }
        />
      ) : (
        <div className="rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 dark:bg-slate-800/50 border-b border-slate-200 dark:border-slate-800 text-slate-600 dark:text-slate-400 font-medium">
                <tr>
                  <th className="px-4 py-3.5">{isVi ? 'Mã giao dịch (ID)' : 'Transaction ID'}</th>
                  <th className="px-4 py-3.5">{isVi ? 'Phân loại' : 'Type'}</th>
                  <th className="px-4 py-3.5">{isVi ? 'Số tiền' : 'Amount'}</th>
                  <th className="px-4 py-3.5">{isVi ? 'Mã sổ cái (Ledger ID)' : 'Ledger Transaction ID'}</th>
                  <th className="px-4 py-3.5">{isVi ? 'Trạng thái' : 'Status'}</th>
                  <th className="px-4 py-3.5 text-right">{isVi ? 'Thời gian tạo' : 'Created At'}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800 font-medium text-slate-700 dark:text-slate-300">
                {filtered.map((tx) => (
                  <tr
                    key={tx.id}
                    onClick={() => setSelectedTx(tx)}
                    className="hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer transition-colors"
                  >
                    <td className="px-4 py-3.5 font-mono text-[11px] text-slate-900 dark:text-white font-bold">
                      {tx.id}
                    </td>
                    <td className="px-4 py-3.5 text-xs text-slate-700 dark:text-slate-300">
                      <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
                        {tx.type}
                      </span>
                    </td>
                    <td className="px-4 py-3.5 font-bold text-slate-900 dark:text-white whitespace-nowrap">
                      {formatCurrency(tx.amount, tx.currency)}
                    </td>
                    <td className="px-4 py-3.5 font-mono text-[11px] text-slate-500 dark:text-slate-400">
                      {tx.ledgerTransactionId}
                    </td>
                    <td className="px-4 py-3.5">
                      <span
                        className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold ${
                          tx.status === 'Completed'
                            ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                            : tx.status === 'Pending' || tx.status === 'Processing'
                            ? 'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 border border-amber-200/60 dark:border-amber-800/40'
                            : 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60 dark:border-rose-800/40'
                        }`}
                      >
                        {tx.status}
                      </span>
                    </td>
                    <td className="px-4 py-3.5 text-right font-mono text-[11px] text-slate-400 whitespace-nowrap">
                      {formatDate(tx.createdAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="px-4 py-2.5 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{isVi ? 'Bấm vào từng dòng để xem chi tiết giao dịch và liên kết nghiệp vụ' : 'Click any row for transaction details and linkages'}</span>
            <span>{filtered.length} / {transactions.length} {isVi ? 'giao dịch' : 'transactions'}</span>
          </div>
        </div>
      )}

      {/* Sensitive Search Reason Modal */}
      {isReasonModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xl p-6 space-y-4 text-xs">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  {isVi ? 'Xác nhận lý do tra cứu nhạy cảm' : 'Sensitive Access Authorization'}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setIsReasonModalOpen(false)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={executeSensitiveSearch} className="space-y-3">
              <p className="text-slate-500 dark:text-slate-400 text-xs leading-relaxed">
                {isVi
                  ? 'Theo quy định bảo mật sổ cái, thao tác tra cứu giao dịch tài chính hệ thống sẽ được ghi nhận vào nhật ký kiểm toán (AuditLog).'
                  : 'In compliance with financial ledger policy, accessing sensitive transaction records will be logged in AuditLog.'}
              </p>

              <div className="space-y-1.5">
                <label className="font-semibold text-slate-800 dark:text-slate-200">
                  {isVi ? 'Lý do tra cứu (tối thiểu 5 ký tự) *' : 'Access Justification (min 5 chars) *'}
                </label>
                <textarea
                  rows={3}
                  value={accessReason}
                  onChange={(e) => setAccessReason(e.target.value)}
                  placeholder={
                    isVi
                      ? 'Ví dụ: Đối soát định kỳ theo yêu cầu kiểm toán / Rà soát sự cố giao dịch...'
                      : 'E.g., Periodic financial ledger audit review / Incident investigation...'
                  }
                  required
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white text-xs font-medium resize-none focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
                />
              </div>

              {reasonError && (
                <div className="p-2.5 rounded-xl bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300 font-medium flex items-center gap-2">
                  <AlertCircle className="w-4 h-4 shrink-0" />
                  <span>{reasonError}</span>
                </div>
              )}

              <div className="flex items-center justify-end gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setIsReasonModalOpen(false)}
                  className="text-xs cursor-pointer"
                >
                  {isVi ? 'Hủy bỏ' : 'Cancel'}
                </Button>
                <Button
                  type="submit"
                  size="sm"
                  disabled={isLoading || accessReason.trim().length < 5}
                  className="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs gap-1.5 cursor-pointer"
                >
                  {isLoading ? (
                    <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  ) : (
                    <KeyRound className="w-3.5 h-3.5" />
                  )}
                  <span>{isVi ? 'Xác nhận & Nạp dữ liệu' : 'Confirm & Query'}</span>
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Transaction Details Modal */}
      {selectedTx && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xl p-6 space-y-4 text-xs">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <div>
                <span className="text-[11px] font-medium text-slate-400">
                  {isVi ? 'Chi tiết giao dịch hệ thống' : 'System Transaction Details'}
                </span>
                <h3 className="text-sm font-bold text-slate-900 dark:text-white font-mono mt-0.5 break-all">
                  {selectedTx.id}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedTx(null)}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="space-y-3">
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800/60">
                <span className="text-slate-400">{isVi ? 'Số tiền' : 'Amount'}</span>
                <span className="font-bold text-slate-900 dark:text-white">
                  {formatCurrency(selectedTx.amount, selectedTx.currency)}
                </span>
              </div>
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800/60">
                <span className="text-slate-400">{isVi ? 'Loại giao dịch' : 'Type'}</span>
                <span className="text-slate-800 dark:text-slate-200 font-semibold">
                  {selectedTx.type}
                </span>
              </div>
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800/60">
                <span className="text-slate-400">{isVi ? 'Trạng thái' : 'Status'}</span>
                <span className="font-semibold text-emerald-600 dark:text-emerald-400">
                  {selectedTx.status}
                </span>
              </div>
              <div className="flex flex-col py-1.5 border-b border-slate-100 dark:border-slate-800/60 space-y-0.5">
                <span className="text-slate-400">{isVi ? 'Mã giao dịch sổ cái (Ledger ID)' : 'Ledger Transaction ID'}</span>
                <span className="font-mono text-slate-700 dark:text-slate-300 break-all text-[11px]">
                  {selectedTx.ledgerTransactionId}
                </span>
              </div>
              {selectedTx.relatedBusinessId && (
                <div className="flex flex-col py-1.5 border-b border-slate-100 dark:border-slate-800/60 space-y-0.5">
                  <span className="text-slate-400">{isVi ? 'Định danh nghiệp vụ liên quan (Business ID)' : 'Related Business ID'}</span>
                  <span className="font-mono text-slate-700 dark:text-slate-300 break-all text-[11px]">
                    {selectedTx.relatedBusinessId}
                  </span>
                </div>
              )}
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800/60">
                <span className="text-slate-400">{isVi ? 'Thời gian ghi nhận' : 'Recorded Timestamp'}</span>
                <span className="font-mono text-slate-700 dark:text-slate-300">
                  {formatDate(selectedTx.createdAt)}
                </span>
              </div>
            </div>

            <div className="pt-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setSelectedTx(null)}
                className="w-full text-xs font-semibold cursor-pointer"
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

export default AdminTransactionsPage;
