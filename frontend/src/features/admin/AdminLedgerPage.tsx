import React, { useState, useMemo } from 'react';
import {
  BookOpen,
  Search,
  Eye,
  KeyRound,
  ShieldCheck,
  RefreshCw,
  AlertCircle,
  X,
  CheckCircle2,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { EmptyState } from '../../components/ui/EmptyState';
import { Button } from '../../components/ui/Button';
import { Select } from '../../components/ui/Select';
import { casesApiService, LedgerTransactionDto } from '../../services/casesApiService';
import { ApiError } from '../../lib/apiError';

export const AdminLedgerPage: React.FC = () => {
  const { t, locale } = useI18n();
  const isVi = locale === 'vi-VN';
  const { currentUser } = useAuth();

  const [entries, setEntries] = useState<LedgerTransactionDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [hasSearched, setHasSearched] = useState(false);

  // Sensitive reason authorization modal
  const [isReasonModalOpen, setIsReasonModalOpen] = useState(false);
  const [accessReason, setAccessReason] = useState('');
  const [reasonError, setReasonError] = useState<string | null>(null);

  // Search & filter
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [businessTypeFilter, setBusinessTypeFilter] = useState<string>('all');
  const [selectedEntry, setSelectedEntry] = useState<LedgerTransactionDto | null>(null);

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat(locale === 'vi-VN' ? 'vi-VN' : 'en-US', {
      style: 'currency',
      currency: 'VND',
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
          ? 'Lý do truy cập sổ cái nhạy cảm phải có độ dài từ 5 đến 1000 ký tự (theo quy định kiểm toán hệ thống).'
          : 'A meaningful access justification of at least 5 characters is required for audit compliance.'
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
      const data = await casesApiService.searchAdminLedger(trimmedReason);
      setEntries(data || []);
      setHasSearched(true);
      setIsReasonModalOpen(false);
    } catch (err) {
      console.error('Failed to search admin ledger:', err);
      const apiErr = err as ApiError;
      const msg =
        apiErr?.detail ||
        apiErr?.title ||
        (isVi ? 'Lỗi khi tra cứu sổ cái tài chính.' : 'Failed to retrieve ledger entries.');
      setReasonError(msg);
      setLoadError(msg);
    } finally {
      setIsLoading(false);
    }
  };

  const businessTypes = useMemo(() => {
    const set = new Set<string>();
    entries.forEach((e) => {
      if (e.businessType) set.add(e.businessType);
    });
    return Array.from(set).sort();
  }, [entries]);

  const filtered = useMemo(() => {
    return entries.filter((e) => {
      const q = searchQuery.toLowerCase().trim();
      const matchesSearch =
        !q ||
        (e.id && e.id.toLowerCase().includes(q)) ||
        (e.reference && e.reference.toLowerCase().includes(q)) ||
        (e.businessType && e.businessType.toLowerCase().includes(q)) ||
        (e.businessId && e.businessId.toLowerCase().includes(q));

      const matchesStatus =
        statusFilter === 'all' ||
        (statusFilter === 'balanced' && e.balanced) ||
        (statusFilter === 'unbalanced' && !e.balanced) ||
        (e.status && e.status.toLowerCase() === statusFilter.toLowerCase());

      const matchesBusinessType =
        businessTypeFilter === 'all' ||
        (e.businessType && e.businessType.toLowerCase() === businessTypeFilter.toLowerCase());

      return matchesSearch && matchesStatus && matchesBusinessType;
    });
  }, [entries, searchQuery, statusFilter, businessTypeFilter]);

  return (
    <div id="admin-ledger-page" className="space-y-6">
      {/* 1. Page Title & short plain-language description */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            {t('adminLedger.title')}
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
            {isVi
              ? 'Kiểm toán sổ cái bút toán kép (Double-Entry Ledger) với tổng Nợ (Debit), Có (Credit) và tính cân bằng bất biến.'
              : 'Audit double-entry ledger postings with Debit, Credit totals, and balanced status.'}
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
              : (isVi ? 'Xác thực & Tra cứu sổ cái' : 'Authorize & Search Ledger')}
          </span>
        </Button>
      </div>

      {/* Immutable Rule Banner */}
      <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-800 text-xs text-slate-700 dark:text-slate-300 flex items-center justify-between gap-2.5">
        <div className="flex items-center gap-2.5">
          <ShieldCheck className="w-4 h-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
          <span>{t('adminLedger.readOnlyNotice')}</span>
        </div>
        <span className="text-[11px] font-mono text-slate-400 shrink-0">
          POST /admin/ledger/search
        </span>
      </div>

      {/* Search & Filter Controls (only active when data loaded) */}
      {hasSearched && (
        <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
          <div className="relative flex-1 max-w-md">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
            <input
              id="admin-ledger-search"
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={
                isVi
                  ? 'Tìm theo mã tham chiếu, nghiệp vụ, Business ID...'
                  : 'Search by reference, business type, ID...'
              }
              className="w-full pl-9 pr-3 h-9 text-xs rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-900 dark:text-white placeholder-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
            />
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
              value={businessTypeFilter}
              onChange={(val) => setBusinessTypeFilter(val)}
              size="sm"
              variant="compact"
              className="w-44"
            >
              <option value="all">{isVi ? 'Tất cả nghiệp vụ' : 'All Business Types'}</option>
              {businessTypes.map((type) => (
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
              <option value="balanced">{isVi ? 'Cân bằng (Balanced)' : 'Balanced'}</option>
              <option value="unbalanced">{isVi ? 'Lệch sổ (Unbalanced)' : 'Unbalanced'}</option>
              <option value="Posted">{isVi ? 'Posted' : 'Posted'}</option>
              <option value="Pending">{isVi ? 'Pending' : 'Pending'}</option>
            </Select>
          </div>
        </div>
      )}

      {/* Error display */}
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

      {/* Table / Empty State */}
      {!hasSearched && !isLoading ? (
        <div className="p-12 text-center bg-white dark:bg-slate-900 rounded-2xl border border-dashed border-slate-200 dark:border-slate-800 space-y-4">
          <div className="w-12 h-12 mx-auto rounded-2xl bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400 flex items-center justify-center">
            <BookOpen className="w-6 h-6 text-emerald-600 dark:text-emerald-400" />
          </div>
          <div className="space-y-1.5 max-w-md mx-auto">
            <h3 className="text-sm font-bold text-slate-900 dark:text-white">
              {isVi ? 'Yêu cầu xác nhận kiểm toán sổ cái' : 'Ledger Audit Authorization Required'}
            </h3>
            <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
              {isVi
                ? 'Sổ cái bút toán kép ghi nhận dòng tiền bất biến của toàn bộ tài khoản SpendWise. Cần nhập lý do truy cập để ghi nhật ký kiểm toán (VIEW_LEDGER) trước khi nạp dữ liệu.'
                : 'Double-entry ledger records immutable financial postings. An audit justification is required to log the access.'}
            </p>
          </div>
          <Button
            size="sm"
            onClick={handleOpenSearchModal}
            className="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs gap-1.5 cursor-pointer shadow-xs"
          >
            <KeyRound className="w-3.5 h-3.5" />
            <span>{isVi ? 'Nhập lý do & Tra cứu sổ cái' : 'Provide Reason & Search Ledger'}</span>
          </Button>
        </div>
      ) : isLoading ? (
        <div className="flex items-center justify-center py-16 gap-2 text-slate-500 text-xs">
          <RefreshCw className="h-4 w-4 animate-spin text-emerald-600" />
          <span>{isVi ? 'Đang xác thực kiểm toán và đọc dữ liệu sổ cái...' : 'Auditing access and reading ledger records...'}</span>
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={BookOpen}
          title={
            isVi
              ? 'Không tìm thấy bản ghi sổ cái nào'
              : 'No ledger records found'
          }
          description={
            isVi
              ? 'Thử thay đổi bộ lọc tìm kiếm hoặc kiểm tra lại điều kiện tra cứu sổ cái.'
              : 'Try adjusting your search criteria or filter options.'
          }
        />
      ) : (
        <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-xs">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs text-slate-600 dark:text-slate-300">
              <thead className="border-b border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/50 text-xs font-semibold text-slate-500 dark:text-slate-400">
                <tr>
                  <th className="px-4 py-3">{isVi ? 'Mã tham chiếu' : 'Reference'}</th>
                  <th className="px-4 py-3">{isVi ? 'Loại nghiệp vụ' : 'Business Type'}</th>
                  <th className="px-4 py-3">{isVi ? 'Tổng Nợ (Debit)' : 'Debit Total'}</th>
                  <th className="px-4 py-3">{isVi ? 'Tổng Có (Credit)' : 'Credit Total'}</th>
                  <th className="px-4 py-3">{isVi ? 'Cân bằng' : 'Balanced'}</th>
                  <th className="px-4 py-3">{isVi ? 'Trạng thái' : 'Status'}</th>
                  <th className="px-4 py-3 text-right">{isVi ? 'Thời gian ghi' : 'Created At'}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {filtered.map((item) => (
                  <tr
                    key={item.id}
                    onClick={() => setSelectedEntry(item)}
                    className="hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer transition-colors"
                  >
                    <td className="px-4 py-3 font-mono font-bold text-slate-900 dark:text-white">
                      {item.reference}
                    </td>
                    <td className="px-4 py-3">
                      <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-800 dark:text-slate-200">
                        {item.businessType}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-mono font-semibold text-slate-900 dark:text-white whitespace-nowrap">
                      {formatCurrency(item.debitTotal)}
                    </td>
                    <td className="px-4 py-3 font-mono font-semibold text-slate-900 dark:text-white whitespace-nowrap">
                      {formatCurrency(item.creditTotal)}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <span
                        className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold ${
                          item.balanced
                            ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                            : 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60 dark:border-rose-800/40'
                        }`}
                      >
                        {item.balanced ? (
                          <>
                            <CheckCircle2 className="w-3 h-3 text-emerald-600" />
                            <span>{isVi ? 'Cân bằng' : 'Balanced'}</span>
                          </>
                        ) : (
                          <>
                            <AlertCircle className="w-3 h-3 text-rose-600" />
                            <span>{isVi ? 'Lệch sổ' : 'Unbalanced'}</span>
                          </>
                        )}
                      </span>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap font-mono text-[11px] text-slate-600 dark:text-slate-400">
                      {item.status}
                    </td>
                    <td className="px-4 py-3 text-right whitespace-nowrap font-mono text-[11px] text-slate-400">
                      {formatDate(item.createdAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="px-4 py-2.5 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{isVi ? 'Bấm vào từng dòng để xem chi tiết bút toán sổ cái' : 'Click any row for ledger entry details'}</span>
            <span>{filtered.length} / {entries.length} {isVi ? 'bút toán' : 'postings'}</span>
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
                  {isVi ? 'Xác nhận lý do kiểm toán sổ cái' : 'Ledger Audit Authorization'}
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
                  ? 'Theo quy chuẩn an toàn tài chính, thao tác tra cứu sổ cái sẽ ghi nhận sự kiện VIEW_LEDGER vào nhật ký kiểm toán hệ thống.'
                  : 'In compliance with financial controls, querying the ledger will log a VIEW_LEDGER event in the system audit trail.'}
              </p>

              <div className="space-y-1.5">
                <label className="font-semibold text-slate-800 dark:text-slate-200">
                  {isVi ? 'Lý do tra cứu sổ cái (tối thiểu 5 ký tự) *' : 'Audit Reason (min 5 chars) *'}
                </label>
                <textarea
                  rows={3}
                  value={accessReason}
                  onChange={(e) => setAccessReason(e.target.value)}
                  placeholder={
                    isVi
                      ? 'Ví dụ: Kiểm toán tính cân đối Nợ/Có định kỳ / Rà soát đối soát sổ cái...'
                      : 'E.g., Periodic debit/credit balance verification / Ledger reconciliation...'
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
                  <span>{isVi ? 'Xác nhận & Đọc sổ cái' : 'Confirm & Query'}</span>
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Details modal */}
      {selectedEntry && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-slate-900 p-6 shadow-xl border border-slate-200 dark:border-slate-800 space-y-4 text-xs">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-800">
              <div>
                <span className="text-[11px] font-medium text-slate-400">
                  {isVi ? 'Chi tiết bút toán sổ cái' : 'Ledger Transaction Details'}
                </span>
                <h3 className="text-base font-bold text-slate-900 dark:text-white font-mono mt-0.5">
                  {selectedEntry.reference}
                </h3>
              </div>
              <button
                onClick={() => setSelectedEntry(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer p-1"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="space-y-3">
              <div className="flex flex-col py-1.5 border-b border-slate-100 dark:border-slate-800/60 space-y-0.5">
                <span className="text-slate-400">{isVi ? 'Mã định danh sổ cái (ID)' : 'Ledger Transaction ID'}</span>
                <span className="font-mono text-slate-900 dark:text-white font-semibold break-all text-[11px]">
                  {selectedEntry.id}
                </span>
              </div>
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800/60">
                <span className="text-slate-400">{isVi ? 'Nghiệp vụ phát sinh' : 'Business Type'}</span>
                <span className="font-semibold text-slate-800 dark:text-slate-200">
                  {selectedEntry.businessType}
                </span>
              </div>
              <div className="flex flex-col py-1.5 border-b border-slate-100 dark:border-slate-800/60 space-y-0.5">
                <span className="text-slate-400">{isVi ? 'Mã nghiệp vụ liên kết (Business ID)' : 'Linked Business ID'}</span>
                <span className="font-mono text-slate-700 dark:text-slate-300 break-all text-[11px]">
                  {selectedEntry.businessId}
                </span>
              </div>
              <div className="grid grid-cols-2 gap-4 py-1.5 border-b border-slate-100 dark:border-slate-800/60">
                <div>
                  <span className="text-slate-400 block mb-0.5">{isVi ? 'Tổng Nợ (Debit)' : 'Debit Total'}</span>
                  <span className="font-mono font-bold text-slate-900 dark:text-white text-xs">
                    {formatCurrency(selectedEntry.debitTotal)}
                  </span>
                </div>
                <div>
                  <span className="text-slate-400 block mb-0.5">{isVi ? 'Tổng Có (Credit)' : 'Credit Total'}</span>
                  <span className="font-mono font-bold text-slate-900 dark:text-white text-xs">
                    {formatCurrency(selectedEntry.creditTotal)}
                  </span>
                </div>
              </div>
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800/60">
                <span className="text-slate-400">{isVi ? 'Tính cân đối (Debit = Credit)' : 'Balance Integrity'}</span>
                <span
                  className={`inline-flex items-center gap-1 font-semibold ${
                    selectedEntry.balanced
                      ? 'text-emerald-600 dark:text-emerald-400'
                      : 'text-rose-600 dark:text-rose-400'
                  }`}
                >
                  {selectedEntry.balanced ? (
                    <>
                      <CheckCircle2 className="w-3.5 h-3.5" />
                      <span>{isVi ? 'Cân bằng hoàn hảo' : 'Balanced'}</span>
                    </>
                  ) : (
                    <>
                      <AlertCircle className="w-3.5 h-3.5" />
                      <span>{isVi ? 'Chênh lệch / Cần đối soát' : 'Unbalanced'}</span>
                    </>
                  )}
                </span>
              </div>
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800/60">
                <span className="text-slate-400">{isVi ? 'Trạng thái bút toán' : 'Posting Status'}</span>
                <span className="font-mono text-slate-700 dark:text-slate-300">
                  {selectedEntry.status}
                </span>
              </div>
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800/60">
                <span className="text-slate-400">{isVi ? 'Thời gian ghi sổ' : 'Created At'}</span>
                <span className="font-mono text-slate-700 dark:text-slate-300">
                  {formatDate(selectedEntry.createdAt)}
                </span>
              </div>
            </div>

            <div className="pt-2 flex justify-end">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setSelectedEntry(null)}
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

export default AdminLedgerPage;
