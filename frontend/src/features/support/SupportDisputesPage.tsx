import React, { useState, useEffect, useMemo } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
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
  DollarSign,
  User as UserIcon,
  ChevronRight,
  Send,
  X,
  FileQuestion,
  RefreshCw,
} from 'lucide-react';
import { useAuth } from '../../providers/AuthProvider';
import { useFinancial } from '../../providers/FinancialProvider';
import { useI18n } from '../../i18n/I18nContext';
import {
  useSupportDisputes,
} from '../../services/supportDisputeService';
import {
  SupportCase,
  DisputeRecord,
  SupportCaseCategory,
  DisputeReason,
  AttachmentMetadata,
  Transaction,
} from '../../types';
import { Button } from '../../components/ui/Button';
import { Select } from '../../components/ui/Select';
import { AttachmentUploader } from '../../components/support/AttachmentUploader';
import { AttachmentList } from '../../components/support/AttachmentList';
import { getLocalizedErrorMessage } from '../../utils/localizationHelpers';
import { formatMoney } from '../../utils/money';
import { SupportDisputesSkeleton } from '../../components/skeletons/SupportDisputesSkeleton';

interface SupportDisputesPageProps {
  defaultTab?: 'cases' | 'disputes';
}

export const SupportDisputesPage: React.FC<SupportDisputesPageProps> = ({
  defaultTab = 'cases',
}) => {
  const { currentUser } = useAuth();
  const { transactions } = useFinancial();
  const { t, locale } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  // Tab State
  const tabParam = searchParams.get('tab');
  const [activeTab, setActiveTab] = useState<'cases' | 'disputes'>(
    tabParam === 'disputes' || defaultTab === 'disputes' ? 'disputes' : 'cases'
  );

  // Unified Store Hook
  const {
    supportCases,
    disputes,
    isLoading,
    loadError,
    refresh,
    loadAttachmentBlob,
    createSupportCase,
    createDispute,
    userReplySupportCase,
    userReplyDispute,
  } = useSupportDisputes(currentUser);

  // Modals state
  const [isCreateCaseOpen, setIsCreateCaseOpen] = useState(false);
  const [isCreateDisputeOpen, setIsCreateDisputeOpen] = useState(false);
  const [selectedCase, setSelectedCase] = useState<SupportCase | null>(null);
  const [selectedDispute, setSelectedDispute] = useState<DisputeRecord | null>(null);

  // Form states: Create Support Case
  const [caseTitle, setCaseTitle] = useState('');
  const [caseCategory, setCaseCategory] = useState<SupportCaseCategory>('system_error');
  const [caseDescription, setCaseDescription] = useState('');
  const [caseLinkedTxId, setCaseLinkedTxId] = useState('');
  const [caseAttachments, setCaseAttachments] = useState<AttachmentMetadata[]>([]);
  const [caseError, setCaseError] = useState<string | null>(null);
  const [caseSubmitting, setCaseSubmitting] = useState(false);

  // Form states: Create Dispute
  const [disputeTxId, setDisputeTxId] = useState('');
  const [disputeReason, setDisputeReason] = useState<DisputeReason>('unrecognized_charge');
  const [disputeDescription, setDisputeDescription] = useState('');
  const [disputeAttachments, setDisputeAttachments] = useState<AttachmentMetadata[]>([]);
  const [disputeError, setDisputeError] = useState<string | null>(null);
  const [disputeSubmitting, setDisputeSubmitting] = useState(false);

  // Reply state for cases/disputes
  const [replyMessage, setReplyMessage] = useState('');
  const [replyAttachments, setReplyAttachments] = useState<AttachmentMetadata[]>([]);
  const [replySubmitting, setReplySubmitting] = useState(false);
  const [replyError, setReplyError] = useState<string | null>(null);

  // Dedicated state for pending attachment retry after case/dispute creation
  const [pendingAttachmentRetry, setPendingAttachmentRetry] = useState<{
    targetType: 'case' | 'dispute';
    targetId: string;
    attachments: AttachmentMetadata[];
  } | null>(null);

  // Toast
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3500);
  };

  // Filter transactions belonging to current user (strictly transaction.userId === currentUser.id)
  const userTransactions = useMemo(() => {
    if (!currentUser?.id) return [];
    return transactions.filter(
      (tx) => tx.userId === currentUser.id
    );
  }, [transactions, currentUser?.id]);

  // Handle URL deep links: tab, tx, id, disputeId
  useEffect(() => {
    const tab = searchParams.get('tab');
    if (tab === 'disputes') {
      setActiveTab('disputes');
    } else if (tab === 'cases') {
      setActiveTab('cases');
    }

    const txParam = searchParams.get('tx') || searchParams.get('txId');
    const categoryParam = searchParams.get('category');

    if (txParam && (categoryParam === 'transaction_edit' || tab === 'cases')) {
      setActiveTab('cases');
      // For transaction_edit, transaction must have:
      // - tx.userId === currentUser.id (never accept missing userId)
      // - tx.isManualEntry === true
      // - tx.type === 'income' || tx.type === 'expense'
      const matchedTx = transactions.find((t) => t.id === txParam || t.referenceCode === txParam);
      const isEligible = Boolean(
        matchedTx &&
        currentUser?.id &&
        matchedTx.userId === currentUser.id &&
        matchedTx.isManualEntry === true &&
        (matchedTx.type === 'income' || matchedTx.type === 'expense')
      );

      if (isEligible && matchedTx) {
        setCaseCategory('transaction_edit');
        setCaseLinkedTxId(matchedTx.id);
        const txRef = matchedTx.referenceCode || matchedTx.id;
        setCaseTitle(
          locale === 'vi-VN'
            ? `Yêu cầu thêm lượt sửa giao dịch #${txRef}`
            : `Request an additional edit for transaction #${txRef}`
        );
        setCaseDescription(
          locale === 'vi-VN'
            ? `Yêu cầu cấp thêm 1 lượt chỉnh sửa cho giao dịch #${txRef} (${matchedTx.title} - ${formatMoney(Math.abs(matchedTx.amount), locale)}).\nLý do cần chỉnh sửa thêm: `
            : `Request 1 additional correction allowance for transaction #${txRef} (${matchedTx.title} - ${formatMoney(Math.abs(matchedTx.amount), locale)}).\nReason for requesting another correction: `
        );
        setCaseError(null);
        setIsCreateCaseOpen(true);
      } else {
        setCaseCategory('transaction_edit');
        setCaseLinkedTxId('');
        setCaseError(
          locale === 'vi-VN'
            ? 'Giao dịch không hợp lệ hoặc không thuộc quyền sở hữu của bạn để yêu cầu thêm lượt sửa (chỉ hỗ trợ giao dịch thu/chi thủ công do chính bạn tạo).'
            : 'Transaction is invalid or not owned by you to request a correction allowance.'
        );
        setIsCreateCaseOpen(true);
      }
    } else if (txParam) {
      setActiveTab('disputes');
      const matchedDisputeTx = transactions.find((t) => t.id === txParam || t.referenceCode === txParam);
      if (matchedDisputeTx && currentUser?.id && matchedDisputeTx.userId === currentUser.id) {
        setDisputeTxId(matchedDisputeTx.id);
        setDisputeError(null);
      } else {
        setDisputeTxId('');
        setDisputeError(
          locale === 'vi-VN'
            ? 'Giao dịch không hợp lệ hoặc không thuộc quyền sở hữu của bạn để khiếu nại.'
            : 'Transaction is invalid or not owned by you to dispute.'
        );
      }
      setIsCreateDisputeOpen(true);
    }

    const caseId = searchParams.get('id') || searchParams.get('caseId');
    if (caseId) {
      const found = supportCases.find((c) => c.id === caseId);
      if (found) {
        setSelectedCase(found);
        setActiveTab('cases');
      }
    }

    const dId = searchParams.get('disputeId');
    if (dId) {
      const found = disputes.find((d) => d.id === dId);
      if (found) {
        setSelectedDispute(found);
        setActiveTab('disputes');
      }
    }
  }, [searchParams, supportCases, disputes]);

  // Keep selectedCase / selectedDispute fresh when store updates
  useEffect(() => {
    if (selectedCase) {
      const fresh = supportCases.find((c) => c.id === selectedCase.id);
      if (fresh) setSelectedCase(fresh);
    }
  }, [supportCases]);

  useEffect(() => {
    if (selectedDispute) {
      const fresh = disputes.find((d) => d.id === selectedDispute.id);
      if (fresh) setSelectedDispute(fresh);
    }
  }, [disputes]);

  const switchTab = (tab: 'cases' | 'disputes') => {
    setActiveTab(tab);
    setSearchParams({ tab });
  };

  // Submit Support Case
  const handleCreateCaseSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentUser) return;
    setCaseError(null);

    if (!caseTitle.trim()) {
      setCaseError(locale === 'vi-VN' ? 'Vui lòng nhập tiêu đề yêu cầu.' : 'Please enter a subject.');
      return;
    }
    if (!caseDescription.trim()) {
      setCaseError(locale === 'vi-VN' ? 'Vui lòng nhập mô tả chi tiết vấn đề.' : 'Please describe the issue.');
      return;
    }

    if (caseCategory === 'transaction_edit') {
      if (!caseLinkedTxId) {
        setCaseError(
          locale === 'vi-VN'
            ? 'Vui lòng chọn giao dịch thu/chi thủ công cần cấp thêm lượt sửa.'
            : 'Please select a manual transaction to request extra correction.'
        );
        return;
      }
      const linkedTx = transactions.find((t) => t.id === caseLinkedTxId);
      if (
        !linkedTx ||
        !currentUser?.id ||
        linkedTx.userId !== currentUser.id ||
        linkedTx.isManualEntry !== true ||
        (linkedTx.type !== 'income' && linkedTx.type !== 'expense')
      ) {
        setCaseError(
          locale === 'vi-VN'
            ? 'Giao dịch được chọn không hợp lệ hoặc không thuộc sở hữu của bạn (chỉ áp dụng cho giao dịch thu/chi thủ công do chính bạn tạo).'
            : 'The selected transaction is invalid or not owned by you.'
        );
        return;
      }
    }

    setCaseSubmitting(true);
    try {
      let linkedRef: string | undefined;
      if (caseLinkedTxId) {
        const found = userTransactions.find((tx) => tx.id === caseLinkedTxId);
        if (found) linkedRef = found.referenceCode || found.id;
      }

      const res = await createSupportCase({
        category: caseCategory,
        title: caseTitle.trim(),
        description: caseDescription.trim(),
        attachments: caseAttachments,
        linkedTransactionId: caseLinkedTxId || undefined,
        linkedTransactionRef: linkedRef,
      });

      if (res.success) {
        if (res.attachmentError && res.caseId) {
          // Case was created successfully, but attachments failed to upload
          const retained = [...caseAttachments];
          setPendingAttachmentRetry({
            targetType: 'case',
            targetId: res.caseId,
            attachments: retained,
          });
          setReplyAttachments(retained);
          setReplyMessage('');

          // Select created case and switch to tab so user can retry immediately on this specific case
          const foundCase = supportCases.find((c) => c.id === res.caseId);
          if (foundCase) {
            setSelectedCase(foundCase);
          } else {
            // Temporary minimal case object until background refresh delivers the full DTO
            setSelectedCase({
              id: res.caseId,
              ticketNumber: `#${res.caseId.slice(0, 8).toUpperCase()}`,
              userId: currentUser.id,
              subject: caseTitle.trim(),
              title: caseTitle.trim(),
              category: caseCategory,
              description: caseDescription.trim(),
              message: caseDescription.trim(),
              status: 'pending',
              priority: 'normal',
              createdAt: new Date().toISOString(),
              updatedAt: new Date().toISOString(),
            });
          }
          setActiveTab('cases');

          showToast(
            locale === 'vi-VN'
              ? `Đã tạo yêu cầu hỗ trợ (${res.caseId}) thành công, nhưng tệp đính kèm tải lên thất bại. Bạn có thể thử gửi lại tệp ngay bên dưới.`
              : `Support request created (${res.caseId}), but attachment upload failed. You can retry uploading files below.`
          );
        } else {
          showToast(
            locale === 'vi-VN'
              ? 'Đã gửi yêu cầu hỗ trợ thành công! Đội ngũ hỗ trợ sẽ phản hồi sớm.'
              : 'Support request submitted successfully!'
          );
        }
        setCaseTitle('');
        setCaseDescription('');
        setCaseLinkedTxId('');
        setCaseAttachments([]);
        setIsCreateCaseOpen(false);
      } else {
        setCaseError(
          getLocalizedErrorMessage(res.error, locale, t)
        );
      }
    } finally {
      setCaseSubmitting(false);
    }
  };

  // Submit Transaction Dispute
  const handleCreateDisputeSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentUser) return;
    setDisputeError(null);

    if (!disputeTxId) {
      setDisputeError(locale === 'vi-VN' ? 'Vui lòng chọn giao dịch cần khiếu nại.' : 'Please select a transaction to dispute.');
      return;
    }
    if (!disputeDescription.trim()) {
      setDisputeError(locale === 'vi-VN' ? 'Vui lòng nhập mô tả lý do khiếu nại.' : 'Please provide dispute details.');
      return;
    }

    const tx = userTransactions.find((t) => t.id === disputeTxId);
    if (!tx || !currentUser?.id || tx.userId !== currentUser.id) {
      setDisputeError(
        locale === 'vi-VN'
          ? 'Giao dịch không hợp lệ hoặc không thuộc quyền sở hữu của bạn.'
          : 'Valid user transaction not found.'
        );
      return;
    }

    setDisputeSubmitting(true);
    try {
      const res = await createDispute({
        transaction: {
          id: tx.id,
          userId: tx.userId,
          referenceCode: tx.referenceCode,
          title: tx.title,
          amount: tx.amount,
          date: tx.date || tx.createdAt,
          recipient: tx.recipient,
          sender: tx.sender,
          merchantName: tx.merchantName,
        },
        reason: disputeReason,
        description: disputeDescription.trim(),
        attachments: disputeAttachments,
      });

      if (res.success) {
        if (res.attachmentError && res.disputeId) {
          // Dispute created, but attachments failed
          const retained = [...disputeAttachments];
          setPendingAttachmentRetry({
            targetType: 'dispute',
            targetId: res.disputeId,
            attachments: retained,
          });
          setReplyAttachments(retained);
          setReplyMessage('');

          // Select created dispute and switch to tab so user can retry immediately on this specific dispute
          const foundDispute = disputes.find((d) => d.id === res.disputeId);
          if (foundDispute) {
            setSelectedDispute(foundDispute);
          } else {
            setSelectedDispute({
              id: res.disputeId,
              disputeCode: `#${res.disputeId.slice(0, 8).toUpperCase()}`,
              userId: currentUser.id,
              transactionId: tx.id,
              transactionRef: tx.referenceCode || tx.id,
              transactionTitle: tx.title,
              transactionAmount: tx.amount,
              transactionDate: tx.date || tx.createdAt,
              reason: disputeReason,
              description: disputeDescription.trim(),
              status: 'pending',
              createdAt: new Date().toISOString(),
              updatedAt: new Date().toISOString(),
            });
          }
          setActiveTab('disputes');

          showToast(
            locale === 'vi-VN'
              ? `Đã tạo hồ sơ khiếu nại (${res.disputeId}) thành công, nhưng tài liệu đính kèm tải lên thất bại. Bạn có thể thử gửi lại tài liệu ngay bên dưới.`
              : `Dispute created (${res.disputeId}), but attachments failed to upload. You can retry uploading files below.`
          );
        } else {
          showToast(
            locale === 'vi-VN'
              ? 'Đã gửi khiếu nại giao dịch thành công! Hồ sơ đang chờ đối soát.'
              : 'Transaction dispute submitted successfully!'
          );
        }
        setDisputeTxId('');
        setDisputeDescription('');
        setDisputeAttachments([]);
        setIsCreateDisputeOpen(false);
      } else {
        setDisputeError(
          getLocalizedErrorMessage(res.error, locale, t)
        );
      }
    } finally {
      setDisputeSubmitting(false);
    }
  };

  // Handle User Reply to Need More Info
  const handleUserReplyCase = async (caseId: string) => {
    if (!replyMessage.trim() && replyAttachments.length === 0) {
      setReplyError(locale === 'vi-VN' ? 'Vui lòng nhập nội dung phản hồi hoặc chọn tệp bổ sung.' : 'Please enter a reply or attach files.');
      return;
    }

    setReplySubmitting(true);
    setReplyError(null);
    try {
      const res = await userReplySupportCase({
        caseId,
        message: replyMessage.trim(),
        attachments: replyAttachments,
      });
      if (res.success) {
        setReplyMessage('');
        setReplyAttachments([]);
        setPendingAttachmentRetry((prev) => (prev?.targetId === caseId ? null : prev));
        if (res.case) {
          setSelectedCase(res.case);
        }
        showToast(
          locale === 'vi-VN'
            ? 'Đã gửi phản hồi / tài liệu thành công!'
            : 'Information / documents submitted successfully!'
        );
      } else {
        setReplyError(
          getLocalizedErrorMessage(res.error, locale, t)
        );
      }
    } finally {
      setReplySubmitting(false);
    }
  };

  const handleUserReplyDispute = async (disputeId: string) => {
    if (!replyMessage.trim() && replyAttachments.length === 0) {
      setReplyError(locale === 'vi-VN' ? 'Vui lòng nhập nội dung giải trình hoặc chọn chứng từ bổ sung.' : 'Please enter details or attach files.');
      return;
    }

    setReplySubmitting(true);
    setReplyError(null);
    try {
      const res = await userReplyDispute({
        disputeId,
        message: replyMessage.trim(),
        attachments: replyAttachments,
      });
      if (res.success) {
        setReplyMessage('');
        setReplyAttachments([]);
        setPendingAttachmentRetry((prev) => (prev?.targetId === disputeId ? null : prev));
        if (res.dispute) {
          setSelectedDispute(res.dispute);
        }
        showToast(
          locale === 'vi-VN'
            ? 'Đã gửi tài liệu đối soát thành công!'
            : 'Evidence submitted successfully!'
        );
      } else {
        setReplyError(
          getLocalizedErrorMessage(res.error, locale, t)
        );
      }
    } finally {
      setReplySubmitting(false);
    }
  };

  const getCategoryLabel = (cat: SupportCaseCategory) => {
    switch (cat) {
      case 'system_error':
        return locale === 'vi-VN' ? 'Lỗi hệ thống' : 'System Error';
      case 'account':
        return locale === 'vi-VN' ? 'Tài khoản' : 'Account';
      case 'banking_sepay':
        return locale === 'vi-VN' ? 'Ngân hàng / SePay' : 'Banking / SePay';
      case 'transaction_edit':
        return locale === 'vi-VN' ? 'Yêu cầu chỉnh sửa giao dịch' : 'Transaction Edit Request';
      case 'other':
      case 'general':
      default:
        return locale === 'vi-VN' ? 'Khác' : 'Other';
    }
  };

  const getReasonLabel = (reason: DisputeReason) => {
    switch (reason) {
      case 'unrecognized_charge':
        return locale === 'vi-VN' ? 'Giao dịch lạ / Nghi vấn gian lận' : 'Unrecognized / Fraud Charge';
      case 'duplicate_deduction':
      case 'duplicate_transaction':
        return locale === 'vi-VN' ? 'Bị trừ tiền 2 lần / Trùng lặp' : 'Duplicate Charge';
      case 'incorrect_amount':
        return locale === 'vi-VN' ? 'Sai số tiền giao dịch' : 'Incorrect Amount';
      case 'goods_not_received':
        return locale === 'vi-VN' ? 'Chưa nhận được hàng / dịch vụ' : 'Goods Not Received';
      case 'merchant_dispute':
        return locale === 'vi-VN' ? 'Bất đồng với bên bán / Giao dịch lỗi' : 'Merchant Dispute';
      case 'other':
      default:
        return locale === 'vi-VN' ? 'Lý do khác' : 'Other';
    }
  };

  const getCaseStatusBadge = (status: string) => {
    switch (status) {
      case 'pending':
      case 'open':
        return {
          label: locale === 'vi-VN' ? 'Chờ tiếp nhận' : 'Pending',
          className: 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 border-amber-200 dark:border-amber-900',
        };
      case 'in_progress':
        return {
          label: locale === 'vi-VN' ? 'Đang xử lý' : 'In Progress',
          className: 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300 border-blue-200 dark:border-blue-900',
        };
      case 'need_more_info':
        return {
          label: locale === 'vi-VN' ? 'Cần bổ sung thông tin' : 'Need More Info',
          className: 'bg-orange-100 text-orange-800 dark:bg-orange-950 dark:text-orange-300 border-orange-200 dark:border-orange-900 animate-pulse',
        };
      case 'resolved':
        return {
          label: locale === 'vi-VN' ? 'Đã giải quyết' : 'Resolved',
          className: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border-emerald-200 dark:border-emerald-900',
        };
      case 'rejected':
        return {
          label: locale === 'vi-VN' ? 'Từ chối' : 'Rejected',
          className: 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300 border-rose-200 dark:border-rose-900',
        };
      case 'closed':
      default:
        return {
          label: locale === 'vi-VN' ? 'Đã đóng' : 'Closed',
          className: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300 border-slate-200 dark:border-slate-700',
        };
    }
  };

  const getDisputeStatusBadge = (status: string) => {
    switch (status) {
      case 'pending':
      case 'submitted':
        return {
          label: locale === 'vi-VN' ? 'Chờ xử lý' : 'Pending',
          className: 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 border-amber-200 dark:border-amber-900',
        };
      case 'in_review':
      case 'under_review':
        return {
          label: locale === 'vi-VN' ? 'Đang rà soát' : 'In Review',
          className: 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300 border-blue-200 dark:border-blue-900',
        };
      case 'need_more_info':
        return {
          label: locale === 'vi-VN' ? 'Cần thêm thông tin' : 'Need More Info',
          className: 'bg-orange-100 text-orange-800 dark:bg-orange-950 dark:text-orange-300 border-orange-200 dark:border-orange-900 animate-pulse',
        };
      case 'resolved':
        return {
          label: locale === 'vi-VN' ? 'Đã giải quyết (Chấp nhận)' : 'Resolved',
          className: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border-emerald-200 dark:border-emerald-900',
        };
      case 'rejected':
      default:
        return {
          label: locale === 'vi-VN' ? 'Từ chối khiếu nại' : 'Rejected',
          className: 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300 border-rose-200 dark:border-rose-900',
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

  if (isLoading && supportCases.length === 0 && disputes.length === 0) {
    return <SupportDisputesSkeleton />;
  }

  return (
    <div id="support-disputes-page" className="max-w-6xl mx-auto space-y-6 pb-12">
      {/* Toast */}
      {toastMessage && (
        <div className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-xl bg-slate-900 text-white shadow-xl border border-emerald-500/50 text-xs font-semibold animate-in fade-in slide-in-from-bottom-2">
          <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Header Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3.5 py-4 px-5 sm:px-6 rounded-2xl bg-white dark:bg-[#151D2F] border border-slate-200/80 dark:border-slate-800/80 shadow-xs">
        <div className="space-y-0.5">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 border border-emerald-100 dark:border-emerald-900/40">
              <LifeBuoy className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-lg sm:text-xl font-bold text-slate-900 dark:text-white tracking-tight">
                {locale === 'vi-VN' ? 'Hỗ trợ & Khiếu nại' : 'Support & Disputes'}
              </h1>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN'
                  ? 'Gửi yêu cầu hỗ trợ hoặc khiếu nại giao dịch cần kiểm tra.'
                  : 'Submit support requests or transaction disputes for review.'}
              </p>
            </div>
          </div>
        </div>

        {/* Action button based on active tab */}
        <div>
          {activeTab === 'cases' ? (
            <Button
              onClick={() => {
                setCaseError(null);
                setIsCreateCaseOpen(true);
              }}
              className="w-full sm:w-auto bg-emerald-600 hover:bg-emerald-700 text-white font-bold gap-2 text-xs shadow-xs cursor-pointer py-2 px-3.5 rounded-xl"
            >
              <Plus className="w-4 h-4" />
              <span>{locale === 'vi-VN' ? 'Tạo yêu cầu hỗ trợ mới' : 'New Support Request'}</span>
            </Button>
          ) : (
            <Button
              onClick={() => {
                setDisputeError(null);
                setIsCreateDisputeOpen(true);
              }}
              className="w-full sm:w-auto bg-rose-600 hover:bg-rose-700 text-white font-bold gap-2 text-xs shadow-xs cursor-pointer py-2 px-3.5 rounded-xl"
            >
              <Plus className="w-4 h-4" />
              <span>{locale === 'vi-VN' ? 'Gửi khiếu nại giao dịch' : 'Submit Transaction Dispute'}</span>
            </Button>
          )}
        </div>
      </div>

      {/* Tabs Navigation */}
      <div className="inline-flex items-center gap-1.5 p-1 rounded-xl bg-slate-100 dark:bg-slate-900/80 border border-slate-200/80 dark:border-slate-800">
        <button
          type="button"
          onClick={() => switchTab('cases')}
          className={`flex items-center gap-2 px-3.5 py-2 rounded-lg text-xs font-bold transition-all cursor-pointer ${
            activeTab === 'cases'
              ? 'bg-white dark:bg-[#151D2F] text-emerald-700 dark:text-emerald-300 shadow-xs border border-slate-200/60 dark:border-slate-700/60'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
          }`}
        >
          <MessageSquare className="w-4 h-4" />
          <span>{locale === 'vi-VN' ? 'Yêu cầu hỗ trợ' : 'Support Requests'}</span>
          <span className="px-2 py-0.5 rounded-full text-[11px] font-mono font-bold bg-slate-200/80 dark:bg-slate-800 text-slate-700 dark:text-slate-200">
            {supportCases.length}
          </span>
        </button>

        <button
          type="button"
          onClick={() => switchTab('disputes')}
          className={`flex items-center gap-2 px-3.5 py-2 rounded-lg text-xs font-bold transition-all cursor-pointer ${
            activeTab === 'disputes'
              ? 'bg-white dark:bg-[#151D2F] text-rose-700 dark:text-rose-300 shadow-xs border border-slate-200/60 dark:border-slate-700/60'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
          }`}
        >
          <ShieldAlert className="w-4 h-4" />
          <span>{locale === 'vi-VN' ? 'Khiếu nại giao dịch' : 'Transaction Disputes'}</span>
          <span className="px-2 py-0.5 rounded-full text-[11px] font-mono font-bold bg-slate-200/80 dark:bg-slate-800 text-slate-700 dark:text-slate-200">
            {disputes.length}
          </span>
        </button>
      </div>

      {/* ========================================================================= */}
      {/* TAB 1: SUPPORT REQUESTS LIST                                              */}
      {/* ========================================================================= */}
      {activeTab === 'cases' && (
        <div className="space-y-4">
          {isLoading ? (
            <div className="flex items-center justify-center py-12 gap-2 text-slate-500 text-xs">
              <RefreshCw className="h-4 w-4 animate-spin text-emerald-600" />
              <span>{locale === 'vi-VN' ? 'Đang tải danh sách yêu cầu hỗ trợ...' : 'Loading support requests...'}</span>
            </div>
          ) : loadError ? (
            <div className="p-8 text-center bg-rose-50/50 dark:bg-rose-950/20 rounded-3xl border border-rose-200 dark:border-rose-900/50 space-y-3">
              <div className="w-10 h-10 mx-auto rounded-xl bg-rose-100 dark:bg-rose-900/40 text-rose-600 dark:text-rose-400 flex items-center justify-center">
                <AlertCircle className="w-5 h-5" />
              </div>
              <h3 className="text-sm font-bold text-rose-900 dark:text-rose-200">
                {locale === 'vi-VN' ? 'Không thể tải danh sách yêu cầu hỗ trợ' : 'Failed to load support requests'}
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
                <span>{locale === 'vi-VN' ? 'Thử lại' : 'Retry'}</span>
              </Button>
            </div>
          ) : supportCases.length === 0 ? (
            <div className="max-w-md mx-auto p-7 text-center bg-white dark:bg-[#151D2F] rounded-2xl border border-dashed border-slate-300 dark:border-slate-700/80 shadow-xs space-y-3.5 my-4">
              <div className="w-11 h-11 mx-auto rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 border border-emerald-100 dark:border-emerald-900/40 flex items-center justify-center">
                <HelpCircle className="w-5 h-5" />
              </div>
              <div className="space-y-1">
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  {locale === 'vi-VN' ? 'Chưa có yêu cầu hỗ trợ' : 'No support requests yet'}
                </h3>
                <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed max-w-xs mx-auto">
                  {locale === 'vi-VN'
                    ? 'Bạn chưa gửi yêu cầu nào. Khi cần trợ giúp, hãy tạo yêu cầu mới.'
                    : "You haven't submitted any requests yet. If you need help, create a new request."}
                </p>
              </div>
              <Button
                size="sm"
                onClick={() => {
                  setCaseError(null);
                  setIsCreateCaseOpen(true);
                }}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold gap-1.5 text-xs shadow-xs cursor-pointer py-2 px-3.5 mx-auto rounded-xl"
              >
                <Plus className="w-3.5 h-3.5" />
                <span>{locale === 'vi-VN' ? 'Tạo yêu cầu hỗ trợ' : 'New Support Request'}</span>
              </Button>
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-3">
              {supportCases.map((c) => {
                const badge = getCaseStatusBadge(c.status);
                const isNeedMoreInfo = c.status === 'need_more_info';

                return (
                  <div
                    key={c.id}
                    onClick={() => setSelectedCase(c)}
                    className={`p-5 rounded-2xl bg-white dark:bg-[#151D2F] border transition-all cursor-pointer hover:shadow-md hover:border-slate-300 dark:hover:border-slate-700 ${
                      isNeedMoreInfo
                        ? 'border-orange-300 dark:border-orange-800/80 bg-orange-50/20 dark:bg-orange-950/10'
                        : 'border-slate-200/80 dark:border-slate-800/80 shadow-xs'
                    }`}
                  >
                    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
                      <div className="space-y-1 min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-mono text-xs font-bold text-slate-500 dark:text-slate-400">
                            {c.ticketNumber || c.id}
                          </span>
                          <span className="px-2 py-0.5 rounded-full text-[10px] font-bold uppercase bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                            {getCategoryLabel(c.category)}
                          </span>
                          <span className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold border ${badge.className}`}>
                            {badge.label}
                          </span>
                          {c.attachments && c.attachments.length > 0 && (
                            <span className="text-[10px] font-semibold text-slate-400">
                              • {c.attachments.length}{' '}
                              {locale === 'vi-VN'
                                ? 'tệp đính kèm'
                                : c.attachments.length === 1
                                ? 'attachment'
                                : 'attachments'}
                            </span>
                          )}
                        </div>

                        <h3 className="font-bold text-sm text-slate-900 dark:text-white truncate">
                          {c.title || c.subject}
                        </h3>

                        <p className="text-xs text-slate-500 dark:text-slate-400 line-clamp-1">
                          {c.description || c.message}
                        </p>
                      </div>

                      <div className="flex items-center gap-3 shrink-0">
                        <div className="text-right">
                          <span className="text-[11px] text-slate-400 font-mono block">
                            {formatDate(c.createdAt)}
                          </span>
                          {c.history && c.history.length > 1 && (
                            <span className="text-[10px] text-emerald-600 dark:text-emerald-400 font-semibold">
                              {c.history.length}{' '}
                              {locale === 'vi-VN'
                                ? 'cập nhật'
                                : c.history.length === 1
                                ? 'update'
                                : 'updates'}
                            </span>
                          )}
                        </div>
                        <ChevronRight className="w-4 h-4 text-slate-400" />
                      </div>
                    </div>

                    {/* Prominent banner if need more info */}
                    {isNeedMoreInfo && (
                      <div className="mt-3 p-3 rounded-xl bg-orange-100/70 dark:bg-orange-950/40 border border-orange-200 dark:border-orange-900/50 flex items-center justify-between gap-2 text-xs text-orange-900 dark:text-orange-200 font-medium">
                        <div className="flex items-center gap-2">
                          <AlertTriangle className="w-4 h-4 text-orange-600 shrink-0" />
                          <span>
                            {locale === 'vi-VN'
                              ? 'Quản trị viên cần bạn bổ sung thêm thông tin. Nhấn vào đây để xem chi tiết và phản hồi.'
                              : 'The administrator needs additional information from you. Open this request to review and reply.'}
                          </span>
                        </div>
                        <span className="underline font-bold shrink-0">
                          {locale === 'vi-VN' ? 'Phản hồi ngay' : 'Reply now'}
                        </span>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 2: TRANSACTION DISPUTES LIST                                          */}
      {/* ========================================================================= */}
      {activeTab === 'disputes' && (
        <div className="space-y-4">
          {isLoading ? (
            <div className="flex items-center justify-center py-12 gap-2 text-slate-500 text-xs">
              <RefreshCw className="h-4 w-4 animate-spin text-rose-600" />
              <span>{locale === 'vi-VN' ? 'Đang tải danh sách khiếu nại...' : 'Loading disputes...'}</span>
            </div>
          ) : loadError ? (
            <div className="p-8 text-center bg-rose-50/50 dark:bg-rose-950/20 rounded-3xl border border-rose-200 dark:border-rose-900/50 space-y-3">
              <div className="w-10 h-10 mx-auto rounded-xl bg-rose-100 dark:bg-rose-900/40 text-rose-600 dark:text-rose-400 flex items-center justify-center">
                <AlertCircle className="w-5 h-5" />
              </div>
              <h3 className="text-sm font-bold text-rose-900 dark:text-rose-200">
                {locale === 'vi-VN' ? 'Không thể tải danh sách khiếu nại giao dịch' : 'Failed to load transaction disputes'}
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
                <span>{locale === 'vi-VN' ? 'Thử lại' : 'Retry'}</span>
              </Button>
            </div>
          ) : disputes.length === 0 ? (
            <div className="max-w-md mx-auto p-7 text-center bg-white dark:bg-[#151D2F] rounded-2xl border border-dashed border-slate-300 dark:border-slate-700/80 shadow-xs space-y-3.5 my-4">
              <div className="w-11 h-11 mx-auto rounded-xl bg-rose-50 dark:bg-rose-950/40 text-rose-600 dark:text-rose-400 border border-rose-100 dark:border-rose-900/40 flex items-center justify-center">
                <ShieldAlert className="w-5 h-5" />
              </div>
              <div className="space-y-1">
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  {locale === 'vi-VN' ? 'Chưa có khiếu nại giao dịch' : 'No transaction disputes yet'}
                </h3>
                <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed max-w-xs mx-auto">
                  {locale === 'vi-VN'
                    ? 'Bạn chưa có khiếu nại nào. Nếu phát hiện giao dịch bất thường, hãy gửi khiếu nại để được đối soát.'
                    : 'No disputes submitted. If you notice an unusual transaction, submit a dispute for verification.'}
                </p>
              </div>
              <Button
                size="sm"
                onClick={() => {
                  setDisputeError(null);
                  setIsCreateDisputeOpen(true);
                }}
                className="bg-rose-600 hover:bg-rose-700 text-white font-bold gap-1.5 text-xs shadow-xs cursor-pointer py-2 px-3.5 mx-auto rounded-xl"
              >
                <Plus className="w-3.5 h-3.5" />
                <span>{locale === 'vi-VN' ? 'Gửi khiếu nại giao dịch' : 'Submit Transaction Dispute'}</span>
              </Button>
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-3">
              {disputes.map((d) => {
                const badge = getDisputeStatusBadge(d.status);
                const isNeedMoreInfo = d.status === 'need_more_info';

                return (
                  <div
                    key={d.id}
                    onClick={() => setSelectedDispute(d)}
                    className={`p-5 rounded-2xl bg-white dark:bg-[#151D2F] border transition-all cursor-pointer hover:shadow-md hover:border-slate-300 dark:hover:border-slate-700 ${
                      isNeedMoreInfo
                        ? 'border-orange-300 dark:border-orange-800/80 bg-orange-50/20 dark:bg-orange-950/10'
                        : 'border-slate-200/80 dark:border-slate-800/80 shadow-xs'
                    }`}
                  >
                    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
                      <div className="space-y-1 min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-mono text-xs font-bold text-slate-500 dark:text-slate-400">
                            {d.disputeCode || d.id}
                          </span>
                          <span className="px-2 py-0.5 rounded-full text-[10px] font-bold uppercase bg-purple-100 text-purple-800 dark:bg-purple-950 dark:text-purple-300">
                            {getReasonLabel(d.reason)}
                          </span>
                          <span className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold border ${badge.className}`}>
                            {badge.label}
                          </span>
                          {d.attachments && d.attachments.length > 0 && (
                            <span className="text-[10px] font-semibold text-slate-400">
                              • {d.attachments.length}{' '}
                              {locale === 'vi-VN'
                                ? 'tài liệu đối soát'
                                : d.attachments.length === 1
                                ? 'evidence file'
                                : 'evidence files'}
                            </span>
                          )}
                        </div>

                        <div className="flex items-center gap-2">
                          <h3 className="font-bold text-sm text-slate-900 dark:text-white">
                            Tx #{d.transactionRef}
                          </h3>
                          <span className="text-xs font-bold text-rose-600 dark:text-rose-400 font-mono">
                            ({formatMoney(d.transactionAmount, locale)})
                          </span>
                          {d.transactionTitle && (
                            <span className="text-xs text-slate-500 dark:text-slate-400 truncate">
                              - {d.transactionTitle}
                            </span>
                          )}
                        </div>

                        <p className="text-xs text-slate-500 dark:text-slate-400 line-clamp-1">
                          {d.description}
                        </p>
                      </div>

                      <div className="flex items-center gap-3 shrink-0">
                        <div className="text-right">
                          <span className="text-[11px] text-slate-400 font-mono block">
                            {formatDate(d.createdAt)}
                          </span>
                          {d.history && d.history.length > 1 && (
                            <span className="text-[10px] text-emerald-600 dark:text-emerald-400 font-semibold">
                              {d.history.length}{' '}
                              {locale === 'vi-VN'
                                ? 'cập nhật'
                                : d.history.length === 1
                                ? 'update'
                                : 'updates'}
                            </span>
                          )}
                        </div>
                        <ChevronRight className="w-4 h-4 text-slate-400" />
                      </div>
                    </div>

                    {isNeedMoreInfo && (
                      <div className="mt-3 p-3 rounded-xl bg-orange-100/70 dark:bg-orange-950/40 border border-orange-200 dark:border-orange-900/50 flex items-center justify-between gap-2 text-xs text-orange-900 dark:text-orange-200 font-medium">
                        <div className="flex items-center gap-2">
                          <AlertTriangle className="w-4 h-4 text-orange-600 shrink-0" />
                          <span>
                            {locale === 'vi-VN'
                              ? 'Quản trị viên cần bạn bổ sung thêm chứng từ đối soát giao dịch.'
                              : 'The administrator needs additional evidence for this dispute.'}
                          </span>
                        </div>
                        <span className="underline font-bold shrink-0">
                          {locale === 'vi-VN' ? 'Bổ sung ngay' : 'Add information'}
                        </span>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 1: CREATE SUPPORT CASE                                              */}
      {/* ========================================================================= */}
      {isCreateCaseOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in">
          <form
            onSubmit={handleCreateCaseSubmit}
            className="w-full max-w-xl bg-white dark:bg-[#151D2F] rounded-2xl p-6 border border-slate-200 dark:border-slate-800 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto"
          >
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2.5">
                <div className="p-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 border border-emerald-100 dark:border-emerald-900/40">
                  <LifeBuoy className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {locale === 'vi-VN' ? 'Tạo yêu cầu hỗ trợ' : 'New Support Request'}
                  </h3>
                  <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                    {locale === 'vi-VN'
                      ? 'Mô tả vấn đề bạn gặp phải để SpendWise có đủ thông tin hỗ trợ.'
                      : 'Describe the issue you are experiencing so SpendWise has enough information to assist.'}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setIsCreateCaseOpen(false)}
                className="p-1.5 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {caseError && (
              <div className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="w-4 h-4 shrink-0" />
                <span>{caseError}</span>
              </div>
            )}

            <div className="space-y-3.5 text-xs">
              {/* GROUP 1: Thông tin yêu cầu */}
              <div className="p-4 rounded-xl bg-slate-50/70 dark:bg-slate-900/40 border border-slate-200/80 dark:border-slate-800/80 space-y-3">
                <div className="text-[11px] font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
                  1. {locale === 'vi-VN' ? 'Thông tin yêu cầu' : 'Request Information'}
                </div>

                {/* Category */}
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Phân loại vấn đề *' : 'Category *'}
                  </label>
                  <Select
                    value={caseCategory}
                    onChange={(val) => setCaseCategory(val as SupportCaseCategory)}
                    accentColor="emerald"
                  >
                    <option value="system_error">{locale === 'vi-VN' ? 'Lỗi hệ thống' : 'System Error'}</option>
                    <option value="account">{locale === 'vi-VN' ? 'Tài khoản' : 'Account'}</option>
                    <option value="banking_sepay">{locale === 'vi-VN' ? 'Ngân hàng / SePay' : 'Banking / SePay'}</option>
                    <option value="transaction_edit">{locale === 'vi-VN' ? 'Yêu cầu chỉnh sửa giao dịch' : 'Transaction Edit Request'}</option>
                    <option value="other">{locale === 'vi-VN' ? 'Khác' : 'Other'}</option>
                  </Select>
                </div>

                {/* Title */}
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Tiêu đề yêu cầu *' : 'Subject *'}
                  </label>
                  <input
                    type="text"
                    placeholder={locale === 'vi-VN' ? 'Ví dụ: Lỗi không đồng bộ số dư SePay...' : 'e.g. Balance sync delay...'}
                    value={caseTitle}
                    onChange={(e) => setCaseTitle(e.target.value)}
                    required
                    className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white font-medium"
                  />
                </div>

                {/* Optional Linked Transaction */}
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {caseCategory === 'transaction_edit'
                      ? (locale === 'vi-VN' ? 'Giao dịch thu/chi cần cấp thêm lượt sửa *' : 'Manual transaction to request extra correction *')
                      : (locale === 'vi-VN' ? 'Giao dịch liên quan (Tùy chọn)' : 'Linked Transaction (Optional)')}
                  </label>
                  <Select
                    value={caseLinkedTxId}
                    onChange={(val) => setCaseLinkedTxId(val)}
                    accentColor="emerald"
                  >
                    <option value="">
                      {caseCategory === 'transaction_edit'
                        ? (locale === 'vi-VN' ? '-- Chọn giao dịch thu/chi thủ công của bạn --' : '-- Select your manual transaction --')
                        : (locale === 'vi-VN' ? '-- Không liên kết giao dịch --' : '-- No linked transaction --')}
                    </option>
                    {(caseCategory === 'transaction_edit'
                      ? transactions.filter(
                          (tx) => tx.userId === currentUser?.id && tx.isManualEntry === true && (tx.type === 'income' || tx.type === 'expense')
                        )
                      : userTransactions
                    ).map((tx) => (
                      <option key={tx.id} value={tx.id}>
                        {tx.referenceCode || tx.id} - {tx.title} ({formatMoney(tx.amount, locale)})
                      </option>
                    ))}
                  </Select>
                </div>
              </div>

              {/* GROUP 2: Chi tiết & tệp đính kèm */}
              <div className="p-4 rounded-xl bg-slate-50/70 dark:bg-slate-900/40 border border-slate-200/80 dark:border-slate-800/80 space-y-3">
                <div className="text-[11px] font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
                  2. {locale === 'vi-VN' ? 'Chi tiết & tệp đính kèm' : 'Details & Attachments'}
                </div>

                {/* Description */}
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Mô tả chi tiết *' : 'Detailed Description *'}
                  </label>
                  <textarea
                    rows={4}
                    placeholder={
                      locale === 'vi-VN'
                        ? 'Mô tả chi tiết tình huống bạn gặp phải, các bước dẫn đến lỗi để quản trị viên kiểm tra...'
                        : 'Describe your issue in detail for our support team...'
                    }
                    value={caseDescription}
                    onChange={(e) => setCaseDescription(e.target.value)}
                    required
                    className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white font-medium resize-none leading-relaxed"
                  />
                </div>

                {/* Attachments */}
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Ảnh chụp màn hình / Tài liệu đính kèm' : 'Attachments / Evidence'}
                  </label>
                  <AttachmentUploader
                    attachments={caseAttachments}
                    onChange={setCaseAttachments}
                  />
                </div>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={() => setIsCreateCaseOpen(false)}
                className="cursor-pointer rounded-xl"
              >
                {t('common.cancel')}
              </Button>
              <Button
                type="submit"
                disabled={caseSubmitting}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer rounded-xl"
              >
                {caseSubmitting ? (
                  <RefreshCw className="w-4 h-4 animate-spin" />
                ) : (
                  <span>{locale === 'vi-VN' ? 'Gửi yêu cầu hỗ trợ' : 'Submit Support Request'}</span>
                )}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 2: CREATE TRANSACTION DISPUTE                                       */}
      {/* ========================================================================= */}
      {isCreateDisputeOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in">
          <form
            onSubmit={handleCreateDisputeSubmit}
            className="w-full max-w-xl bg-white dark:bg-[#151D2F] rounded-2xl p-6 border border-slate-200 dark:border-slate-800 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto"
          >
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2.5">
                <div className="p-2 rounded-xl bg-rose-50 dark:bg-rose-950/40 text-rose-600 dark:text-rose-400 border border-rose-100 dark:border-rose-900/40">
                  <ShieldAlert className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {locale === 'vi-VN' ? 'Gửi khiếu nại giao dịch' : 'Submit Transaction Dispute'}
                  </h3>
                  <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                    {locale === 'vi-VN'
                      ? 'Hồ sơ sẽ được chuyển tới phòng nghiệp vụ để kiểm tra và đối soát.'
                      : 'Dispute will be reviewed and verified by operations team.'}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setIsCreateDisputeOpen(false)}
                className="p-1.5 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {disputeError && (
              <div className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="w-4 h-4 shrink-0" />
                <span>{disputeError}</span>
              </div>
            )}

            <div className="space-y-3.5 text-xs">
              {/* GROUP 1: Thông tin giao dịch & lý do */}
              <div className="p-4 rounded-xl bg-slate-50/70 dark:bg-slate-900/40 border border-slate-200/80 dark:border-slate-800/80 space-y-3">
                <div className="text-[11px] font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
                  1. {locale === 'vi-VN' ? 'Thông tin giao dịch khiếu nại' : 'Dispute Information'}
                </div>

                {/* Transaction Selector (Only user's own transactions) */}
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Chọn giao dịch khiếu nại *' : 'Select Transaction to Dispute *'}
                  </label>
                  <Select
                    value={disputeTxId}
                    onChange={(val) => setDisputeTxId(val)}
                    accentColor="emerald"
                  >
                    <option value="">{locale === 'vi-VN' ? '-- Chọn giao dịch của bạn --' : '-- Select your transaction --'}</option>
                    {userTransactions.map((tx) => (
                      <option key={tx.id} value={tx.id}>
                        {tx.referenceCode || tx.id} - {tx.title} ({formatMoney(tx.amount, locale)})
                      </option>
                    ))}
                  </Select>
                </div>

                {/* Reason */}
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Lý do khiếu nại *' : 'Dispute Reason *'}
                  </label>
                  <Select
                    value={disputeReason}
                    onChange={(val) => setDisputeReason(val as DisputeReason)}
                    accentColor="emerald"
                  >
                    <option value="unrecognized_charge">{locale === 'vi-VN' ? 'Giao dịch lạ / Nghi vấn gian lận' : 'Unrecognized / Fraudulent Charge'}</option>
                    <option value="duplicate_deduction">{locale === 'vi-VN' ? 'Bị trừ tiền 2 lần / Trùng lặp' : 'Duplicate Charge'}</option>
                    <option value="incorrect_amount">{locale === 'vi-VN' ? 'Sai số tiền giao dịch' : 'Incorrect Amount'}</option>
                    <option value="goods_not_received">{locale === 'vi-VN' ? 'Chưa nhận được hàng / dịch vụ' : 'Goods Not Received'}</option>
                    <option value="merchant_dispute">{locale === 'vi-VN' ? 'Bất đồng với bên bán / Giao dịch lỗi' : 'Merchant Dispute'}</option>
                    <option value="other">{locale === 'vi-VN' ? 'Lý do khác' : 'Other Reason'}</option>
                  </Select>
                </div>
              </div>

              {/* GROUP 2: Giải trình & chứng từ */}
              <div className="p-4 rounded-xl bg-slate-50/70 dark:bg-slate-900/40 border border-slate-200/80 dark:border-slate-800/80 space-y-3">
                <div className="text-[11px] font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
                  2. {locale === 'vi-VN' ? 'Giải trình & chứng từ đối soát' : 'Explanation & Evidence'}
                </div>

                {/* Description */}
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Mô tả chi tiết và giải trình *' : 'Detailed Explanation *'}
                  </label>
                  <textarea
                    rows={4}
                    placeholder={
                      locale === 'vi-VN'
                        ? 'Nêu rõ tình huống phát sinh, hóa đơn hoặc sự sai lệch để đối soát...'
                        : 'Provide details, invoice reference or unexpected charges...'
                    }
                    value={disputeDescription}
                    onChange={(e) => setDisputeDescription(e.target.value)}
                    required
                    className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white font-medium resize-none leading-relaxed"
                  />
                </div>

                {/* Attachments */}
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Biên lai / Chứng từ / Ảnh chụp bằng chứng' : 'Receipt / Evidence Attachments'}
                  </label>
                  <AttachmentUploader
                    attachments={disputeAttachments}
                    onChange={setDisputeAttachments}
                  />
                </div>

                {/* Important notice */}
                <div className="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200/60 dark:border-amber-900/40 flex items-start gap-2.5 text-amber-800 dark:text-amber-300 text-xs leading-relaxed font-medium">
                  <AlertTriangle className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" />
                  <span>
                    {locale === 'vi-VN'
                      ? 'Lưu ý: Yêu cầu khiếu nại sẽ được kiểm tra và đối soát. Việc gửi yêu cầu không đồng nghĩa giao dịch sẽ được hoàn tiền ngay.'
                      : 'Note: The dispute will be reviewed and verified. Submitting a dispute does not mean the transaction will be refunded immediately.'}
                  </span>
                </div>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={() => setIsCreateDisputeOpen(false)}
                className="cursor-pointer rounded-xl"
              >
                {t('common.cancel')}
              </Button>
              <Button
                type="submit"
                disabled={disputeSubmitting}
                className="bg-rose-600 hover:bg-rose-700 text-white font-bold cursor-pointer rounded-xl"
              >
                {disputeSubmitting ? (
                  <RefreshCw className="w-4 h-4 animate-spin" />
                ) : (
                  <span>{locale === 'vi-VN' ? 'Gửi hồ sơ khiếu nại' : 'Submit Dispute'}</span>
                )}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* ========================================================================= */}
      {/* DETAIL MODAL 1: VIEW SUPPORT CASE & REPLY                                */}
      {/* ========================================================================= */}
      {selectedCase && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in">
          <div className="w-full max-w-2xl bg-white dark:bg-[#151D2F] rounded-3xl p-6 sm:p-7 border border-slate-200 dark:border-slate-800 shadow-2xl space-y-5 max-h-[90vh] overflow-y-auto">
            {/* Header */}
            <div className="flex items-start justify-between gap-3 pb-3 border-b border-slate-100 dark:border-slate-800">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <span className="font-mono text-xs font-bold text-slate-500">
                    {selectedCase.ticketNumber || selectedCase.id}
                  </span>
                  <span className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold border ${getCaseStatusBadge(selectedCase.status).className}`}>
                    {getCaseStatusBadge(selectedCase.status).label}
                  </span>
                  <span className="text-[10px] font-bold uppercase text-slate-400">
                    • {getCategoryLabel(selectedCase.category)}
                  </span>
                </div>
                <h2 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white">
                  {selectedCase.title || selectedCase.subject}
                </h2>
                <span className="text-[11px] text-slate-400 font-mono">
                  {locale === 'vi-VN' ? 'Ngày gửi:' : 'Submitted:'} {formatDate(selectedCase.createdAt)}
                </span>
              </div>
              <button
                type="button"
                onClick={() => setSelectedCase(null)}
                className="p-1.5 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Content & History Timeline */}
            <div className="space-y-4 text-xs">
              {/* Linked Tx if any */}
              {selectedCase.linkedTransactionRef && (
                <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200/60 dark:border-slate-800 flex items-center justify-between">
                  <span className="text-slate-500 font-semibold">
                    {locale === 'vi-VN' ? 'Giao dịch liên quan:' : 'Linked transaction:'}
                  </span>
                  <span className="font-mono font-bold text-slate-800 dark:text-slate-200">
                    #{selectedCase.linkedTransactionRef}
                  </span>
                </div>
              )}

              {/* History Timeline */}
              <div className="space-y-3">
                <div className="text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                  {locale === 'vi-VN' ? 'Tiến độ & Lịch sử trao đổi' : 'Progress & Conversation History'} ({selectedCase.history?.length || 1})
                </div>

                <div className="space-y-3">
                  {(selectedCase.history || []).map((h, idx) => {
                    const isAdmin = h.actorRole === 'SystemAdmin';
                    return (
                      <div
                        key={h.id || idx}
                        className={`p-4 rounded-2xl border space-y-2 ${
                          isAdmin
                            ? 'bg-blue-50/50 dark:bg-blue-950/20 border-blue-100 dark:border-blue-900/40 text-blue-950 dark:text-blue-200'
                            : 'bg-slate-50 dark:bg-slate-900/50 border-slate-200/60 dark:border-slate-800 text-slate-900 dark:text-slate-200'
                        }`}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <div className="flex items-center gap-2">
                            <span
                              className={`px-2 py-0.5 rounded-md text-[10px] font-bold ${
                                isAdmin
                                  ? 'bg-blue-600 text-white'
                                  : 'bg-slate-200 dark:bg-slate-800 text-slate-700 dark:text-slate-300'
                              }`}
                            >
                              {isAdmin
                                ? (locale === 'vi-VN' ? 'Quản trị viên SpendWise' : 'SpendWise Administrator')
                                : (locale === 'vi-VN' ? 'Bạn (Người dùng)' : 'You')}
                            </span>
                            <span className="font-bold text-xs">{h.actorName}</span>
                          </div>
                          <span className="text-[10px] text-slate-400 font-mono">
                            {formatDate(h.timestamp)}
                          </span>
                        </div>

                        <p className="text-xs leading-relaxed whitespace-pre-wrap">
                          {h.message}
                        </p>

                        {/* Attachments if any */}
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

              {/* Initial Case Attachments if not in history */}
              {(!selectedCase.history || selectedCase.history.length === 0) && selectedCase.attachments && (
                <AttachmentList
                  attachments={selectedCase.attachments}
                  loadAttachmentBlob={loadAttachmentBlob}
                />
              )}

              {/* Special Retry Banner if initial attachment upload failed for this specific case */}
              {pendingAttachmentRetry && pendingAttachmentRetry.targetType === 'case' && pendingAttachmentRetry.targetId === selectedCase.id && (
                <div className="p-3.5 rounded-2xl bg-amber-50 dark:bg-amber-950/30 border border-amber-300 dark:border-amber-800/80 space-y-2">
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex items-center gap-2 text-amber-900 dark:text-amber-200 font-bold text-xs">
                      <AlertTriangle className="w-4 h-4 text-amber-600 shrink-0" />
                      <span>
                        {locale === 'vi-VN'
                          ? `Yêu cầu (${selectedCase.ticketNumber || selectedCase.id}) đã được tạo trên hệ thống, nhưng ${pendingAttachmentRetry.attachments.length} tệp đính kèm chưa tải lên thành công.`
                          : `Ticket (${selectedCase.ticketNumber || selectedCase.id}) was created, but ${pendingAttachmentRetry.attachments.length} attachments failed to upload.`}
                      </span>
                    </div>
                    <button
                      type="button"
                      onClick={() => {
                        setPendingAttachmentRetry(null);
                        setReplyAttachments([]);
                      }}
                      className="text-[11px] text-slate-500 hover:text-rose-600 underline font-medium cursor-pointer shrink-0"
                    >
                      {locale === 'vi-VN' ? 'Hủy bỏ tệp này' : 'Discard files'}
                    </button>
                  </div>
                  <p className="text-[11px] text-amber-800 dark:text-amber-300/90 leading-relaxed">
                    {locale === 'vi-VN'
                      ? 'Các tệp bạn vừa chọn đã được lưu giữ lại bên dưới. Nhấn "Gửi tệp đính kèm ngay" để gửi lại tệp lên vé hỗ trợ này mà không tạo vé trùng lặp.'
                      : 'Your selected files have been retained below. Click submit to attach them to this ticket without creating a duplicate.'}
                  </p>
                </div>
              )}

              {/* User Reply Form if case is active (need_more_info, pending, open, in_progress) */}
              {selectedCase.status !== 'resolved' && selectedCase.status !== 'closed' && selectedCase.status !== 'rejected' ? (
                <div className={`p-4 rounded-2xl border space-y-3 ${
                  selectedCase.status === 'need_more_info'
                    ? 'bg-orange-50 dark:bg-orange-950/30 border-orange-200 dark:border-orange-900/50'
                    : 'bg-slate-50/80 dark:bg-slate-900/60 border-slate-200 dark:border-slate-800'
                }`}>
                  <div className="flex items-center gap-2 font-bold text-xs">
                    {selectedCase.status === 'need_more_info' ? (
                      <>
                        <AlertTriangle className="w-4 h-4 text-orange-600 shrink-0" />
                        <span className="text-orange-900 dark:text-orange-200">
                          {locale === 'vi-VN'
                            ? 'Quản trị viên yêu cầu bổ sung thông tin. Vui lòng phản hồi bên dưới:'
                            : 'The administrator requested additional information. Please reply below:'}
                        </span>
                      </>
                    ) : (
                      <>
                        <MessageSquare className="w-4 h-4 text-emerald-600 shrink-0" />
                        <span className="text-slate-800 dark:text-slate-200">
                          {locale === 'vi-VN'
                            ? 'Gửi thêm phản hồi hoặc bổ sung tệp đính kèm vào yêu cầu:'
                            : 'Send additional reply or attach documents to this ticket:'}
                        </span>
                      </>
                    )}
                  </div>

                  {replyError && (
                    <div className="p-2 rounded-xl bg-rose-50 text-rose-700 text-xs font-semibold">
                      {replyError}
                    </div>
                  )}

                  <textarea
                    rows={3}
                    placeholder={
                      locale === 'vi-VN'
                        ? 'Nhập nội dung trao đổi hoặc giải thích thêm...'
                        : 'Enter additional information or explanation...'
                    }
                    value={replyMessage}
                    onChange={(e) => setReplyMessage(e.target.value)}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-medium resize-none text-xs"
                  />

                  <AttachmentUploader
                    attachments={replyAttachments}
                    onChange={setReplyAttachments}
                  />

                  <div className="flex items-center justify-between pt-1">
                    {pendingAttachmentRetry && pendingAttachmentRetry.targetId === selectedCase.id ? (
                      <span className="text-[11px] text-amber-700 dark:text-amber-400 font-medium">
                        {locale === 'vi-VN' ? 'Đang sẵn sàng tải lại tệp đính kèm' : 'Ready to retry attachments'}
                      </span>
                    ) : <span />}

                    <Button
                      size="sm"
                      onClick={() => handleUserReplyCase(selectedCase.id)}
                      disabled={replySubmitting}
                      className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold gap-1.5 cursor-pointer text-xs"
                    >
                      {replySubmitting ? (
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                      ) : (
                        <Send className="w-3.5 h-3.5" />
                      )}
                      <span>
                        {pendingAttachmentRetry && pendingAttachmentRetry.targetId === selectedCase.id
                          ? (locale === 'vi-VN' ? 'Tải lên tệp đính kèm ngay' : 'Upload attachments now')
                          : (locale === 'vi-VN' ? 'Gửi phản hồi / tệp' : 'Submit reply / files')}
                      </span>
                    </Button>
                  </div>
                </div>
              ) : selectedCase.status === 'rejected' ? (
                <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/20 border border-rose-200/60 dark:border-rose-900/40 text-rose-800 dark:text-rose-300 text-xs flex items-center gap-2">
                  <AlertCircle className="w-4 h-4 text-rose-600 shrink-0" />
                  <span>
                    {locale === 'vi-VN'
                      ? 'Yêu cầu hỗ trợ này đã bị từ chối. Xem lịch sử xử lý để biết lý do.'
                      : 'This support request was rejected. See the case history for the reason.'}
                  </span>
                </div>
              ) : (
                <div className="p-3 rounded-2xl bg-emerald-50 dark:bg-emerald-950/20 border border-emerald-200/60 dark:border-emerald-900/40 text-emerald-800 dark:text-emerald-300 text-xs flex items-center gap-2">
                  <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                  <span>
                    {locale === 'vi-VN'
                      ? 'Yêu cầu hỗ trợ này đã hoàn tất giải quyết hoặc đã đóng.'
                      : 'This support request has been fully resolved or closed.'}
                  </span>
                </div>
              )}
            </div>

            <div className="flex items-center justify-end pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                variant="outline"
                onClick={() => setSelectedCase(null)}
                className="cursor-pointer text-xs"
              >
                {t('common.close')}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* DETAIL MODAL 2: VIEW DISPUTE & REPLY                                     */}
      {/* ========================================================================= */}
      {selectedDispute && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in">
          <div className="w-full max-w-2xl bg-white dark:bg-[#151D2F] rounded-3xl p-6 sm:p-7 border border-slate-200 dark:border-slate-800 shadow-2xl space-y-5 max-h-[90vh] overflow-y-auto">
            {/* Header */}
            <div className="flex items-start justify-between gap-3 pb-3 border-b border-slate-100 dark:border-slate-800">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <span className="font-mono text-xs font-bold text-slate-500">
                    {selectedDispute.disputeCode || selectedDispute.id}
                  </span>
                  <span className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold border ${getDisputeStatusBadge(selectedDispute.status).className}`}>
                    {getDisputeStatusBadge(selectedDispute.status).label}
                  </span>
                  <span className="text-[10px] font-bold uppercase text-purple-700 dark:text-purple-300">
                    • {getReasonLabel(selectedDispute.reason)}
                  </span>
                </div>
                <h2 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white">
                  {locale === 'vi-VN'
                    ? `Khiếu nại giao dịch #${selectedDispute.transactionRef}`
                    : `Transaction Dispute #${selectedDispute.transactionRef}`}
                </h2>
                <span className="text-[11px] text-slate-400 font-mono">
                  {locale === 'vi-VN' ? 'Ngày gửi khiếu nại:' : 'Dispute filed:'} {formatDate(selectedDispute.createdAt)}
                </span>
              </div>
              <button
                type="button"
                onClick={() => setSelectedDispute(null)}
                className="p-1.5 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Dispute Details */}
            <div className="space-y-4 text-xs">
              {/* Real Transaction Card */}
              <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200/60 dark:border-slate-800 space-y-2">
                <div className="flex items-center justify-between text-[11px] text-slate-400 font-bold uppercase">
                  <span>{locale === 'vi-VN' ? 'Thông tin giao dịch gốc' : 'Original Transaction Details'}</span>
                  <span className="font-mono">ID: {selectedDispute.transactionId}</span>
                </div>
                <div className="flex items-center justify-between">
                  <div>
                    <span className="font-bold text-sm text-slate-900 dark:text-white block">
                      {selectedDispute.transactionTitle ||
                        (locale === 'vi-VN' ? 'Giao dịch SpendWise' : 'SpendWise Transaction')}
                    </span>
                    <span className="text-[11px] text-slate-500 font-mono">
                      Ref: #{selectedDispute.transactionRef} • {locale === 'vi-VN' ? 'Ngày:' : 'Date:'} {selectedDispute.transactionDate}
                    </span>
                  </div>
                  <span className="text-base font-bold text-rose-600 dark:text-rose-400 font-mono tabular-nums">
                    {formatMoney(selectedDispute.transactionAmount, locale)}
                  </span>
                </div>
              </div>

              {/* History Timeline */}
              <div className="space-y-3">
                <div className="text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                  {locale === 'vi-VN' ? 'Tiến độ đối soát & Nhật ký trao đổi' : 'Dispute Progress & Review History'} ({selectedDispute.history?.length || 1})
                </div>

                <div className="space-y-3">
                  {(selectedDispute.history || []).map((h, idx) => {
                    const isAdmin = h.actorRole === 'SystemAdmin';
                    return (
                      <div
                        key={h.id || idx}
                        className={`p-4 rounded-2xl border space-y-2 ${
                          isAdmin
                            ? 'bg-blue-50/50 dark:bg-blue-950/20 border-blue-100 dark:border-blue-900/40 text-blue-950 dark:text-blue-200'
                            : 'bg-slate-50 dark:bg-slate-900/50 border-slate-200/60 dark:border-slate-800 text-slate-900 dark:text-slate-200'
                        }`}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <div className="flex items-center gap-2">
                            <span
                              className={`px-2 py-0.5 rounded-md text-[10px] font-bold ${
                                isAdmin
                                  ? 'bg-blue-600 text-white'
                                  : 'bg-slate-200 dark:bg-slate-800 text-slate-700 dark:text-slate-300'
                              }`}
                            >
                              {isAdmin
                                ? (locale === 'vi-VN' ? 'Quản trị viên Đối soát' : 'Dispute Reviewer')
                                : (locale === 'vi-VN' ? 'Bạn (Người khiếu nại)' : 'You (Claimant)')}
                            </span>
                            <span className="font-bold text-xs">{h.actorName}</span>
                          </div>
                          <span className="text-[10px] text-slate-400 font-mono">
                            {formatDate(h.timestamp)}
                          </span>
                        </div>

                        <p className="text-xs leading-relaxed whitespace-pre-wrap">
                          {h.message}
                        </p>

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

              {/* Special Retry Banner if initial attachment upload failed for this specific dispute */}
              {pendingAttachmentRetry && pendingAttachmentRetry.targetType === 'dispute' && pendingAttachmentRetry.targetId === selectedDispute.id && (
                <div className="p-3.5 rounded-2xl bg-amber-50 dark:bg-amber-950/30 border border-amber-300 dark:border-amber-800/80 space-y-2">
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex items-center gap-2 text-amber-900 dark:text-amber-200 font-bold text-xs">
                      <AlertTriangle className="w-4 h-4 text-amber-600 shrink-0" />
                      <span>
                        {locale === 'vi-VN'
                          ? `Hồ sơ khiếu nại (${selectedDispute.disputeCode || selectedDispute.id}) đã được tạo trên hệ thống, nhưng ${pendingAttachmentRetry.attachments.length} tài liệu đối soát chưa tải lên thành công.`
                          : `Dispute (${selectedDispute.disputeCode || selectedDispute.id}) was created, but ${pendingAttachmentRetry.attachments.length} evidence attachments failed to upload.`}
                      </span>
                    </div>
                    <button
                      type="button"
                      onClick={() => {
                        setPendingAttachmentRetry(null);
                        setReplyAttachments([]);
                      }}
                      className="text-[11px] text-slate-500 hover:text-rose-600 underline font-medium cursor-pointer shrink-0"
                    >
                      {locale === 'vi-VN' ? 'Hủy bỏ tài liệu này' : 'Discard documents'}
                    </button>
                  </div>
                  <p className="text-[11px] text-amber-800 dark:text-amber-300/90 leading-relaxed">
                    {locale === 'vi-VN'
                      ? 'Các chứng từ bạn vừa chọn đã được lưu giữ lại bên dưới. Nhấn "Gửi tài liệu đối soát ngay" để bổ sung vào hồ sơ khiếu nại này mà không tạo hồ sơ mới.'
                      : 'Your selected evidence files have been retained below. Click submit to attach them to this dispute without creating a new record.'}
                  </p>
                </div>
              )}

              {/* User Reply Form if dispute is active (need_more_info, pending, under_review, in_review) */}
              {selectedDispute.status !== 'resolved' && selectedDispute.status !== 'rejected' ? (
                <div className={`p-4 rounded-2xl border space-y-3 ${
                  selectedDispute.status === 'need_more_info'
                    ? 'bg-orange-50 dark:bg-orange-950/30 border-orange-200 dark:border-orange-900/50'
                    : 'bg-slate-50/80 dark:bg-slate-900/60 border-slate-200 dark:border-slate-800'
                }`}>
                  <div className="flex items-center gap-2 font-bold text-xs">
                    {selectedDispute.status === 'need_more_info' ? (
                      <>
                        <AlertTriangle className="w-4 h-4 text-orange-600 shrink-0" />
                        <span className="text-orange-900 dark:text-orange-200">
                          {locale === 'vi-VN'
                            ? 'Quản trị viên cần thêm bằng chứng đối soát. Vui lòng phản hồi bổ sung:'
                            : 'The administrator needs additional evidence. Please reply below:'}
                        </span>
                      </>
                    ) : (
                      <>
                        <ShieldAlert className="w-4 h-4 text-purple-600 shrink-0" />
                        <span className="text-slate-800 dark:text-slate-200">
                          {locale === 'vi-VN'
                            ? 'Bổ sung giải trình hoặc gửi chứng từ / biên lai đối soát:'
                            : 'Provide additional explanation or submit evidence / receipts:'}
                        </span>
                      </>
                    )}
                  </div>

                  {replyError && (
                    <div className="p-2 rounded-xl bg-rose-50 text-rose-700 text-xs font-semibold">
                      {replyError}
                    </div>
                  )}

                  <textarea
                    rows={3}
                    placeholder={
                      locale === 'vi-VN'
                        ? 'Giải trình thêm thông tin hoặc đính kèm ảnh chụp sao kê, hóa đơn...'
                        : 'Provide further explanation or attach bank statements, invoices...'
                    }
                    value={replyMessage}
                    onChange={(e) => setReplyMessage(e.target.value)}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-medium resize-none text-xs"
                  />

                  <AttachmentUploader
                    attachments={replyAttachments}
                    onChange={setReplyAttachments}
                  />

                  <div className="flex items-center justify-between pt-1">
                    {pendingAttachmentRetry && pendingAttachmentRetry.targetId === selectedDispute.id ? (
                      <span className="text-[11px] text-amber-700 dark:text-amber-400 font-medium">
                        {locale === 'vi-VN' ? 'Đang sẵn sàng tải lại tài liệu đối soát' : 'Ready to retry evidence upload'}
                      </span>
                    ) : <span />}

                    <Button
                      size="sm"
                      onClick={() => handleUserReplyDispute(selectedDispute.id)}
                      disabled={replySubmitting}
                      className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold gap-1.5 cursor-pointer text-xs"
                    >
                      {replySubmitting ? (
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                      ) : (
                        <Send className="w-3.5 h-3.5" />
                      )}
                      <span>
                        {pendingAttachmentRetry && pendingAttachmentRetry.targetId === selectedDispute.id
                          ? (locale === 'vi-VN' ? 'Gửi tài liệu đối soát ngay' : 'Upload evidence now')
                          : (locale === 'vi-VN' ? 'Gửi tài liệu bổ sung' : 'Submit Additional Evidence')}
                      </span>
                    </Button>
                  </div>
                </div>
              ) : selectedDispute.status === 'resolved' ? (
                <div className="p-3 rounded-2xl bg-emerald-50 dark:bg-emerald-950/20 border border-emerald-200/60 dark:border-emerald-900/40 text-emerald-800 dark:text-emerald-300 text-xs flex items-center gap-2">
                  <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                  <span>
                    {locale === 'vi-VN'
                      ? 'Khiếu nại này đã được chấp nhận và giải quyết hoàn tất.'
                      : 'This dispute has been accepted and fully resolved.'}
                  </span>
                </div>
              ) : (
                <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/20 border border-rose-200/60 dark:border-rose-900/40 text-rose-800 dark:text-rose-300 text-xs flex items-center gap-2">
                  <AlertCircle className="w-4 h-4 text-rose-600 shrink-0" />
                  <span>
                    {locale === 'vi-VN'
                      ? 'Khiếu nại này đã bị từ chối sau khi đối soát đối ứng. Bạn không thể sửa hoặc xóa hồ sơ này.'
                      : 'This dispute was rejected after review. You cannot edit or delete this record.'}
                  </span>
                </div>
              )}
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
