import React, { useState, useEffect } from 'react';
import {
  Building2,
  Search,
  Eye,
  X,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useFinancial } from '../../providers/FinancialProvider';
import { AdminBankIntegrationEvent, BankTransaction } from '../../types/admin';
import { BankConnection } from '../../types';
import { formatMoney } from '../../utils/money';
import { formatDate } from '../../utils/date';
import { cn } from '../../utils/cn';
import { EmptyState } from '../../components/ui/EmptyState';

function formatBankConnectionStatus(status: string, locale: string): string {
  const isVi = locale === 'vi-VN';
  switch (status) {
    case 'Pending':
      return isVi ? 'Đang chờ' : 'Pending';
    case 'Active':
      return isVi ? 'Hoạt động' : 'Active';
    case 'Inactive':
      return isVi ? 'Không hoạt động' : 'Inactive';
    case 'Unlinked':
      return isVi ? 'Đã ngắt kết nối' : 'Unlinked';
    case 'Error':
      return isVi ? 'Lỗi' : 'Error';
    default:
      return status;
  }
}

export const AdminBankingPage: React.FC = () => {
  const { t, locale } = useI18n();
  const {
    adminBankConnections,
    adminBankTransactions,
    adminBankIntegrationEvents,
    sePayIntegrationStatus,
    refreshAdminBanking,
  } = useFinancial();

  useEffect(() => {
    void refreshAdminBanking();
  }, [refreshAdminBanking]);

  const isVi = locale === 'vi-VN';

  // Tabs: 'connections' | 'transactions' | 'webhooks'
  const [activeTab, setActiveTab] = useState<'connections' | 'transactions' | 'webhooks'>('connections');
  const [searchQuery, setSearchQuery] = useState('');

  // Modal inspection
  const [selectedEventPayload, setSelectedEventPayload] = useState<AdminBankIntegrationEvent | null>(null);
  const [selectedConnection, setSelectedConnection] = useState<BankConnection | null>(null);
  const [selectedTransaction, setSelectedTransaction] = useState<BankTransaction | null>(null);

  const formatProcessingStatus = (status: string) => {
    switch (status) {
      case 'Processed':
        return isVi ? 'Đã xử lý' : 'Processed';
      case 'Received':
        return isVi ? 'Đã tiếp nhận' : 'Received';
      case 'Ignored':
        return isVi ? 'Đã bỏ qua' : 'Ignored';
      case 'Unmatched':
        return isVi ? 'Chưa khớp' : 'Unmatched';
      case 'Failed':
      default:
        return isVi ? 'Thất bại' : 'Failed';
    }
  };

  // Filtered lists
  const filteredConnections = adminBankConnections.filter(
    (c) =>
      c.bankBrandName.toLowerCase().includes(searchQuery.toLowerCase()) ||
      c.maskedAccountNumber.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (c.accountHolderName && c.accountHolderName.toLowerCase().includes(searchQuery.toLowerCase())) ||
      c.userId.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const filteredTransactions = adminBankTransactions.filter(
    (t) =>
      t.bankBrandName?.toLowerCase().includes(searchQuery.toLowerCase()) ||
      t.maskedAccountNumber?.toLowerCase().includes(searchQuery.toLowerCase()) ||
      t.content.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (t.externalTransactionId && t.externalTransactionId.toLowerCase().includes(searchQuery.toLowerCase()))
  );

  const filteredEvents = adminBankIntegrationEvents.filter(
    (e) =>
      e.provider.toLowerCase().includes(searchQuery.toLowerCase()) ||
      e.eventType.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (e.externalEventId && e.externalEventId.toLowerCase().includes(searchQuery.toLowerCase()))
  );

  return (
    <div id="admin-banking-page" className="space-y-6">

      {/* 1. Page Header & Short Plain-language Description */}
      <div>
        <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
          {isVi ? 'Ngân hàng' : 'Banking'}
        </h2>
        <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
          {isVi
            ? 'Kiểm tra tài khoản ngân hàng, giao dịch và các sự kiện tích hợp cần xử lý.'
            : 'Review bank accounts, transactions, and integration events that need attention.'}
        </p>
      </div>

      {/* 2. Tabs & Search */}
      <div className="flex flex-col sm:flex-row gap-3 items-stretch sm:items-center justify-between border-b border-slate-200 dark:border-slate-800 pb-3">
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => {
              setActiveTab('connections');
              setSearchQuery('');
            }}
            className={cn(
              'px-3.5 py-1.5 rounded-xl text-xs font-semibold transition',
              activeTab === 'connections'
                ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            {isVi ? 'Kết nối' : 'Connections'} ({adminBankConnections.length})
          </button>
          <button
            type="button"
            onClick={() => {
              setActiveTab('transactions');
              setSearchQuery('');
            }}
            className={cn(
              'px-3.5 py-1.5 rounded-xl text-xs font-semibold transition',
              activeTab === 'transactions'
                ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            {isVi ? 'Giao dịch đồng bộ' : 'Synced Transactions'} ({adminBankTransactions.length})
          </button>
          <button
            type="button"
            onClick={() => {
              setActiveTab('webhooks');
              setSearchQuery('');
            }}
            className={cn(
              'px-3.5 py-1.5 rounded-xl text-xs font-semibold transition',
              activeTab === 'webhooks'
                ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            {isVi ? 'Sự kiện cần kiểm tra' : 'Events to Review'} ({adminBankIntegrationEvents.length})
          </button>
        </div>

        <div className="relative w-full sm:w-64">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={isVi ? 'Tìm kiếm trong bảng...' : 'Search table...'}
            className="w-full pl-8 pr-3 py-1.5 text-xs rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-900 dark:text-white placeholder-slate-400 focus:outline-hidden focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500"
          />
        </div>
      </div>

      {/* Tab 1: Connections Table */}
      {activeTab === 'connections' && (
        <>
          {filteredConnections.length === 0 ? (
            <EmptyState
              icon={Building2}
              title={isVi ? 'Chưa có kết nối ngân hàng nào' : 'No bank connections yet'}
              description={
                isVi
                  ? 'Khi người dùng liên kết tài khoản ngân hàng thông qua cổng kết nối, danh sách sẽ hiển thị tại đây.'
                  : 'When users link their bank accounts through the provider, they will be listed here.'
              }
            />
          ) : (
            <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-xs">
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm text-slate-600 dark:text-slate-300">
                  <thead className="border-b border-slate-200 dark:border-slate-800 bg-slate-50/80 dark:bg-slate-800/60 text-xs font-semibold text-slate-700 dark:text-slate-200">
                    <tr>
                      <th className="px-4 py-3">{isVi ? 'Ngân hàng' : 'Bank'}</th>
                      <th className="px-4 py-3">{isVi ? 'Người dùng' : 'User'}</th>
                      <th className="px-4 py-3">{isVi ? 'Số tài khoản' : 'Account Number'}</th>
                      <th className="px-4 py-3">{isVi ? 'Trạng thái' : 'Status'}</th>
                      <th className="px-4 py-3">{isVi ? 'Lần đồng bộ cuối' : 'Last Synced'}</th>
                      <th className="px-4 py-3 text-right">{isVi ? 'Thao tác' : 'Actions'}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {filteredConnections.map((conn) => (
                      <tr
                        key={conn.id}
                        onClick={() => setSelectedConnection(conn)}
                        className="hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer transition-colors"
                      >
                        <td className="px-4 py-3 font-medium text-slate-900 dark:text-white">
                          {conn.bankBrandName}
                        </td>
                        <td className="px-4 py-3 text-xs text-slate-600 dark:text-slate-400 font-mono">
                          {conn.userId}
                        </td>
                        <td className="px-4 py-3 font-mono text-slate-700 dark:text-slate-300">
                          {conn.maskedAccountNumber}
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={cn(
                              'inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium',
                              conn.status === 'Active'
                                ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-400'
                                : conn.status === 'Pending'
                                ? 'bg-sky-100 text-sky-800 dark:bg-sky-900/30 dark:text-sky-400'
                                : 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-400'
                            )}
                          >
                            {formatBankConnectionStatus(conn.status, locale)}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-xs text-slate-500 font-mono">
                          {conn.lastSyncedAt ? formatDate(conn.lastSyncedAt, locale) : '—'}
                        </td>
                        <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                          <button
                            onClick={() => setSelectedConnection(conn)}
                            className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
                            title={isVi ? 'Xem chi tiết' : 'View details'}
                          >
                            <Eye className="h-4 w-4" />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}

      {/* Tab 2: Synced Transactions Table */}
      {activeTab === 'transactions' && (
        <>
          {filteredTransactions.length === 0 ? (
            <EmptyState
              icon={Building2}
              title={isVi ? 'Chưa có giao dịch đồng bộ nào' : 'No synced transactions yet'}
              description={
                isVi
                  ? 'Các biến động số dư được đồng bộ tự động từ ngân hàng sẽ hiển thị ở đây.'
                  : 'Automated bank transaction updates will appear here.'
              }
            />
          ) : (
            <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-xs">
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm text-slate-600 dark:text-slate-300">
                  <thead className="border-b border-slate-200 dark:border-slate-800 bg-slate-50/80 dark:bg-slate-800/60 text-xs font-semibold text-slate-700 dark:text-slate-200">
                    <tr>
                      <th className="px-4 py-3">{isVi ? 'Thời gian' : 'Time'}</th>
                      <th className="px-4 py-3">{isVi ? 'Ngân hàng' : 'Bank'}</th>
                      <th className="px-4 py-3">{isVi ? 'Số tài khoản' : 'Account Number'}</th>
                      <th className="px-4 py-3">{isVi ? 'Số tiền' : 'Amount'}</th>
                      <th className="px-4 py-3">{isVi ? 'Chiều giao dịch' : 'Direction'}</th>
                      <th className="px-4 py-3">{isVi ? 'Nội dung' : 'Content'}</th>
                      <th className="px-4 py-3">{isVi ? 'Trạng thái' : 'Status'}</th>
                      <th className="px-4 py-3 text-right">{isVi ? 'Thao tác' : 'Actions'}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {filteredTransactions.map((tx) => (
                      <tr
                        key={tx.id}
                        onClick={() => setSelectedTransaction(tx)}
                        className="hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer transition-colors"
                      >
                        <td className="px-4 py-3 text-xs text-slate-500 font-mono whitespace-nowrap">
                          {formatDate(tx.transactionDate, locale)}
                        </td>
                        <td className="px-4 py-3 font-medium text-slate-900 dark:text-white">
                          {tx.bankBrandName || (isVi ? 'Ngân hàng' : 'Bank')}
                        </td>
                        <td className="px-4 py-3 font-mono text-xs text-slate-600 dark:text-slate-400">
                          {tx.maskedAccountNumber || '••••'}
                        </td>
                        <td className="px-4 py-3 font-semibold text-slate-900 dark:text-white whitespace-nowrap">
                          {formatMoney(tx.amount, locale)}
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={cn(
                              'inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium',
                              tx.direction === 'Credit'
                                ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-400'
                                : 'bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-300'
                            )}
                          >
                            {tx.direction === 'Credit'
                              ? isVi ? '+ Ghi có (Nhận)' : '+ Credit'
                              : isVi ? '- Ghi nợ (Chi)' : '- Debit'}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-slate-600 dark:text-slate-300 max-w-xs truncate">
                          {tx.content}
                        </td>
                        <td className="px-4 py-3">
                          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-50 dark:bg-emerald-900/20 text-emerald-700 dark:text-emerald-300">
                            {isVi ? 'Đã đối chiếu' : 'Reconciled'}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                          <button
                            onClick={() => setSelectedTransaction(tx)}
                            className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
                            title={isVi ? 'Xem chi tiết' : 'View details'}
                          >
                            <Eye className="h-4 w-4" />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}

      {/* Tab 3: Events to Review Table */}
      {activeTab === 'webhooks' && (
        <>
          {filteredEvents.length === 0 ? (
            <EmptyState
              icon={Building2}
              title={isVi ? 'Chưa có sự kiện nào cần kiểm tra' : 'No events to review yet'}
              description={
                isVi
                  ? 'Các sự kiện đồng bộ từ ngân hàng sẽ được ghi nhận tại đây để quản trị viên rà soát.'
                  : 'Bank synchronization events will appear here for administrator review.'
              }
            />
          ) : (
            <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 overflow-hidden shadow-xs">
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm text-slate-600 dark:text-slate-300">
                  <thead className="border-b border-slate-200 dark:border-slate-800 bg-slate-50/80 dark:bg-slate-800/60 text-xs font-semibold text-slate-700 dark:text-slate-200">
                    <tr>
                      <th className="px-4 py-3">{isVi ? 'Thời gian' : 'Time'}</th>
                      <th className="px-4 py-3">{isVi ? 'Cổng kết nối' : 'Gateway'}</th>
                      <th className="px-4 py-3">{isVi ? 'Mã tham chiếu' : 'Reference'}</th>
                      <th className="px-4 py-3">{isVi ? 'Ghi chú / Nội dung' : 'Notes / Content'}</th>
                      <th className="px-4 py-3">{isVi ? 'Trạng thái' : 'Status'}</th>
                      <th className="px-4 py-3 text-right">{isVi ? 'Thao tác' : 'Actions'}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {filteredEvents.map((evt) => (
                      <tr
                        key={evt.id}
                        onClick={() => setSelectedEventPayload(evt)}
                        className="hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer transition-colors"
                      >
                        <td className="px-4 py-3 text-xs text-slate-500 font-mono whitespace-nowrap">
                          {evt.receivedAt ? formatDate(evt.receivedAt, locale) : '—'}
                        </td>
                        <td className="px-4 py-3 font-semibold text-slate-900 dark:text-white">
                          {evt.provider}
                        </td>
                        <td className="px-4 py-3 font-mono text-xs text-slate-700 dark:text-slate-300">
                          {evt.externalTransactionId || evt.externalEventId || '—'}
                        </td>
                        <td className="px-4 py-3 text-xs text-slate-600 dark:text-slate-400 max-w-xs truncate">
                          {evt.details || evt.eventType}
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={cn(
                              'inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium',
                              evt.processingStatus === 'Processed'
                                ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-400'
                                : evt.processingStatus === 'Received'
                                ? 'bg-sky-100 text-sky-800 dark:bg-sky-900/30 dark:text-sky-400'
                                : evt.processingStatus === 'Unmatched'
                                ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400'
                                : 'bg-rose-100 text-rose-800 dark:bg-rose-900/30 dark:text-rose-400'
                            )}
                          >
                            {formatProcessingStatus(evt.processingStatus)}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                          <button
                            type="button"
                            onClick={() => setSelectedEventPayload(evt)}
                            className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 text-xs font-medium transition"
                          >
                            <Eye className="w-3.5 h-3.5" />
                            <span>{isVi ? 'Chi tiết' : 'Details'}</span>
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}

      {/* Modal: Connection Details */}
      {selectedConnection && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-slate-900 p-6 shadow-xl border border-slate-200 dark:border-slate-800 space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-800">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {isVi ? 'Chi tiết kết nối ngân hàng' : 'Bank Connection Details'}
              </h3>
              <button
                onClick={() => setSelectedConnection(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                ✕
              </button>
            </div>
            <div className="space-y-3 text-sm">
              <div>
                <span className="text-xs text-slate-500 block">{isVi ? 'Ngân hàng' : 'Bank'}</span>
                <span className="font-semibold text-slate-900 dark:text-white">{selectedConnection.bankBrandName}</span>
              </div>
              <div>
                <span className="text-xs text-slate-500 block">{isVi ? 'Chủ tài khoản' : 'Account Holder'}</span>
                <span className="text-slate-800 dark:text-slate-200">{selectedConnection.accountHolderName || '—'}</span>
              </div>
              <div>
                <span className="text-xs text-slate-500 block">{isVi ? 'Số tài khoản' : 'Account Number'}</span>
                <span className="font-mono text-slate-800 dark:text-slate-200">{selectedConnection.maskedAccountNumber}</span>
              </div>
              <div>
                <span className="text-xs text-slate-500 block">{isVi ? 'Mã người dùng' : 'User ID'}</span>
                <span className="font-mono text-xs text-slate-600 dark:text-slate-400">{selectedConnection.userId}</span>
              </div>
              <div>
                <span className="text-xs text-slate-500 block">{isVi ? 'Trạng thái' : 'Status'}</span>
                <span className="inline-block mt-0.5 px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-100 dark:bg-emerald-900/30 text-emerald-800 dark:text-emerald-400">
                  {formatBankConnectionStatus(selectedConnection.status, locale)}
                </span>
              </div>
            </div>
            <div className="pt-3 border-t border-slate-200 dark:border-slate-800 flex justify-end">
              <button
                onClick={() => setSelectedConnection(null)}
                className="px-4 py-2 text-xs font-medium rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
              >
                {isVi ? 'Đóng' : 'Close'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Transaction Details */}
      {selectedTransaction && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-slate-900 p-6 shadow-xl border border-slate-200 dark:border-slate-800 space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-800">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {isVi ? 'Chi tiết giao dịch đồng bộ' : 'Synced Transaction Details'}
              </h3>
              <button
                onClick={() => setSelectedTransaction(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                ✕
              </button>
            </div>
            <div className="space-y-3 text-sm">
              <div>
                <span className="text-xs text-slate-500 block">{isVi ? 'Mã tham chiếu' : 'Reference Code'}</span>
                <span className="font-mono font-medium text-slate-900 dark:text-white">{selectedTransaction.externalTransactionId}</span>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <span className="text-xs text-slate-500 block">{isVi ? 'Ngân hàng' : 'Bank'}</span>
                  <span className="font-semibold text-slate-900 dark:text-white">{selectedTransaction.bankBrandName}</span>
                </div>
                <div>
                  <span className="text-xs text-slate-500 block">{isVi ? 'Số tài khoản' : 'Account'}</span>
                  <span className="font-mono text-slate-800 dark:text-slate-200">{selectedTransaction.maskedAccountNumber}</span>
                </div>
              </div>
              <div>
                <span className="text-xs text-slate-500 block">{isVi ? 'Số tiền' : 'Amount'}</span>
                <span className="text-lg font-bold text-slate-900 dark:text-white">{formatMoney(selectedTransaction.amount, locale)}</span>
              </div>
              <div>
                <span className="text-xs text-slate-500 block">{isVi ? 'Nội dung chuyển khoản' : 'Content'}</span>
                <p className="text-slate-700 dark:text-slate-300 mt-0.5">{selectedTransaction.content}</p>
              </div>
            </div>
            <div className="pt-3 border-t border-slate-200 dark:border-slate-800 flex justify-end">
              <button
                onClick={() => setSelectedTransaction(null)}
                className="px-4 py-2 text-xs font-medium rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
              >
                {isVi ? 'Đóng' : 'Close'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Bank Event Details */}
      {selectedEventPayload && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-xl bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-200 dark:border-slate-800 shadow-xl space-y-4 max-h-[85vh] overflow-y-auto">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-800">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {isVi ? 'Chi tiết sự kiện ngân hàng' : 'Bank Event Details'}
              </h3>
              <button
                type="button"
                onClick={() => setSelectedEventPayload(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="grid grid-cols-2 gap-3 text-xs">
              <div>
                <span className="text-slate-500 block">{isVi ? 'Cổng kết nối' : 'Gateway'}</span>
                <span className="font-semibold text-slate-900 dark:text-white">{selectedEventPayload.provider}</span>
              </div>
              <div>
                <span className="text-slate-500 block">{isVi ? 'Mã tham chiếu' : 'Reference'}</span>
                <span className="font-mono font-medium text-slate-900 dark:text-white">
                  {selectedEventPayload.externalTransactionId || selectedEventPayload.externalEventId || '—'}
                </span>
              </div>
              <div>
                <span className="text-slate-500 block">{isVi ? 'Loại sự kiện' : 'Event Type'}</span>
                <span className="font-medium text-slate-800 dark:text-slate-200">{selectedEventPayload.eventType}</span>
              </div>
              <div>
                <span className="text-slate-500 block">{isVi ? 'Trạng thái' : 'Status'}</span>
                <span className="font-medium text-slate-800 dark:text-slate-200">
                  {formatProcessingStatus(selectedEventPayload.processingStatus)}
                </span>
              </div>
              <div className="col-span-2">
                <span className="text-slate-500 block">{isVi ? 'Thời gian nhận' : 'Received Time'}</span>
                <span className="text-slate-700 dark:text-slate-300">
                  {selectedEventPayload.receivedAt ? formatDate(selectedEventPayload.receivedAt, locale) : '—'}
                </span>
              </div>
              {selectedEventPayload.details && (
                <div className="col-span-2">
                  <span className="text-slate-500 block">{isVi ? 'Ghi chú' : 'Notes'}</span>
                  <span className="text-slate-700 dark:text-slate-300">{selectedEventPayload.details}</span>
                </div>
              )}
            </div>

            {/* Collapsed Technical Details */}
            <details className="border border-slate-200 dark:border-slate-800 rounded-xl overflow-hidden bg-slate-50/50 dark:bg-slate-800/30">
              <summary className="px-4 py-2.5 text-xs font-semibold text-slate-700 dark:text-slate-300 cursor-pointer select-none flex items-center justify-between hover:bg-slate-100 dark:hover:bg-slate-800/60">
                <span>{isVi ? 'Thông tin kỹ thuật' : 'Technical Details'}</span>
                <span className="text-[11px] text-slate-400 font-normal">
                  {isVi ? 'Mã sự kiện & payload thô' : 'Event ID & raw payload'}
                </span>
              </summary>
              <div className="p-3 border-t border-slate-200 dark:border-slate-800 space-y-2">
                <div className="text-xs">
                  <span className="text-slate-500">{isVi ? 'Mã sự kiện (Webhook ID): ' : 'Webhook Event ID: '}</span>
                  <span className="font-mono text-slate-700 dark:text-slate-300">
                    {selectedEventPayload.externalEventId || selectedEventPayload.id}
                  </span>
                </div>
                <div className="p-3 rounded-lg bg-slate-950 text-emerald-400 font-mono text-xs overflow-x-auto border border-slate-800">
                  <pre>{JSON.stringify(selectedEventPayload.payload || selectedEventPayload, null, 2)}</pre>
                </div>
              </div>
            </details>

            <div className="flex justify-end pt-2 border-t border-slate-200 dark:border-slate-800">
              <button
                type="button"
                onClick={() => setSelectedEventPayload(null)}
                className="px-4 py-2 text-xs font-medium rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
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

