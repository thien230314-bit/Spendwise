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
 * SUPPORT LAYOUT B — EFFICIENT DUAL-PANE WORKSPACE
 * Bố cục:
 * - Cột trái (7 cols): Danh sách khiếu nại & hỗ trợ dạng thẻ ngắn gọn, có tìm kiếm inline.
 * - Cột phải (5 cols): Xem nhanh chi tiết phiếu được chọn, đính kèm biên lai và trao đổi trực tiếp.
 */
export interface SupportLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const SupportLayoutB: React.FC<SupportLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();

  const [selectedTicketId, setSelectedTicketId] = useState('SC-1029');

  return (
    <div className="space-y-6">
      {/* 1. Header Toolbar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <LifeBuoy className="h-5 w-5 text-cyan-500" />
          <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
            {locale === 'vi-VN' ? 'Cổng tiếp nhận hỗ trợ & Khiếu nại' : 'Support Console'}
          </h2>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold px-2.5 py-1 rounded-full bg-cyan-100 dark:bg-cyan-950/60 text-cyan-800 dark:text-cyan-300">
            Layout B (Efficient Dual-Pane)
          </span>
          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('support-create-dispute')}
            className="text-xs font-semibold gap-1 cursor-pointer text-rose-600"
          >
            <ShieldAlert className="h-3.5 w-3.5" />
            <span>Khiếu nại</span>
          </Button>
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('support-create-case')}
            className="text-xs font-semibold gap-1 cursor-pointer shadow-xs"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>Yêu cầu</span>
          </Button>
        </div>
      </div>

      {/* 2. Dual Pane (7 cols / 5 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left (7 cols): Tickets List */}
        <div className="lg:col-span-7 bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs divide-y divide-slate-100 dark:divide-slate-800 text-xs">
          {[
            { id: 'SC-1029', title: 'Lỗi đồng bộ giao dịch SePay VietinBank', category: 'Lỗi hệ thống', status: 'Đang xử lý', time: '12/10/2026' },
            { id: 'SC-1028', title: 'Hỏi về quy tắc phân loại tự động', category: 'Tư vấn tính năng', status: 'Đã giải quyết', time: '08/10/2026' },
            { id: 'DISP-8821', title: 'Khiếu nại trừ tiền 2 lần Highlands', category: 'Đối soát giao dịch', status: 'Chờ đối soát', time: '11/10/2026' },
          ].map((item) => (
            <div
              key={item.id}
              onClick={() => setSelectedTicketId(item.id)}
              className={cn(
                'p-4 flex items-center justify-between gap-3 cursor-pointer transition',
                selectedTicketId === item.id ? 'bg-cyan-50/30 dark:bg-cyan-950/20 border-l-4 border-cyan-500' : 'hover:bg-slate-50 dark:hover:bg-slate-800/40'
              )}
            >
              <div>
                <div className="flex items-center gap-2">
                  <span className="font-mono font-bold text-slate-400">{item.id}</span>
                  <span className="font-bold text-slate-900 dark:text-white">{item.title}</span>
                </div>
                <p className="text-[11px] text-slate-400 mt-0.5">{item.category} • {item.time}</p>
              </div>
              <span className="font-mono text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                {item.status}
              </span>
            </div>
          ))}
        </div>

        {/* Right (5 cols): Quick Ticket Inspection View */}
        <div className="lg:col-span-5 bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs p-5 space-y-4 text-xs">
          <div className="border-b border-slate-100 dark:border-slate-800 pb-3">
            <span className="font-mono text-cyan-600 font-bold block">{selectedTicketId}</span>
            <h3 className="text-sm font-bold text-slate-900 dark:text-white mt-1">Lỗi đồng bộ giao dịch SePay VietinBank</h3>
            <p className="text-[11px] text-slate-400">Tiếp nhận bởi Kỹ thuật viên SpendWise</p>
          </div>

          <div className="space-y-2 text-slate-600 dark:text-slate-300">
            <p><strong>Nội dung:</strong> Đã chuyển 500.000 ₫ qua VietinBank lúc 14:00 nhưng số dư ví trong app chưa cập nhật tự động.</p>
            <div className="p-3 bg-slate-50 dark:bg-slate-800 rounded-xl space-y-1">
              <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400 block">Phản hồi mới nhất:</span>
              <p className="text-[11px]">Đội ngũ kỹ thuật đang kiểm tra webhook từ cổng thanh toán SePay. Vui lòng giữ biên lai.</p>
            </div>
          </div>

          <div className="pt-2 flex justify-end">
            <Button
              size="sm"
              variant="outline"
              onClick={() => onOpenPopup?.('support-detail')}
              className="text-xs font-bold"
            >
              Mở chi tiết đầy đủ & Trả lời
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
};
