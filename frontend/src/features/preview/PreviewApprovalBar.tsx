import React, { useState, useEffect } from 'react';
import {
  Check,
  Edit3,
  RotateCcw,
  CheckCircle2,
  AlertCircle,
  HelpCircle,
  Clock,
  Sparkles,
  ExternalLink,
  MessageSquare,
  ShieldCheck,
  X,
  Loader2,
  Lock,
} from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { cn } from '../../utils/cn';
import {
  devApprovalService,
  ApprovalStatus,
  ParityStatus,
  PageApprovalRecord,
} from '../../services/devApprovalService';

interface ApprovalBarProps {
  pageKey: string;
  pageName: string;
  previewVersion: string;
  fingerprint: string;
  route: string;
  parityStatus?: ParityStatus;
  parityMissingCount?: number;
  parityAuditNotes?: string;
  onStatusChange?: (newStatus: ApprovalStatus) => void;
}

const STATUS_CONFIG: Record<
  ApprovalStatus,
  { label: string; bg: string; text: string; border: string; icon: any }
> = {
  PENDING: {
    label: 'Chờ duyệt (PENDING)',
    bg: 'bg-slate-100 dark:bg-slate-800',
    text: 'text-slate-700 dark:text-slate-300',
    border: 'border-slate-300 dark:border-slate-700',
    icon: Clock,
  },
  APPROVED: {
    label: 'Đã duyệt — Chờ agent áp dụng (APPROVED)',
    bg: 'bg-emerald-50 dark:bg-emerald-950/60',
    text: 'text-emerald-700 dark:text-emerald-300',
    border: 'border-emerald-300 dark:border-emerald-700',
    icon: CheckCircle2,
  },
  NEEDS_REVISION: {
    label: 'Cần sửa giao diện (NEEDS_REVISION)',
    bg: 'bg-amber-50 dark:bg-amber-950/60',
    text: 'text-amber-700 dark:text-amber-300',
    border: 'border-amber-300 dark:border-amber-700',
    icon: AlertCircle,
  },
  KEEP_CURRENT: {
    label: 'Giữ giao diện cũ (KEEP_CURRENT)',
    bg: 'bg-slate-100 dark:bg-slate-800',
    text: 'text-slate-600 dark:text-slate-400',
    border: 'border-slate-300 dark:border-slate-700',
    icon: RotateCcw,
  },
  APPLYING: {
    label: 'Agent đang áp dụng vào production... (APPLYING)',
    bg: 'bg-blue-50 dark:bg-blue-950/60',
    text: 'text-blue-700 dark:text-blue-300',
    border: 'border-blue-300 dark:border-blue-700',
    icon: Loader2,
  },
  APPLIED: {
    label: 'Đã áp dụng production & Đã kiểm tra (APPLIED)',
    bg: 'bg-emerald-100 dark:bg-emerald-950',
    text: 'text-emerald-800 dark:text-emerald-200',
    border: 'border-emerald-400 dark:border-emerald-600',
    icon: ShieldCheck,
  },
  APPLY_FAILED: {
    label: 'Áp dụng thất bại / Cần xử lý (APPLY_FAILED)',
    bg: 'bg-rose-50 dark:bg-rose-950/60',
    text: 'text-rose-700 dark:text-rose-300',
    border: 'border-rose-300 dark:border-rose-700',
    icon: AlertCircle,
  },
};

const PARITY_CONFIG: Record<
  ParityStatus,
  { label: string; badgeClass: string; icon: any }
