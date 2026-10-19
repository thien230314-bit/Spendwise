import React, { useState, useEffect, useMemo, useCallback } from 'react';
import {
  ScrollText,
  Search,
  ShieldCheck,
  AlertCircle,
  X,
  Clock,
  User,
  Copy,
  Check,
  RefreshCw,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { casesApiService, AuditLogDto } from '../../services/casesApiService';
import { EmptyState } from '../../components/ui/EmptyState';
import { Select } from '../../components/ui/Select';
import { Button } from '../../components/ui/Button';
import { ApiError } from '../../lib/apiError';

export const AdminAuditPage: React.FC = () => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const isVi = locale === 'vi-VN';

  const [logs, setLogs] = useState<AuditLogDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState('');
  const [targetTypeFilter, setTargetTypeFilter] = useState<string>('all');
  const [selectedLog, setSelectedLog] = useState<AuditLogDto | null>(null);
  const [copiedId, setCopiedId] = useState(false);

  // Fetch real audit logs from backend
  const fetchAuditLogs = useCallback(async () => {
    if (!currentUser || currentUser.role !== 'SystemAdmin') {
      setLogs([]);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setLoadError(null);

    try {
      const data = await casesApiService.getAdminAuditLogs();
      setLogs(data || []);
    } catch (err) {
      console.error('Failed to load audit logs:', err);
      const apiErr = err as ApiError;
      setLoadError(
        apiErr?.detail ||
        apiErr?.title ||
        (isVi ? 'Không thể tải nhật ký kiểm toán từ hệ thống.' : 'Failed to load audit logs from system.')
      );
      setLogs([]);
    } finally {
      setIsLoading(false);
    }
  }, [currentUser, isVi]);

  // Load on mount and reset/reload on account change
  useEffect(() => {
    if (!currentUser || currentUser.role !== 'SystemAdmin') {
      setLogs([]);
      setLoadError(null);
      return;
    }
    fetchAuditLogs();
  }, [fetchAuditLogs, currentUser]);

  // Extract unique target types for dropdown filter
  const targetTypes = useMemo(() => {
    const set = new Set<string>();
    logs.forEach((l) => {
      if (l.targetType) set.add(l.targetType);
    });
    return Array.from(set).sort();
  }, [logs]);

  const filtered = useMemo(() => {
    return logs.filter((l) => {
      const q = searchQuery.toLowerCase().trim();
      const matchesSearch =
        !q ||
        (l.id && l.id.toLowerCase().includes(q)) ||
        (l.actorUserId && l.actorUserId.toLowerCase().includes(q)) ||
        (l.action && l.action.toLowerCase().includes(q)) ||
        (l.targetType && l.targetType.toLowerCase().includes(q)) ||
        (l.targetId && l.targetId.toLowerCase().includes(q)) ||
        (l.reason && l.reason.toLowerCase().includes(q)) ||
        (l.metadataJson && l.metadataJson.toLowerCase().includes(q));

      const matchesType =
        targetTypeFilter === 'all' ||
        l.targetType.toLowerCase() === targetTypeFilter.toLowerCase();

      return matchesSearch && matchesType;
    });
  }, [logs, searchQuery, targetTypeFilter]);

  const copyText = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(true);
    setTimeout(() => setCopiedId(false), 2000);
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

  return (
    <div id="admin-audit-page" className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            {t('adminAudit.title')}
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 max-w-2xl leading-relaxed">
            {t('adminAudit.subtitle')}
          </p>
        </div>

        <Button
          size="sm"
          variant="outline"
          onClick={fetchAuditLogs}
          disabled={isLoading}
          className="text-xs font-semibold gap-1.5 cursor-pointer self-start sm:self-auto"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
          <span>{isVi ? 'Làm mới' : 'Refresh'}</span>
        </Button>
      </div>

      {/* Immutable Rule Notice */}
      <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-800 text-xs text-slate-700 dark:text-slate-300 flex items-center gap-2.5">
        <ShieldCheck className="w-4 h-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
        <span>{t('adminAudit.immutableNotice')}</span>
      </div>

      {/* Search & Filter Controls */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        <div className="relative flex-1 max-w-md">
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={isVi ? 'Tìm theo hành động, loại đối tượng, ID, lý do...' : 'Search action, target, ID, reason...'}
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

        <Select
          value={targetTypeFilter}
          onChange={(val) => setTargetTypeFilter(val)}
          size="sm"
          variant="compact"
          className="w-52"
        >
          <option value="all">{isVi ? 'Tất cả loại đối tượng' : 'All Target Types'}</option>
          {targetTypes.map((type) => (
            <option key={type} value={type}>
              {type}
            </option>
          ))}
        </Select>
      </div>

      {/* Audit Table or EmptyState or Error */}
      {isLoading ? (
        <div className="flex items-center justify-center py-16 gap-2 text-slate-500 text-xs">
          <RefreshCw className="h-4 w-4 animate-spin text-emerald-600" />
          <span>{isVi ? 'Đang tải nhật ký kiểm toán...' : 'Loading audit logs...'}</span>
        </div>
      ) : loadError ? (
        <div className="p-8 text-center bg-rose-50/50 dark:bg-rose-950/20 rounded-2xl border border-rose-200 dark:border-rose-900/50 space-y-3">
          <div className="w-10 h-10 mx-auto rounded-xl bg-rose-100 dark:bg-rose-900/40 text-rose-600 dark:text-rose-400 flex items-center justify-center">
            <AlertCircle className="w-5 h-5" />
          </div>
          <h3 className="text-sm font-bold text-rose-900 dark:text-rose-200">
            {isVi ? 'Không thể tải nhật ký kiểm toán' : 'Failed to load audit logs'}
          </h3>
          <p className="text-xs text-rose-700 dark:text-rose-400 max-w-md mx-auto">
            {loadError}
          </p>
          <Button
            size="sm"
            variant="outline"
            onClick={fetchAuditLogs}
            className="text-xs font-bold gap-1.5 cursor-pointer mt-2 border-rose-300 dark:border-rose-800 text-rose-800 dark:text-rose-300 hover:bg-rose-100/50"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            <span>{isVi ? 'Thử lại' : 'Retry'}</span>
          </Button>
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={ScrollText}
          title={
            locale === 'vi-VN'
              ? 'Chưa có hoạt động quản trị nào được ghi nhận.'
              : 'No administrative activity recorded yet.'
          }
          description={
            locale === 'vi-VN'
              ? 'Các thao tác thay đổi trạng thái, cấp quyền và xử lý nghiệp vụ quản trị sẽ xuất hiện tại đây.'
              : 'Administrative actions and ledger modifications will appear here.'
          }
        />
      ) : (
        <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-xs">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50/80 dark:bg-slate-800/60 border-b border-slate-200 dark:border-slate-800 text-slate-700 dark:text-slate-200 font-semibold text-xs">
                <tr>
                  <th className="px-4 py-3">{isVi ? 'Người thực hiện' : 'Actor (Admin ID)'}</th>
                  <th className="px-4 py-3">{isVi ? 'Hành động' : 'Action'}</th>
                  <th className="px-4 py-3">{isVi ? 'Đối tượng tác động' : 'Target Type & ID'}</th>
                  <th className="px-4 py-3">{isVi ? 'Lý do / Ghi chú' : 'Reason / Notes'}</th>
                  <th className="px-4 py-3 text-right">{isVi ? 'Thời gian' : 'Time'}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800 text-slate-700 dark:text-slate-300">
                {filtered.map((log) => (
                  <tr
                    key={log.id}
                    onClick={() => setSelectedLog(log)}
                    className="hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer transition"
                  >
                    <td className="px-4 py-3 font-mono text-[11px]">
                      <div className="flex items-center gap-1.5 font-semibold text-slate-900 dark:text-white">
                        <User className="w-3.5 h-3.5 text-slate-400 shrink-0" />
                        <span className="truncate max-w-[140px]">{log.actorUserId || 'System'}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <span className="font-semibold px-2 py-0.5 rounded-full text-[11px] bg-slate-100 dark:bg-slate-800 text-slate-800 dark:text-slate-200 border border-slate-200/60 dark:border-slate-700">
                        {log.action}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="font-medium text-slate-800 dark:text-slate-200">
                        {log.targetType}
                      </div>
                      {log.targetId && (
                        <div className="text-[10px] font-mono text-slate-400 truncate max-w-[160px]">
                          {log.targetId}
                        </div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-slate-600 dark:text-slate-400 max-w-xs truncate">
                      {log.reason || (isVi ? '—' : '—')}
                    </td>
                    <td className="px-4 py-3 text-right font-mono text-slate-500 whitespace-nowrap">
                      {formatDate(log.createdAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="px-4 py-2.5 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{isVi ? 'Bấm vào từng dòng để xem chi tiết và metadata' : 'Click any row to inspect details and metadata'}</span>
            <span>{filtered.length} {isVi ? 'bản ghi' : 'records'}</span>
          </div>
        </div>
      )}

      {/* Audit Detail Modal */}
      {selectedLog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-200 dark:border-slate-800 shadow-xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-800">
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {isVi ? 'Chi tiết bản ghi kiểm toán' : 'Audit Record Details'}
                </h3>
                <span className="text-xs font-mono text-slate-400">{selectedLog.id}</span>
              </div>
              <button
                type="button"
                onClick={() => setSelectedLog(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 p-1 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-3 text-xs">
              <div className="grid grid-cols-2 gap-3 p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800">
                <div>
                  <span className="text-slate-400 block">{isVi ? 'Người thực hiện (ID)' : 'Actor (Admin ID)'}</span>
                  <span className="font-mono text-slate-800 dark:text-slate-200 text-xs mt-0.5 block break-all">
                    {selectedLog.actorUserId || 'System'}
                  </span>
                </div>
                <div>
                  <span className="text-slate-400 block">{isVi ? 'Hành động' : 'Action'}</span>
                  <span className="font-semibold text-slate-900 dark:text-white mt-0.5 block">
                    {selectedLog.action}
                  </span>
                </div>
                <div>
                  <span className="text-slate-400 block">{isVi ? 'Loại đối tượng' : 'Target Type'}</span>
                  <span className="font-semibold text-slate-800 dark:text-slate-200 mt-0.5 block">
                    {selectedLog.targetType}
                  </span>
                  {selectedLog.targetId && (
                    <span className="text-[10px] text-slate-400 font-mono block break-all">
                      ID: {selectedLog.targetId}
                    </span>
                  )}
                </div>
                <div>
                  <span className="text-slate-400 block">{isVi ? 'Thời gian' : 'Timestamp'}</span>
                  <span className="font-mono text-slate-700 dark:text-slate-300 mt-0.5 block">
                    {formatDate(selectedLog.createdAt)}
                  </span>
                </div>
              </div>

              {selectedLog.reason && (
                <div>
                  <span className="text-slate-500 font-medium block mb-1">
                    {isVi ? 'Lý do / Giải trình' : 'Reason / Explanation'}
                  </span>
                  <p className="p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800 text-slate-800 dark:text-slate-200 font-medium leading-relaxed">
                    {selectedLog.reason}
                  </p>
                </div>
              )}

              {selectedLog.metadataJson && (
                <div>
                  <span className="text-slate-500 font-medium block mb-1">
                    {isVi ? 'Metadata JSON' : 'Metadata JSON'}
                  </span>
                  <pre className="p-3 rounded-xl bg-slate-950 text-slate-200 font-mono text-[11px] overflow-x-auto leading-relaxed border border-slate-800">
                    {(() => {
                      try {
                        return JSON.stringify(JSON.parse(selectedLog.metadataJson), null, 2);
                      } catch {
                        return selectedLog.metadataJson;
                      }
                    })()}
                  </pre>
                </div>
              )}
            </div>

            <div className="pt-3 border-t border-slate-200 dark:border-slate-800 flex items-center justify-between">
              <button
                type="button"
                onClick={() => copyText(selectedLog.id)}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition cursor-pointer"
              >
                {copiedId ? <Check className="w-3.5 h-3.5 text-emerald-500" /> : <Copy className="w-3.5 h-3.5" />}
                {isVi ? 'Sao chép mã bản ghi' : 'Copy Log ID'}
              </button>

              <button
                type="button"
                onClick={() => setSelectedLog(null)}
                className="px-4 py-2 text-xs font-medium rounded-xl bg-slate-900 text-white dark:bg-white dark:text-slate-900 hover:opacity-90 transition cursor-pointer"
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

export default AdminAuditPage;
