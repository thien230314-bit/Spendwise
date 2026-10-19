import React, { useState, useMemo } from 'react';
import {
  LifeBuoy,
  ShieldAlert,
  Plus,
  MessageSquare,
  Clock,
  CheckCircle2,
  AlertTriangle,
  AlertCircle,
  HelpCircle,
  Calendar,
  FileQuestion,
  Search,
  Filter,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * SUPPORT LAYOUT A — BALANCED WORKSPACE
 * Bố cục:
 * - Hàng 1: Banner tổng quan phiếu hỗ trợ & khiếu nại (Tổng yêu cầu, Đang xử lý, Đã giải quyết) + Nút Tạo yêu cầu mới & Tạo khiếu nại.
 * - Cột trái (8 cols): Danh sách phiếu hỗ trợ / khiếu nại giao dịch với phân loại nhãn và tiến độ xử lý.
 * - Cột phải (4 cols): Kênh hỗ trợ trực tiếp & Hướng dẫn chính sách đối soát minh bạch.
 */
export interface SupportLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const SupportLayoutA: React.FC<SupportLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();

  const [activeTab, setActiveTab] = useState<'cases' | 'disputes'>('cases');

  const casesList = useMemo(() => [
    { id: 'SC-1029', title: 'Lỗi đồng bộ giao dịch SePay VietinBank', category: 'Lỗi hệ thống', status: 'Đang xử lý', time: '12/10/2026' },
    { id: 'SC-1028', title: 'Hỏi về quy tắc phân loại tự động', category: 'Tư vấn tính năng', status: 'Đã giải quyết', time: '08/10/2026' },
  ], []);

  const disputesList = useMemo(() => [
    { id: 'DISP-8821', title: 'Khiếu nại trừ tiền 2 lần tại Highlands Coffee', amount: '125.000 ₫', status: 'Chờ đối soát', time: '11/10/2026' },
  ], []);

  return (
    <div className="space-y-6">
      {/* 1. Header Toolbar */}
      <div className="p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-6">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <LifeBuoy className="h-5 w-5 text-emerald-500" />
            <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
              {locale === 'vi-VN' ? 'Trung tâm hỗ trợ & Khiếu nại giao dịch' : 'Support & Dispute Resolution'}
            </h2>
            <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
              Layout A (Balanced Workspace)
            </span>
          </div>
          <p className="text-xs text-slate-500 dark:text-slate-400">
            {locale === 'vi-VN' ? 'Gửi yêu cầu trợ giúp kỹ thuật hoặc khiếu nại giao dịch sai lệch với ngân hàng.' : 'Submit support tickets and transaction disputes.'}
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('support-create-dispute')}
            className="text-xs font-semibold gap-1.5 cursor-pointer text-rose-600 hover:text-rose-700"
          >
            <ShieldAlert className="h-3.5 w-3.5" />
            <span>Tạo khiếu nại</span>
          </Button>
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('support-create-case')}
            className="text-xs font-semibold gap-1.5 cursor-pointer shadow-xs"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>+ Yêu cầu hỗ trợ</span>
          </Button>
        </div>
      </div>

      {/* 2. Sub-Tabs */}
      <div className="flex items-center gap-2 border-b border-slate-200 dark:border-slate-800 pb-2">
        <button
          type="button"
          onClick={() => setActiveTab('cases')}
          className={cn(
            'px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2',
            activeTab === 'cases' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'text-slate-500'
          )}
        >
          <HelpCircle className="h-4 w-4" />
          <span>Yêu cầu hỗ trợ (2)</span>
        </button>
        <button
          type="button"
          onClick={() => setActiveTab('disputes')}
          className={cn(
            'px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2',
            activeTab === 'disputes' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'text-slate-500'
          )}
        >
          <ShieldAlert className="h-4 w-4" />
          <span>Khiếu nại giao dịch (1)</span>
        </button>
      </div>

      {/* 3. Main 2-Column Grid (8 cols / 4 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left (8 cols): Tickets Table */}
        <div className="lg:col-span-8 bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
          <div className="divide-y divide-slate-100 dark:divide-slate-800 text-xs">
            {activeTab === 'cases' ? (
              casesList.map((c) => (
                <div
                  key={c.id}
                  onClick={() => onOpenPopup?.('support-detail')}
                  className="p-4 flex items-center justify-between gap-4 hover:bg-slate-50/60 dark:hover:bg-slate-800/30 transition cursor-pointer"
                >
                  <div className="space-y-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-mono text-[11px] font-bold text-slate-400">{c.id}</span>
                      <span className="font-bold text-slate-900 dark:text-white truncate">{c.title}</span>
                    </div>
                    <p className="text-[11px] text-slate-500">{c.category} • Gửi ngày {c.time}</p>
                  </div>
                  <span className={cn('px-2.5 py-1 rounded-full text-[10px] font-bold font-mono', c.status === 'Đang xử lý' ? 'bg-amber-100 text-amber-800' : 'bg-emerald-100 text-emerald-800')}>
                    {c.status}
                  </span>
                </div>
              ))
            ) : (
              disputesList.map((d) => (
                <div
                  key={d.id}
                  onClick={() => onOpenPopup?.('support-detail')}
                  className="p-4 flex items-center justify-between gap-4 hover:bg-slate-50/60 dark:hover:bg-slate-800/30 transition cursor-pointer"
                >
                  <div className="space-y-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-mono text-[11px] font-bold text-rose-500">{d.id}</span>
                      <span className="font-bold text-slate-900 dark:text-white truncate">{d.title}</span>
                    </div>
                    <p className="text-[11px] text-slate-500">Số tiền: {d.amount} • Ngày: {d.time}</p>
                  </div>
                  <span className="px-2.5 py-1 rounded-full text-[10px] font-bold font-mono bg-rose-100 text-rose-800">
                    {d.status}
                  </span>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Right (4 cols): Policy & Contact Info */}
        <div className="lg:col-span-4 space-y-4 text-xs">
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3">
            <h3 className="font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-1.5">
              <Clock className="h-4 w-4 text-emerald-500" />
              <span>Thời gian tiếp nhận</span>
            </h3>
            <p className="text-slate-600 dark:text-slate-300 leading-relaxed">
              Các khiếu nại tài chính được xử lý trong vòng 24 - 48 giờ làm việc theo quy trình đối soát liên ngân hàng.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};