> = {
  VERIFIED: {
    label: 'Feature Parity: 100% ĐẦY ĐỦ CHỨC NĂNG (VERIFIED)',
    badgeClass: 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 border-emerald-300 dark:border-emerald-800',
    icon: ShieldCheck,
  },
  AUDITING: {
    label: 'Feature Parity: Đang kiểm kê đối chiếu...',
    badgeClass: 'bg-blue-100 dark:bg-blue-950/60 text-blue-800 dark:text-blue-300 border-blue-300 dark:border-blue-800',
    icon: Loader2,
  },
  INCOMPLETE: {
    label: 'Feature Parity: CHƯA ĐỦ CHỨC NĂNG (BỊ KHÓA DUYỆT)',
    badgeClass: 'bg-amber-100 dark:bg-amber-950/60 text-amber-800 dark:text-amber-300 border-amber-300 dark:border-amber-800',
    icon: Lock,
  },
  BLOCKED: {
    label: 'Feature Parity: BỊ CHẶN DO REGRESSION',
    badgeClass: 'bg-rose-100 dark:bg-rose-950/60 text-rose-800 dark:text-rose-300 border-rose-300 dark:border-rose-800',
    icon: AlertCircle,
  },
  NOT_AUDITED: {
    label: 'Feature Parity: Chưa đối chiếu',
    badgeClass: 'bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border-slate-300 dark:border-slate-700',
    icon: Clock,
  },
};

