import React, { useState } from 'react';
import { ShieldAlert, Search, Filter, CheckCircle2, Ban, Eye } from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { EmptyState } from '../../components/ui/EmptyState';

interface RiskCase {
  id: string;
  timestamp: string;
  target: string;
  targetType: 'user' | 'transaction';
  reason: string;
  status: 'pending' | 'reviewed' | 'blocked';
}

export const AdminRiskPage: React.FC = () => {
  const { t, locale } = useI18n();
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'pending' | 'reviewed' | 'blocked'>('all');
  // In pure frontend preview, we do not fabricate backend risk scores or fake incidents.
  const [riskCases, setRiskCases] = useState<RiskCase[]>([]);
  const [selectedCase, setSelectedCase] = useState<RiskCase | null>(null);

  const filteredCases = riskCases.filter((item) => {
    const matchesSearch =
      item.target.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.reason.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesStatus = statusFilter === 'all' || item.status === statusFilter;
    return matchesSearch && matchesStatus;
  });

  const handleStatusChange = (id: string, newStatus: 'reviewed' | 'blocked') => {
    setRiskCases((prev) =>
      prev.map((c) => (c.id === id ? { ...c, status: newStatus } : c))
    );
    if (selectedCase && selectedCase.id === id) {
      setSelectedCase((prev) => (prev ? { ...prev, status: newStatus } : null));
    }
  };

  return (
    <div id="admin-risk-page" className="space-y-6">
      {/* 1. Page Title & short plain-language description */}
      <div>
        <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
          {t('adminRisk.title')}
        </h2>
        <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
          {t('adminRisk.subtitle')}
        </p>
      </div>

      {/* 2. Search & filter controls */}
      <div className="flex flex-col sm:flex-row gap-3 items-stretch sm:items-center justify-between">
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
          <input
            id="admin-risk-search"
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={
              locale === 'vi-VN'
                ? 'Tìm kiếm theo tài khoản, lý do...'
                : 'Search by target, reason...'
            }
            className="w-full pl-9 pr-3 py-2 text-sm rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-900 dark:text-white placeholder-slate-400 focus:outline-hidden focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500"
          />
        </div>

        <div className="flex items-center gap-2">
          <Filter className="h-4 w-4 text-slate-400 shrink-0" />
          <select
            id="admin-risk-status-filter"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as any)}
            className="text-sm rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-700 dark:text-slate-300 py-2 px-3 focus:outline-hidden focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500"
          >
            <option value="all">{locale === 'vi-VN' ? 'Tất cả trạng thái' : 'All statuses'}</option>
            <option value="pending">{locale === 'vi-VN' ? 'Cần xử lý' : 'Pending'}</option>
            <option value="reviewed">{locale === 'vi-VN' ? 'Đã rà soát' : 'Reviewed'}</option>
            <option value="blocked">{locale === 'vi-VN' ? 'Đã chặn' : 'Blocked'}</option>
          </select>
        </div>
      </div>

      {/* 3. Table or Honest Empty State */}
      {filteredCases.length === 0 ? (
        <EmptyState
          icon={ShieldAlert}
          title={
            locale === 'vi-VN'
              ? 'Chưa có dữ liệu cảnh báo rủi ro.'
              : 'No risk alert data yet.'
          }
          description={
            locale === 'vi-VN'
              ? 'Các cảnh báo tự động sẽ xuất hiện tại đây khi hệ thống giám sát backend được kết nối.'
              : 'Automated safety alerts will appear here once the backend monitoring system is connected.'
          }
        />
      ) : (
        <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-xs">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm text-slate-600 dark:text-slate-300">
              <thead className="border-b border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/50 text-xs font-semibold text-slate-500 dark:text-slate-400">
                <tr>
                  <th className="px-4 py-3">{locale === 'vi-VN' ? 'Thời gian' : 'Time'}</th>
                  <th className="px-4 py-3">{locale === 'vi-VN' ? 'Đối tượng' : 'Target'}</th>
                  <th className="px-4 py-3">{locale === 'vi-VN' ? 'Lý do gắn cờ' : 'Flag Reason'}</th>
                  <th className="px-4 py-3">{locale === 'vi-VN' ? 'Trạng thái' : 'Status'}</th>
                  <th className="px-4 py-3 text-right">{locale === 'vi-VN' ? 'Thao tác' : 'Actions'}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {filteredCases.map((item) => (
                  <tr
                    key={item.id}
                    onClick={() => setSelectedCase(item)}
                    className="hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer transition-colors"
                  >
                    <td className="px-4 py-3 text-xs text-slate-500 whitespace-nowrap">
                      {item.timestamp}
                    </td>
                    <td className="px-4 py-3 font-medium text-slate-900 dark:text-white">
                      {item.target}
                    </td>
                    <td className="px-4 py-3 text-slate-600 dark:text-slate-300">
                      {item.reason}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <span
                        className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${
                          item.status === 'pending'
                            ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400'
                            : item.status === 'reviewed'
                            ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-400'
                            : 'bg-rose-100 text-rose-800 dark:bg-rose-900/30 dark:text-rose-400'
                        }`}
                      >
                        {item.status === 'pending'
                          ? locale === 'vi-VN' ? 'Cần xử lý' : 'Pending'
                          : item.status === 'reviewed'
                          ? locale === 'vi-VN' ? 'Đã rà soát' : 'Reviewed'
                          : locale === 'vi-VN' ? 'Đã chặn' : 'Blocked'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right whitespace-nowrap" onClick={(e) => e.stopPropagation()}>
                      <div className="flex items-center justify-end gap-2">
                        {item.status === 'pending' && (
                          <>
                            <button
                              onClick={() => handleStatusChange(item.id, 'reviewed')}
                              className="px-2.5 py-1 text-xs font-medium rounded-lg bg-emerald-50 dark:bg-emerald-900/20 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-100 dark:hover:bg-emerald-900/40 transition"
                            >
                              {locale === 'vi-VN' ? 'Xác nhận rà soát' : 'Mark Reviewed'}
                            </button>
                            <button
                              onClick={() => handleStatusChange(item.id, 'blocked')}
                              className="px-2.5 py-1 text-xs font-medium rounded-lg bg-rose-50 dark:bg-rose-900/20 text-rose-700 dark:text-rose-300 hover:bg-rose-100 dark:hover:bg-rose-900/40 transition"
                            >
                              {locale === 'vi-VN' ? 'Chặn' : 'Block'}
                            </button>
                          </>
                        )}
                        <button
                          onClick={() => setSelectedCase(item)}
                          className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
                          title={locale === 'vi-VN' ? 'Xem chi tiết' : 'View details'}
                        >
                          <Eye className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* 4. Click row for details modal */}
      {selectedCase && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-slate-900 p-6 shadow-xl border border-slate-200 dark:border-slate-800 space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-800">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Chi tiết trường hợp nghi vấn' : 'Case Details'}
              </h3>
              <button
                onClick={() => setSelectedCase(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 text-sm font-semibold"
              >
                ✕
              </button>
            </div>

            <div className="space-y-3 text-sm">
              <div>
                <span className="text-xs text-slate-500 block">{locale === 'vi-VN' ? 'Mã trường hợp' : 'Case ID'}</span>
                <span className="font-mono text-slate-900 dark:text-white">{selectedCase.id}</span>
              </div>
              <div>
                <span className="text-xs text-slate-500 block">{locale === 'vi-VN' ? 'Đối tượng' : 'Target'}</span>
                <span className="font-medium text-slate-900 dark:text-white">{selectedCase.target}</span>
              </div>
              <div>
                <span className="text-xs text-slate-500 block">{locale === 'vi-VN' ? 'Lý do gắn cờ' : 'Flag Reason'}</span>
                <p className="text-slate-700 dark:text-slate-300 mt-0.5">{selectedCase.reason}</p>
              </div>
              <div>
                <span className="text-xs text-slate-500 block">{locale === 'vi-VN' ? 'Thời gian ghi nhận' : 'Logged At'}</span>
                <span className="text-slate-700 dark:text-slate-300">{selectedCase.timestamp}</span>
              </div>
              <div>
                <span className="text-xs text-slate-500 block">{locale === 'vi-VN' ? 'Trạng thái' : 'Status'}</span>
                <span className="inline-block mt-0.5 px-2 py-0.5 rounded-full text-xs font-medium bg-slate-100 dark:bg-slate-800 text-slate-800 dark:text-slate-200">
                  {selectedCase.status === 'pending'
                    ? locale === 'vi-VN' ? 'Cần xử lý' : 'Pending'
                    : selectedCase.status === 'reviewed'
                    ? locale === 'vi-VN' ? 'Đã rà soát' : 'Reviewed'
                    : locale === 'vi-VN' ? 'Đã chặn' : 'Blocked'}
                </span>
              </div>
            </div>

            <div className="pt-4 border-t border-slate-200 dark:border-slate-800 flex justify-end gap-2">
              {selectedCase.status === 'pending' && (
                <>
                  <button
                    onClick={() => handleStatusChange(selectedCase.id, 'blocked')}
                    className="px-3 py-1.5 text-xs font-medium rounded-xl bg-rose-600 text-white hover:bg-rose-700 transition"
                  >
                    {locale === 'vi-VN' ? 'Chặn & Phong tỏa' : 'Block & Quarantine'}
                  </button>
                  <button
                    onClick={() => handleStatusChange(selectedCase.id, 'reviewed')}
                    className="px-3 py-1.5 text-xs font-medium rounded-xl bg-emerald-600 text-white hover:bg-emerald-700 transition"
                  >
                    {locale === 'vi-VN' ? 'Xác nhận rà soát' : 'Mark Reviewed'}
                  </button>
                </>
              )}
              <button
                onClick={() => setSelectedCase(null)}
                className="px-3 py-1.5 text-xs font-medium rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
              >
                {locale === 'vi-VN' ? 'Đóng' : 'Close'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