export const PreviewApprovalBar: React.FC<ApprovalBarProps> = ({
  pageKey,
  pageName,
  previewVersion,
  fingerprint,
  route,
  parityStatus = 'VERIFIED',
  parityMissingCount = 0,
  parityAuditNotes,
  onStatusChange,
}) => {
  const [record, setRecord] = useState<PageApprovalRecord | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [isUpdating, setIsUpdating] = useState<boolean>(false);
  const [showFeedbackModal, setShowFeedbackModal] = useState<boolean>(false);
  const [feedbackText, setFeedbackText] = useState<string>('');
  const [toastMsg, setToastMsg] = useState<string | null>(null);

  const fetchRecord = async () => {
    setLoading(true);
    const store = await devApprovalService.getStore();
    if (store && store.pages && store.pages[pageKey]) {
      const current = store.pages[pageKey];
      // Check version mismatch: If previewVersion changed since decision was made
      if (current.previewVersion && current.previewVersion !== previewVersion && current.status === 'APPROVED') {
        // Fingerprint/version mismatch invalidates old approval!
        setRecord({
          ...current,
          status: 'PENDING',
          previewVersion,
          fingerprint,
          parityStatus,
        });
      } else {
        setRecord(current);
      }
    } else {
      setRecord({
        route,
        previewTab: pageKey,
        pageName,
        previewVersion,
        fingerprint,
        status: 'PENDING',
        parityStatus,
        decidedAt: null,
        feedback: null,
        appliedAt: null,
        errorReason: null,
      });
    }
    setLoading(false);
  };

  useEffect(() => {
    fetchRecord();
  }, [pageKey, previewVersion]);

  const showToast = (msg: string) => {
    setToastMsg(msg);
    setTimeout(() => setToastMsg(null), 3000);
  };

  const handleAction = async (status: ApprovalStatus, feedback?: string) => {
    if (status === 'APPROVED' && parityStatus !== 'VERIFIED') {
      showToast('Khóa duyệt: Bản Preview chưa đạt chứng nhận Feature Parity 100%!');
      return;
    }

    setIsUpdating(true);
    const updated = await devApprovalService.updateDecision({
      pageKey,
      status,
      previewVersion,
      fingerprint,
      parityStatus,
      feedback: feedback !== undefined ? feedback : record?.feedback || null,
    });

    if (updated) {
      setRecord(updated);
      onStatusChange?.(status);
      if (status === 'APPROVED') {
        showToast(`✓ Đã duyệt ${pageName}! Quyết định đã được lưu.`);
      } else if (status === 'NEEDS_REVISION') {
        showToast(`✎ Đã lưu yêu cầu chỉnh sửa cho ${pageName}.`);
      } else if (status === 'KEEP_CURRENT') {
        showToast(`↩ Giữ nguyên giao diện production cho ${pageName}.`);
      }
    } else {
      showToast(`Không thể kết nối đến máy chủ dev để lưu quyết định.`);
    }
    setIsUpdating(false);
  };

  const status = record?.status || 'PENDING';
  const cfg = STATUS_CONFIG[status] || STATUS_CONFIG.PENDING;
  const StatusIcon = cfg.icon;

  const parityCfg = PARITY_CONFIG[parityStatus] || PARITY_CONFIG.NOT_AUDITED;
  const ParityIcon = parityCfg.icon;
  const isApprovalLocked = parityStatus !== 'VERIFIED';

  return (
    <div className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs overflow-hidden transition-all">
      {toastMsg && (
        <div className="bg-emerald-600 text-white text-xs font-semibold px-4 py-2 text-center flex items-center justify-center gap-2 animate-in fade-in">
          <CheckCircle2 className="h-4 w-4" />
          <span>{toastMsg}</span>
        </div>
      )}

      {/* Feature Parity Safety Banner if locked */}
      {isApprovalLocked && (
        <div className="bg-amber-50 dark:bg-amber-950/40 border-b border-amber-200 dark:border-amber-800/60 px-4 py-2 text-xs flex items-center justify-between gap-3 text-amber-900 dark:text-amber-200">
          <div className="flex items-center gap-2 font-medium">
            <Lock className="h-4 w-4 text-amber-600 dark:text-amber-400 shrink-0" />
            <span>
              <strong>Bảo vệ Phê duyệt:</strong> Nút "Duyệt & Áp dụng" đang bị khóa do thiếu {parityMissingCount} tính năng/thành phần so với Production.
            </span>
          </div>
          {parityAuditNotes && (
            <span className="text-[11px] text-amber-700 dark:text-amber-300 truncate font-mono">
              {parityAuditNotes}
            </span>
          )}
        </div>
      )}

      <div className="p-4 sm:p-5 flex flex-col md:flex-row md:items-center justify-between gap-4">
        {/* Left: Page identity, version & current status */}
        <div className="space-y-1.5">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-sm font-extrabold text-slate-900 dark:text-white">
              {pageName}
            </h3>
            <span className="text-[10px] font-mono px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 border border-slate-200 dark:border-slate-700">
              {previewVersion}
            </span>
            <span
              className={cn(
                'inline-flex items-center gap-1.5 text-[11px] font-bold px-2.5 py-0.5 rounded-full border',
                cfg.bg,
                cfg.text,
                cfg.border
              )}
            >
              <StatusIcon className="h-3.5 w-3.5 shrink-0" />
              <span>{cfg.label}</span>
            </span>

            {/* Parity Badge */}
            <span
              className={cn(
                'inline-flex items-center gap-1.5 text-[10px] font-bold px-2.5 py-0.5 rounded-full border',
                parityCfg.badgeClass
              )}
            >
              <ParityIcon className="h-3 w-3 shrink-0" />
              <span>{parityCfg.label}</span>
            </span>
          </div>

          <div className="flex flex-wrap items-center gap-3 text-xs text-slate-500 dark:text-slate-400">
            <span>Route: <code className="font-mono text-slate-700 dark:text-slate-300">{route}</code></span>
            {record?.decidedAt && (
              <span>• Quyết định lúc: {new Date(record.decidedAt).toLocaleTimeString('vi-VN')} {new Date(record.decidedAt).toLocaleDateString('vi-VN')}</span>
            )}
            {record?.feedback && (
              <span className="text-amber-600 dark:text-amber-400 font-medium">• Ghi chú: &ldquo;{record.feedback}&rdquo;</span>
            )}
          </div>
        </div>

        {/* Right: The 3 Approval Action Buttons */}
        <div className="flex flex-wrap items-center gap-2 shrink-0 self-start md:self-auto">
          {/* Action 1: APPROVED - Hard-gated by Feature Parity */}
          <Button
            size="sm"
            disabled={isUpdating || loading || isApprovalLocked}
            onClick={() => handleAction('APPROVED')}
            title={
              isApprovalLocked
                ? 'Nút Duyệt bị khóa: Chỉ được duyệt khi Preview đạt 100% Feature Parity so với Production'
                : 'Duyệt & Cho phép áp dụng thiết kế này vào Production'
            }
            className={cn(
              'gap-1.5 text-xs font-bold transition-all shadow-xs cursor-pointer',
              isApprovalLocked
                ? 'bg-slate-300 dark:bg-slate-800 text-slate-500 dark:text-slate-500 cursor-not-allowed opacity-60'
                : status === 'APPROVED'
                ? 'bg-emerald-700 text-white ring-2 ring-emerald-500/50'
                : 'bg-emerald-600 hover:bg-emerald-700 text-white'
            )}
          >
            {isApprovalLocked ? (
              <Lock className="h-3.5 w-3.5" />
            ) : (
              <Check className="h-3.5 w-3.5" />
            )}
            <span>✓ DUYỆT & ÁP DỤNG</span>
          </Button>

          {/* Action 2: NEEDS_REVISION */}
          <Button
            size="sm"
            variant="outline"
            disabled={isUpdating || loading}
            onClick={() => {
              setFeedbackText(record?.feedback || '');
              setShowFeedbackModal(true);
            }}
            className={cn(
              'gap-1.5 text-xs font-semibold cursor-pointer border-slate-200 dark:border-slate-700',
              status === 'NEEDS_REVISION'
                ? 'border-amber-400 bg-amber-50/50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-300 font-bold'
                : 'text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800'
            )}
          >
            <Edit3 className="h-3.5 w-3.5 text-amber-500" />
            <span>✎ CẦN SỬA</span>
          </Button>

          {/* Action 3: KEEP_CURRENT */}
          <Button
            size="sm"
            variant="outline"
            disabled={isUpdating || loading}
            onClick={() => handleAction('KEEP_CURRENT')}
            className={cn(
              'gap-1.5 text-xs font-semibold cursor-pointer border-slate-200 dark:border-slate-700',
              status === 'KEEP_CURRENT'
                ? 'border-slate-400 bg-slate-100 dark:bg-slate-800 text-slate-900 dark:text-white font-bold'
                : 'text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800'
            )}
          >
            <RotateCcw className="h-3.5 w-3.5" />
            <span>↩ GIỮ GIAO DIỆN CŨ</span>
          </Button>
        </div>
      </div>

      {/* Revision Feedback Modal */}
      {showFeedbackModal && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in"
        >
          <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Edit3 className="h-5 w-5 text-amber-500" />
                <h4 className="text-base font-bold text-slate-900 dark:text-white">
                  Yêu cầu chỉnh sửa: {pageName}
                </h4>
              </div>
              <button
                type="button"
                onClick={() => setShowFeedbackModal(false)}
                className="p-1 rounded-lg text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <p className="text-xs text-slate-500 dark:text-slate-400">
              Nhập nhận xét hoặc các điểm chưa hài lòng để agent cải thiện bản thiết kế xem trước này. Giao diện production sẽ được giữ nguyên cho tới khi bạn duyệt bản sửa mới.
            </p>

            <textarea
              rows={4}
              value={feedbackText}
              onChange={(e) => setFeedbackText(e.target.value)}
              placeholder="Ví dụ: Font số tiền quá lớn, bảng giao dịch cần thêm cột trạng thái, màu sắc badge chưa hài hòa..."
              className="w-full p-3 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
              autoFocus
            />

            <div className="flex items-center justify-end gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setShowFeedbackModal(false)}
                className="cursor-pointer text-xs"
              >
                Hủy bỏ
              </Button>
              <Button
                size="sm"
                onClick={() => {
                  setShowFeedbackModal(false);
                  handleAction('NEEDS_REVISION', feedbackText);
                }}
                className="bg-amber-600 hover:bg-amber-700 text-white font-bold text-xs cursor-pointer"
              >
                Lưu yêu cầu CẦN SỬA
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
