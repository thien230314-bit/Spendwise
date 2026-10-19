import React, { useState, useEffect, useCallback } from 'react';
import {
  Search,
  CheckCircle2,
  MessageSquare,
  Send,
  X,
  FileQuestion,
  ChevronRight,
  Filter,
  AlertCircle,
  RefreshCw,
  ShieldCheck,
  Inbox,
  AlertTriangle,
  KeyRound,
  Lock,
  User as UserIcon,
  Phone,
  Clock,
  HelpCircle,
  Check,
  Mail,
  Ban,
  ShieldAlert,
  Info,
} from 'lucide-react';
import { useAuth } from '../../providers/AuthProvider';
import { useI18n } from '../../i18n/I18nContext';
import { useSupportDisputes } from '../../services/supportDisputeService';
import { SupportCase, SupportCaseStatus, SupportCaseCategory } from '../../types';
import { Button } from '../../components/ui/Button';
import { Select } from '../../components/ui/Select';
import { EmptyState } from '../../components/ui/EmptyState';
import { AttachmentList } from '../../components/support/AttachmentList';
import {
  casesApiService,
  AdminAccountRecoveryDto,
} from '../../services/casesApiService';
import { useSecurityPinChallenge } from '../../providers/SecurityPinChallengeProvider';
import { getLocalizedErrorMessage } from '../../utils/localizationHelpers';

export const AdminSupportPage: React.FC = () => {
  const { currentUser } = useAuth();
  const { t, locale } = useI18n();
  const { challengePin, challengePinWithValue } = useSecurityPinChallenge();
  const isVi = locale === 'vi-VN';

  const {
    supportCases,
    isLoading,
    loadError,
    refresh,
    adminReplySupportCase,
    adminUpdateSupportStatus,
    grantTransactionCorrectionAllowance,
    loadAttachmentBlob,
  } = useSupportDisputes(currentUser);

  // Tab selector: Support Tickets vs Account Recovery (Flow B)
  const [activeTab, setActiveTab] = useState<'tickets' | 'recovery'>('tickets');

  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [selectedCase, setSelectedCase] = useState<SupportCase | null>(null);

  // Flow B: Account Recovery State
  const [recoveryRequests, setRecoveryRequests] = useState<AdminAccountRecoveryDto[]>([]);
  const [isRecoveryLoading, setIsRecoveryLoading] = useState(false);
  const [recoverySearchQuery, setRecoverySearchQuery] = useState('');
  const [recoveryStatusFilter, setRecoveryStatusFilter] = useState<string>('all');

  const refreshRecoveryRequests = useCallback(async () => {
    setIsRecoveryLoading(true);
    try {
      const res = await casesApiService.getAdminAccountRecoveries({
        status: recoveryStatusFilter === 'all' ? undefined : recoveryStatusFilter,
        search: recoverySearchQuery.trim() || undefined,
      });
      setRecoveryRequests(Array.isArray(res) ? res : []);
    } catch (e) {
      console.error('Failed to load admin account recovery requests', e);
    } finally {
      setIsRecoveryLoading(false);
    }
  }, [recoveryStatusFilter, recoverySearchQuery]);

  useEffect(() => {
    if (activeTab === 'recovery') {
      refreshRecoveryRequests();
    }
  }, [activeTab, refreshRecoveryRequests]);

  // Flow B Modals:
  // 1. Approve modal
  const [approveModalRequest, setApproveModalRequest] = useState<AdminAccountRecoveryDto | null>(null);
  const [approveReason, setApproveReason] = useState('');
  const [approveError, setApproveError] = useState<string | null>(null);
  const [approveIsSubmitting, setApproveIsSubmitting] = useState(false);

  // 1b. Temporary Password Result Modal
  const [tempPasswordResult, setTempPasswordResult] = useState<{
    userName: string;
    temporaryPassword: string;
  } | null>(null);
  const [isCopiedTempPassword, setIsCopiedTempPassword] = useState(false);

  // 2. Request more info modal
  const [moreInfoModalRequest, setMoreInfoModalRequest] = useState<AdminAccountRecoveryDto | null>(null);
  const [moreInfoNote, setMoreInfoNote] = useState('');
  const [moreInfoError, setMoreInfoError] = useState<string | null>(null);
  const [moreInfoIsSubmitting, setMoreInfoIsSubmitting] = useState(false);

  // 3. Reject modal
  const [rejectModalRequest, setRejectModalRequest] = useState<AdminAccountRecoveryDto | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [rejectError, setRejectError] = useState<string | null>(null);
  const [rejectIsSubmitting, setRejectIsSubmitting] = useState(false);

  // Flow B Handlers
  const handleInitiateApprove = (req: AdminAccountRecoveryDto) => {
    setApproveModalRequest(req);
    setApproveReason('');
    setApproveError(null);
  };

  const handleConfirmApprove = async () => {
    if (!approveModalRequest) return;
    const trimmedReason = approveReason.trim();
    if (!trimmedReason) {
      setApproveError(
        isVi
          ? 'Vui lòng nhập lý do phê duyệt khôi phục tài khoản (bắt buộc).'
          : 'Please enter a valid approval reason (mandatory).'
      );
      return;
    }

    if (!currentUser?.id) {
      setApproveError(
        isVi
          ? 'Không xác định được định danh Quản trị viên thực hiện.'
          : 'Authenticated admin ID is unavailable.'
      );
      return;
    }

    // User resolution: use resolvedUserId first (backend already resolved)
    let targetUserId = approveModalRequest.resolvedUserId;
    if (!targetUserId) {
      // Search by phone or emailHint — exact backend fields only
      const searchTerm = approveModalRequest.phone || approveModalRequest.emailHint;
      if (searchTerm) {
        try {
          const matchedUsers = await casesApiService.getAdminUsers(searchTerm);
          if (!matchedUsers || matchedUsers.length === 0) {
            setApproveError(
              isVi
                ? 'Không tìm thấy tài khoản nào khớp với thông tin khôi phục. Không thể phê duyệt.'
                : 'No user account found matching the recovery information. Cannot approve.'
            );
            return;
          }
          if (matchedUsers.length > 1) {
            // Multiple matches — do NOT guess; block and require admin to resolve manually
            setApproveError(
              isVi
                ? `Tìm thấy ${matchedUsers.length} tài khoản khớp với "${searchTerm}". Không thể xác định duy nhất. Vui lòng xác minh thủ công qua màn hình Quản lý người dùng.`
                : `Found ${matchedUsers.length} accounts matching "${searchTerm}". Cannot resolve unambiguously. Please verify manually via User Management.`
            );
            return;
          }
          // Exactly one match — safe to resolve
          targetUserId = matchedUsers[0].id;
        } catch (lookupErr) {
          console.warn('Failed to resolve target user by search:', lookupErr);
          setApproveError(
            isVi
              ? 'Tra cứu tài khoản người dùng thất bại. Vui lòng thử lại.'
              : 'User account lookup failed. Please try again.'
          );
          return;
        }
      }
    }

    if (!targetUserId) {
      setApproveError(
        isVi
          ? 'Không thể xác định User ID của tài khoản cần khôi phục. Vui lòng kiểm tra thông tin người dùng.'
          : 'Unable to resolve target user ID for recovery. Please verify user information.'
      );
      return;
    }

    const pin = await challengePinWithValue({
      actionName: isVi ? 'Phê duyệt khôi phục tài khoản' : 'Approve Account Recovery',
      title: isVi ? 'Xác thực mã PIN Quản trị viên' : 'Admin Security PIN Verification',
      description: isVi
        ? `Nhập mã PIN Quản trị viên 6 chữ số để phê duyệt khôi phục tài khoản và cấp mật khẩu tạm thời cho "${approveModalRequest.fullName || approveModalRequest.phone}".`
        : `Enter your 6-digit Admin Security PIN to approve account recovery and grant temporary password for "${approveModalRequest.fullName || approveModalRequest.phone}".`,
    });

    if (!pin) {
      return;
    }

    setApproveIsSubmitting(true);
    setApproveError(null);

    try {
      const res = await casesApiService.adminApproveAccountRecovery(approveModalRequest.id, {
        adminPin: pin,
        reason: trimmedReason,
        userId: targetUserId,
      });

      // Use res.temporaryPassword directly — it is a top-level field, NOT nested under recovery
      const tempPass = res.temporaryPassword;
      if (!tempPass) {
        throw new Error(isVi ? 'Máy chủ không trả về mật khẩu tạm thời sau khi phê duyệt.' : 'Server did not return a temporary password upon approval.');
      }

      setTempPasswordResult({
        userName: approveModalRequest.fullName || approveModalRequest.phone || 'User',
        temporaryPassword: tempPass,
      });
      showToast(
        isVi
          ? 'Đã phê duyệt khôi phục tài khoản!'
          : 'Account recovery approved!'
      );
      setApproveModalRequest(null);
      setApproveReason('');
      refreshRecoveryRequests();
    } catch (err: any) {
      setApproveError(
        getLocalizedErrorMessage(
          err,
          locale,
          t,
          isVi ? 'Có lỗi xảy ra khi phê duyệt khôi phục tài khoản.' : 'Failed to approve recovery request.'
        )
      );
    } finally {
      setApproveIsSubmitting(false);
    }
  };

  const handleInitiateMoreInfo = (req: AdminAccountRecoveryDto) => {
    setMoreInfoModalRequest(req);
    setMoreInfoNote('');
    setMoreInfoError(null);
  };

  const handleConfirmMoreInfo = async () => {
    if (!moreInfoModalRequest) return;
    const trimmedNote = moreInfoNote.trim();
    if (!trimmedNote) {
      setMoreInfoError(
        isVi
          ? 'Vui lòng nhập câu hỏi hoặc yêu cầu bổ sung thông tin (bắt buộc).'
          : 'Please enter the information required from user (mandatory).'
      );
      return;
    }

    const pin = await challengePinWithValue({
      actionName: isVi ? 'Yêu cầu bổ sung thông tin' : 'Request More Info',
      title: isVi ? 'Xác thực mã PIN Quản trị viên' : 'Admin Security PIN Verification',
      description: isVi
        ? `Nhập mã PIN Quản trị viên 6 chữ số để gửi yêu cầu bổ sung thông tin cho "${moreInfoModalRequest.fullName || moreInfoModalRequest.phone}".`
        : `Enter your 6-digit Admin Security PIN to request additional information for "${moreInfoModalRequest.fullName || moreInfoModalRequest.phone}".`,
    });

    if (!pin) {
      return;
    }

    setMoreInfoIsSubmitting(true);
    setMoreInfoError(null);

    try {
      await casesApiService.adminRequestMoreInfoAccountRecovery(moreInfoModalRequest.id, {
        adminPin: pin,
        reason: trimmedNote,
      });
      showToast(
        isVi
          ? 'Đã gửi yêu cầu bổ sung thông tin tới người dùng.'
          : 'Requested additional information from user.'
      );
      setMoreInfoModalRequest(null);
      setMoreInfoNote('');
      refreshRecoveryRequests();
    } catch (err: any) {
      setMoreInfoError(
        getLocalizedErrorMessage(
          err,
          locale,
          t,
          isVi ? 'Có lỗi xảy ra khi gửi yêu cầu.' : 'Failed to request additional info.'
        )
      );
    } finally {
      setMoreInfoIsSubmitting(false);
    }
  };

  const handleInitiateReject = (req: AdminAccountRecoveryDto) => {
    setRejectModalRequest(req);
    setRejectReason('');
    setRejectError(null);
  };

  const handleConfirmReject = async () => {
    if (!rejectModalRequest) return;
    const trimmedReason = rejectReason.trim();
    if (!trimmedReason) {
      setRejectError(
        isVi
          ? 'Vui lòng nhập lý do từ chối yêu cầu khôi phục (bắt buộc).'
          : 'Please enter the rejection reason (mandatory).'
      );
      return;
    }

    const pin = await challengePinWithValue({
      actionName: isVi ? 'Từ chối khôi phục tài khoản' : 'Reject Account Recovery',
      title: isVi ? 'Xác thực mã PIN Quản trị viên' : 'Admin Security PIN Verification',
      description: isVi
        ? `Nhập mã PIN Quản trị viên 6 chữ số để từ chối yêu cầu khôi phục tài khoản cho "${rejectModalRequest.fullName || rejectModalRequest.phone}".`
        : `Enter your 6-digit Admin Security PIN to reject account recovery for "${rejectModalRequest.fullName || rejectModalRequest.phone}".`,
    });

    if (!pin) {
      return;
    }

    setRejectIsSubmitting(true);
    setRejectError(null);

    try {
      await casesApiService.adminRejectAccountRecovery(rejectModalRequest.id, {
        adminPin: pin,
        reason: trimmedReason,
      });
      showToast(
        isVi
          ? 'Đã từ chối yêu cầu khôi phục tài khoản.'
          : 'Account recovery request rejected.'
      );
      setRejectModalRequest(null);
      setRejectReason('');
      refreshRecoveryRequests();
    } catch (err: any) {
      setRejectError(
        getLocalizedErrorMessage(
          err,
          locale,
          t,
          isVi ? 'Có lỗi xảy ra khi từ chối yêu cầu.' : 'Failed to reject recovery request.'
        )
      );
    } finally {
      setRejectIsSubmitting(false);
    }
  };

  const getTargetUserDisplay = (req: AdminAccountRecoveryDto) => {
    return {
      name: req.fullName || (isVi ? 'Người dùng' : 'User'),
      phone: req.phone || '',
      email: req.emailHint || '',
    };
  };

  // Filter recovery requests
  const filteredRecoveryRequests = recoveryRequests.filter((req) => {
    const q = recoverySearchQuery.trim().toLowerCase();
    const fullName = req.fullName || '';
    const phone = req.phone || '';
    const email = req.emailHint || '';
    const desc = req.description || '';
    const adminNote = req.adminNote || '';

    const matchesSearch =
      !q ||
      fullName.toLowerCase().includes(q) ||
      phone.toLowerCase().includes(q) ||
      email.toLowerCase().includes(q) ||
      desc.toLowerCase().includes(q) ||
      adminNote.toLowerCase().includes(q);

    const matchesStatus =
      recoveryStatusFilter === 'all' ||
      (req.status || '').toLowerCase() === recoveryStatusFilter.toLowerCase();

    return matchesSearch && matchesStatus;
  });

  const getRecoveryStatusBadge = (status: string) => {
    const s = (status || '').toLowerCase();
    switch (s) {
      case 'pending':
        return {
          label: isVi ? 'Chờ xem xét' : 'Pending',
          className: 'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 border border-amber-200/60 dark:border-amber-800/40',
        };
      case 'moreinfoneeded': // exact backend value lowercased
        return {
          label: isVi ? 'Cần thêm thông tin' : 'More Info Needed',
          className: 'bg-orange-50 text-orange-700 dark:bg-orange-950/40 dark:text-orange-400 border border-orange-200/60 dark:border-orange-800/40',
        };
      case 'approved':
        return {
          label: isVi ? 'Đã phê duyệt' : 'Approved',
          className: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40',
        };
      case 'rejected':
        return {
          label: isVi ? 'Đã từ chối' : 'Rejected',
          className: 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60 dark:border-rose-800/40',
        };
      default:
        return {
          label: status,
          className: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300 border border-slate-200 dark:border-slate-700',
        };
    }
  };

  // Keep selectedCase fresh when store updates
  useEffect(() => {
    if (selectedCase) {
      const fresh = supportCases.find((c) => c.id === selectedCase.id);
      if (fresh) setSelectedCase(fresh);
    }
  }, [supportCases]);

  // Admin reply form states
  const [adminMessage, setAdminMessage] = useState('');
  const [targetStatus, setTargetStatus] = useState<SupportCaseStatus | ''>('');
  const [actionSubmitting, setActionSubmitting] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3500);
  };

  // Filter cases
  const filteredCases = supportCases.filter((c) => {
    const matchesSearch =
      (c.ticketNumber || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
      (c.id || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
      (c.title || c.subject || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
      (c.userName || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
      (c.userEmail || '').toLowerCase().includes(searchQuery.toLowerCase());

    const matchesStatus =
      statusFilter === 'all' ||
      (statusFilter === 'open_group' && (c.status === 'pending' || c.status === 'open' || c.status === 'in_progress')) ||
      c.status === statusFilter;

    return matchesSearch && matchesStatus;
  });

  const getCategoryLabel = (cat: SupportCaseCategory) => {
    if (locale === 'vi-VN') {
      switch (cat) {
        case 'system_error':
          return 'Lỗi hệ thống';
        case 'account':
          return 'Tài khoản';
        case 'banking_sepay':
          return 'Ngân hàng / SePay';
        case 'transaction_edit':
          return 'Chỉnh sửa GD';
        case 'other':
        case 'general':
        default:
          return 'Khác';
      }
    } else {
      switch (cat) {
        case 'system_error':
          return 'System Error';
        case 'account':
          return 'Account';
        case 'banking_sepay':
          return 'Banking / SePay';
        case 'transaction_edit':
          return 'Transaction Edit';
        case 'other':
        case 'general':
        default:
          return 'Other';
      }
    }
  };

  const getStatusBadge = (status: string) => {
    const isVi = locale === 'vi-VN';
    switch (status) {
      case 'pending':
      case 'open':
        return {
          label: isVi ? 'Chờ tiếp nhận' : 'Pending',
          className: 'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 border border-amber-200/60 dark:border-amber-800/40',
        };
      case 'in_progress':
        return {
          label: isVi ? 'Đang xử lý' : 'In Progress',
          className: 'bg-sky-50 text-sky-700 dark:bg-sky-950/40 dark:text-sky-400 border border-sky-200/60 dark:border-sky-800/40',
        };
      case 'need_more_info':
        return {
          label: isVi ? 'Cần thêm thông tin' : 'Need More Info',
          className: 'bg-orange-50 text-orange-700 dark:bg-orange-950/40 dark:text-orange-400 border border-orange-200/60 dark:border-orange-800/40',
        };
      case 'resolved':
        return {
          label: isVi ? 'Đã giải quyết' : 'Resolved',
          className: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40',
        };
      case 'rejected':
        return {
          label: isVi ? 'Từ chối' : 'Rejected',
          className: 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60 dark:border-rose-800/40',
        };
      case 'closed':
      default:
        return {
          label: isVi ? 'Đã đóng' : 'Closed',
          className: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300 border border-slate-200 dark:border-slate-700',
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

  // Submit Admin Action (Reply, Status Change, Request Info)
  const handleAdminAction = async (forcedStatus?: SupportCaseStatus) => {
    if (!selectedCase) return;
    const finalNewStatus = forcedStatus || (targetStatus ? (targetStatus as SupportCaseStatus) : undefined);
    const trimmedMessage = adminMessage.trim();

    if (finalNewStatus === 'rejected') {
      if (trimmedMessage.length < 5 || trimmedMessage.length > 2000) {
        setActionError(
          locale === 'vi-VN'
            ? 'Lý do từ chối phải có độ dài từ 5 đến 2000 ký tự.'
            : 'Rejection reason must be between 5 and 2000 characters.'
        );
        return;
      }
    }

    if (finalNewStatus === 'resolved') {
      if (trimmedMessage.length < 5 || trimmedMessage.length > 2000) {
        setActionError(
          locale === 'vi-VN'
            ? 'Nội dung giải quyết phải có độ dài từ 5 đến 2000 ký tự.'
            : 'Resolution message must be between 5 and 2000 characters.'
        );
        return;
      }
    }

    if (!trimmedMessage && !finalNewStatus) {
      setActionError(
        locale === 'vi-VN'
          ? 'Vui lòng nhập nội dung phản hồi hoặc chọn trạng thái cập nhật.'
          : 'Please enter a reply message or select an updated status.'
      );
      return;
    }

    setActionSubmitting(true);
    setActionError(null);

    try {
      let res: { success: boolean; case?: SupportCase; error?: string };

      if (finalNewStatus) {
        // Changing case status via POST /admin/cases/support/{id}/status
        res = await adminUpdateSupportStatus({
          caseId: selectedCase.id,
          status: finalNewStatus,
          message: trimmedMessage || (
            finalNewStatus === 'need_more_info'
              ? 'Yêu cầu người dùng bổ sung thông tin'
              : 'Cập nhật trạng thái'
          ),
        });
      } else {
        // Sending admin reply via POST /admin/cases/support/{id}/messages
        res = await adminReplySupportCase({
          caseId: selectedCase.id,
          message: trimmedMessage,
        });
      }

      if (res.success) {
        showToast(
          locale === 'vi-VN'
            ? finalNewStatus === 'need_more_info'
              ? 'Đã gửi yêu cầu người dùng bổ sung thông tin!'
              : finalNewStatus === 'resolved'
              ? 'Đã hoàn tất hỗ trợ và cập nhật Đã giải quyết!'
              : finalNewStatus === 'rejected'
              ? 'Đã từ chối yêu cầu hỗ trợ!'
              : finalNewStatus === 'closed'
              ? 'Đã đóng vé hỗ trợ thành công!'
              : 'Đã gửi phản hồi hỗ trợ thành công!'
            : finalNewStatus === 'need_more_info'
            ? 'Requested additional information from user!'
            : finalNewStatus === 'resolved'
            ? 'Support case resolved successfully!'
            : finalNewStatus === 'rejected'
            ? 'Support case rejected!'
            : finalNewStatus === 'closed'
            ? 'Support ticket closed successfully!'
            : 'Reply sent successfully!'
        );
        setAdminMessage('');
        setTargetStatus('');
        // Update local selectedCase directly from res.case
        if (res.case) {
          setSelectedCase(res.case);
        }
      } else {
        const errorMsg = getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          isVi ? 'Có lỗi xảy ra.' : 'Unable to update support case. Please try again.'
        );
        setActionError(errorMsg);
      }
    } finally {
      setActionSubmitting(false);
    }
  };

  const handleGrantCorrectionAllowance = async () => {
    if (!selectedCase) return;
    if (!adminMessage.trim()) {
      setActionError(
        locale === 'vi-VN'
          ? 'Vui lòng nhập lý do cấp thêm lượt sửa vào ô phản hồi bên dưới.'
          : 'Please enter the reason for granting an additional edit in the reply box below.'
      );
      return;
    }

    const adminPin = await challengePinWithValue({
      title: isVi ? 'Xác thực mã PIN Quản trị viên' : 'Admin Security PIN Verification',
      description: isVi
        ? `Nhập mã PIN bảo mật để phê duyệt cấp thêm 1 lượt chỉnh sửa cho giao dịch #${selectedCase.linkedTransactionRef || selectedCase.linkedTransactionId}.`
        : `Enter your security PIN to approve 1 additional edit allowance for transaction #${selectedCase.linkedTransactionRef || selectedCase.linkedTransactionId}.`,
      actionName: 'GRANT_CORRECTION_ALLOWANCE',
    });

    if (!adminPin) {
      // Admin cancelled PIN prompt
      return;
    }

    setActionSubmitting(true);
    setActionError(null);

    try {
      const res = await grantTransactionCorrectionAllowance({
        caseId: selectedCase.id,
        adminPin,
        reason: adminMessage.trim(),
      });

      if (res.success) {
        showToast(
          locale === 'vi-VN'
            ? 'Đã cấp thêm 1 lượt chỉnh sửa cho giao dịch thành công!'
            : 'Successfully granted 1 additional edit allowance for the transaction!'
        );
        setAdminMessage('');
        if (res.case) {
          setSelectedCase(res.case);
        }
      } else {
        const errorMsg = getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          isVi ? 'Có lỗi khi cấp lượt sửa.' : 'Unable to grant edit allowance. Please try again.'
        );
        setActionError(errorMsg);
      }
    } finally {
      setActionSubmitting(false);
    }
  };

  return (
    <div id="admin-support-page" className="space-y-6 pb-12">
      {/* Toast */}
      {toastMessage && (
        <div className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-xl bg-slate-900 text-white shadow-xl border border-emerald-500/50 text-xs font-semibold animate-in fade-in slide-in-from-bottom-2">
          <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            {t('adminSupport.title')}
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
            {t('adminSupport.subtitle')}
          </p>
        </div>

        <div className="flex items-center gap-2">
          <span className="px-3 py-1.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-700 dark:text-slate-300 text-xs font-medium">
            {activeTab === 'tickets'
              ? (isVi ? `Tổng số vé: ${supportCases.length}` : `Total Tickets: ${supportCases.length}`)
              : (isVi ? `Tổng yêu cầu: ${recoveryRequests.length}` : `Total Requests: ${recoveryRequests.length}`)}
          </span>
        </div>
      </div>

      {/* Navigation Tabs */}
      <div className="flex items-center gap-6 border-b border-slate-200 dark:border-slate-800">
        <button
          type="button"
          id="tab-support-tickets"
          onClick={() => setActiveTab('tickets')}
          className={`pb-3 px-1 text-xs sm:text-sm font-semibold border-b-2 transition-colors cursor-pointer flex items-center gap-2 ${
            activeTab === 'tickets'
              ? 'border-emerald-600 text-emerald-600 dark:text-emerald-400'
              : 'border-transparent text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200'
          }`}
        >
          <MessageSquare className="w-4 h-4" />
          <span>{isVi ? 'Yêu cầu hỗ trợ' : 'Support Tickets'}</span>
          <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
            {supportCases.length}
          </span>
        </button>

        <button
          type="button"
          id="tab-account-recovery"
          onClick={() => {
            setActiveTab('recovery');
            refreshRecoveryRequests();
          }}
          className={`pb-3 px-1 text-xs sm:text-sm font-semibold border-b-2 transition-colors cursor-pointer flex items-center gap-2 ${
            activeTab === 'recovery'
              ? 'border-emerald-600 text-emerald-600 dark:text-emerald-400'
              : 'border-transparent text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200'
          }`}
        >
          <KeyRound className="w-4 h-4" />
          <span>{isVi ? 'Khôi phục tài khoản' : 'Account Recovery'}</span>
          {recoveryRequests.filter((r) => (r.status || '').toLowerCase() === 'pending').length > 0 ? (
            <span className="px-2 py-0.5 rounded-full text-xs font-bold bg-amber-100 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300">
              {recoveryRequests.filter((r) => (r.status || '').toLowerCase() === 'pending').length}
            </span>
          ) : (
            <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
              {recoveryRequests.length}
            </span>
          )}
        </button>
      </div>

      {activeTab === 'tickets' ? (
        <>

      {/* Filters & Search */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        <div className="relative flex-1 max-w-md">
          <input
            type="text"
            placeholder={isVi ? "Tìm theo mã vé, tiêu đề, email, tên..." : "Search by ticket #, title, email, name..."}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full h-9 pl-9 pr-3 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-800 dark:text-slate-200 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
          />
          <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
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
            <option value="open_group">{isVi ? 'Cần xử lý' : 'Requires Action'}</option>
            <option value="pending">{isVi ? 'Chờ tiếp nhận' : 'Pending'}</option>
            <option value="in_progress">{isVi ? 'Đang xử lý' : 'In Progress'}</option>
            <option value="need_more_info">{isVi ? 'Cần thêm thông tin' : 'Need More Info'}</option>
            <option value="resolved">{isVi ? 'Đã giải quyết' : 'Resolved'}</option>
            <option value="rejected">{isVi ? 'Từ chối' : 'Rejected'}</option>
            <option value="closed">{isVi ? 'Đã đóng' : 'Closed'}</option>
          </Select>
        </div>
      </div>

      {/* Cases List */}
      <div className="space-y-3">
        {isLoading ? (
          <div className="flex items-center justify-center py-12 gap-2 text-slate-500 text-xs">
            <RefreshCw className="h-4 w-4 animate-spin text-emerald-600" />
            <span>{isVi ? 'Đang tải danh sách vé hỗ trợ...' : 'Loading support tickets...'}</span>
          </div>
        ) : loadError ? (
          <div className="p-8 text-center bg-rose-50/50 dark:bg-rose-950/20 rounded-2xl border border-rose-200 dark:border-rose-900/50 space-y-3">
            <div className="w-10 h-10 mx-auto rounded-xl bg-rose-100 dark:bg-rose-900/40 text-rose-600 dark:text-rose-400 flex items-center justify-center">
              <AlertCircle className="w-5 h-5" />
            </div>
            <h3 className="text-sm font-bold text-rose-900 dark:text-rose-200">
              {isVi ? 'Không thể tải danh sách vé hỗ trợ' : 'Failed to load support tickets'}
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
              <span>{isVi ? 'Thử lại' : 'Retry'}</span>
            </Button>
          </div>
        ) : filteredCases.length === 0 ? (
          <EmptyState
            icon={Inbox}
            title={isVi ? 'Chưa có yêu cầu hỗ trợ nào' : 'No support tickets yet'}
            description={
              isVi
                ? 'Khi người dùng gửi yêu cầu hỗ trợ, yêu cầu sẽ xuất hiện tại đây để quản trị viên xử lý.'
                : 'When users submit support requests, they will appear here for administrators to handle.'
            }
          />
        ) : (
          filteredCases.map((tk) => {
            const badge = getStatusBadge(tk.status);

            return (
              <div
                key={tk.id}
                onClick={() => setSelectedCase(tk)}
                className="p-4 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs hover:bg-slate-50/50 dark:hover:bg-slate-800/40 transition cursor-pointer space-y-2.5"
              >
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-xs font-semibold text-slate-800 dark:text-slate-200">
                      {tk.ticketNumber || tk.id}
                    </span>
                    <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                      {getCategoryLabel(tk.category)}
                    </span>
                    <span className={`px-2.5 py-0.5 rounded-full text-xs font-medium border ${badge.className}`}>
                      {badge.label}
                    </span>
                    {tk.attachments && tk.attachments.length > 0 && (
                      <span className="text-xs text-slate-400">
                        • {tk.attachments.length} {isVi ? 'tệp đính kèm' : 'attachments'}
                      </span>
                    )}
                  </div>

                  <span className="text-xs text-slate-400 font-mono">
                    {formatDate(tk.createdAt)}
                  </span>
                </div>

                <div className="space-y-0.5">
                  <h4 className="font-semibold text-sm text-slate-900 dark:text-white">
                    {tk.title || tk.subject}
                  </h4>
                  <p className="text-xs text-slate-500 dark:text-slate-400 line-clamp-2">
                    {tk.description || tk.message}
                  </p>
                </div>

                <div className="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-slate-100 dark:border-slate-800 text-xs text-slate-500">
                  <div className="flex items-center gap-3">
                    <span>
                      {isVi ? 'Người gửi: ' : 'Sender: '}
                      <strong className="text-slate-700 dark:text-slate-300 font-mono">
                        {tk.userName ? `${tk.userName} (${tk.userId})` : `User ID: ${tk.userId}`}
                      </strong>
                    </span>
                  </div>

                  <div className="flex items-center gap-1 text-slate-600 dark:text-slate-300 font-medium text-xs">
                    <span>{isVi ? 'Xem chi tiết & Xử lý' : 'View Details & Process'}</span>
                    <ChevronRight className="w-3.5 h-3.5" />
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* DETAIL & PROCESSING MODAL */}
      {selectedCase && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-3xl bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-200 dark:border-slate-800 shadow-xl space-y-5 max-h-[90vh] overflow-y-auto">
            {/* Header */}
            <div className="flex items-start justify-between gap-3 pb-3 border-b border-slate-100 dark:border-slate-800">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <span className="font-mono text-xs font-semibold text-slate-500">
                    {selectedCase.ticketNumber || selectedCase.id}
                  </span>
                  <span className={`px-2.5 py-0.5 rounded-full text-xs font-medium border ${getStatusBadge(selectedCase.status).className}`}>
                    {getStatusBadge(selectedCase.status).label}
                  </span>
                  <span className="text-xs text-slate-400">
                    • {getCategoryLabel(selectedCase.category)}
                  </span>
                </div>
                <h2 className="text-base font-bold text-slate-900 dark:text-white">
                  {selectedCase.title || selectedCase.subject}
                </h2>
                <div className="flex flex-wrap items-center gap-3 text-xs text-slate-500 dark:text-slate-400 font-mono mt-1">
                  <span>{isVi ? 'Người dùng (ID):' : 'User (ID):'} <strong>{selectedCase.userName ? `${selectedCase.userName} (${selectedCase.userId})` : selectedCase.userId}</strong></span>
                  <span>{isVi ? 'Ngày tạo: ' : 'Created: '}{formatDate(selectedCase.createdAt)}</span>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setSelectedCase(null)}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Content & History */}
            <div className="space-y-4 text-xs">
              {/* Linked Transaction */}
              {selectedCase.linkedTransactionRef && (
                <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200/60 dark:border-slate-800 flex items-center justify-between">
                  <span className="text-slate-500 font-semibold">
                    {isVi ? 'Giao dịch liên quan:' : 'Linked Transaction:'}
                  </span>
                  <span className="font-mono font-bold text-slate-800 dark:text-slate-200">
                    #{selectedCase.linkedTransactionRef}
                  </span>
                </div>
              )}

              {/* History Timeline */}
              <div className="space-y-3">
                <div className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {isVi
                    ? `Lịch sử trao đổi & Cập nhật trạng thái (${selectedCase.history?.length || 1})`
                    : `Conversation History & Status Updates (${selectedCase.history?.length || 1})`}
                </div>

                <div className="space-y-3">
                  {(selectedCase.history || []).map((h, idx) => {
                    const isAdmin = h.actorRole === 'SystemAdmin';
                    return (
                      <div
                        key={h.id || idx}
                        className={`p-3.5 rounded-xl border space-y-2 ${
                          isAdmin
                            ? 'bg-blue-50/50 dark:bg-blue-950/20 border-blue-100 dark:border-blue-900/40 text-blue-950 dark:text-blue-200'
                            : 'bg-slate-50 dark:bg-slate-800/40 border-slate-200/60 dark:border-slate-800 text-slate-900 dark:text-slate-200'
                        }`}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <div className="flex items-center gap-2">
                            <span
                              className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                                isAdmin
                                  ? 'bg-blue-600 text-white'
                                  : 'bg-emerald-600 text-white'
                              }`}
                            >
                              {isAdmin
                                ? (isVi ? 'Quản trị viên' : 'Administrator')
                                : (isVi ? 'Người dùng' : 'User')}
                            </span>
                            <span className="font-semibold text-xs">{h.actorName}</span>
                            {h.newStatus && (
                              <span className="text-xs font-mono text-slate-400">
                                ({isVi ? 'Trạng thái: ' : 'Status: '}{getStatusBadge(h.newStatus).label})
                              </span>
                            )}
                          </div>
                          <span className="text-xs text-slate-400 font-mono">
                            {formatDate(h.timestamp)}
                          </span>
                        </div>

                        <p className="text-xs leading-relaxed whitespace-pre-wrap">
                          {h.message}
                        </p>

                        {/* Attachments in history */}
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

              {/* SPECIAL BLOCK FOR TRANSACTION_EDIT CASES */}
              {selectedCase.category === 'transaction_edit' && (
                selectedCase.correctionGranted ? (
                  <div className="p-4 rounded-2xl bg-emerald-50/80 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800/60 space-y-2">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <CheckCircle2 className="w-5 h-5 text-emerald-600 dark:text-emerald-400" />
                        <span className="font-bold text-xs text-emerald-900 dark:text-emerald-200">
                          {isVi
                            ? `Đã cấp thêm 1 lượt sửa cho giao dịch #${selectedCase.linkedTransactionRef || selectedCase.linkedTransactionId}`
                            : `Granted 1 additional edit for transaction #${selectedCase.linkedTransactionRef || selectedCase.linkedTransactionId}`}
                        </span>
                      </div>
                      <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 dark:bg-emerald-900/60 text-emerald-800 dark:text-emerald-300">
                        {isVi ? 'Đã giải quyết' : 'Resolved'}
                      </span>
                    </div>
                    <div className="text-[11px] text-emerald-800/90 dark:text-emerald-300/80 space-y-0.5 pl-7">
                      <p>
                        {isVi ? 'Trạng thái nghiệp vụ: ' : 'Ledger Status: '}
                        <span className="font-semibold">{isVi ? 'Đã xác nhận cấp thêm 1 lượt sửa trên hệ thống' : 'Confirmed 1 additional correction granted on ledger'}</span>
                      </p>
                      {selectedCase.correctionGrantedAt && (
                        <p>
                          {isVi ? 'Thời gian cấp: ' : 'Granted At: '}
                          {formatDate(selectedCase.correctionGrantedAt)}
                        </p>
                      )}
                      {selectedCase.correctionGrantReason && (
                        <p>
                          {isVi ? 'Lý do: ' : 'Reason: '}
                          <span className="italic font-medium">"{selectedCase.correctionGrantReason}"</span>
                        </p>
                      )}
                      <p className="text-[10px] text-emerald-600 dark:text-emerald-400 font-semibold pt-1">
                        {isVi
                          ? 'Mỗi yêu cầu chỉ có thể cấp thêm 1 lần duy nhất để bảo đảm an toàn sổ cái tài chính.'
                          : 'Each request may only be granted once to maintain financial ledger integrity.'}
                      </p>
                    </div>
                  </div>
                ) : (
                  <div className="p-4 rounded-2xl bg-amber-50/80 dark:bg-amber-950/25 border border-amber-200 dark:border-amber-800/60 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <ShieldCheck className="w-4 h-4 text-amber-600 dark:text-amber-400" />
                        <span className="font-bold text-xs text-amber-900 dark:text-amber-200">
                          {isVi
                            ? `Yêu cầu cấp thêm lượt sửa giao dịch #${selectedCase.linkedTransactionRef || selectedCase.linkedTransactionId}`
                            : `Request additional edit allowance for transaction #${selectedCase.linkedTransactionRef || selectedCase.linkedTransactionId}`}
                        </span>
                      </div>
                      <p className="text-[11px] text-amber-700 dark:text-amber-300/80">
                        {isVi
                          ? 'Nhập lý do phê duyệt vào ô phản hồi bên dưới, sau đó nhấn nút để cấp thêm đúng 1 lượt sửa và tự động hoàn tất vé.'
                          : 'Enter approval rationale in the reply field below, then click to grant exactly 1 edit and automatically resolve the ticket.'}
                      </p>
                    </div>
                    <Button
                      size="sm"
                      onClick={handleGrantCorrectionAllowance}
                      disabled={actionSubmitting}
                      className="bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold gap-1.5 shrink-0 cursor-pointer shadow-xs"
                    >
                      {actionSubmitting ? (
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                      ) : (
                        <ShieldCheck className="w-3.5 h-3.5" />
                      )}
                      <span>{isVi ? 'Cấp thêm 1 lượt sửa' : 'Grant 1 Edit Allowance'}</span>
                    </Button>
                  </div>
                )
              )}

              {/* ADMIN ACTION PANEL */}
              <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-900/80 border border-slate-200 dark:border-slate-800 space-y-3">
                <div className="flex items-center justify-between">
                  <span className="font-bold text-xs text-slate-800 dark:text-slate-200 flex items-center gap-1.5">
                    <MessageSquare className="w-4 h-4 text-blue-600" />
                    {isVi ? 'Phản hồi & Cập nhật trạng thái' : 'Reply & Status Update'}
                  </span>
                  <span className="text-[11px] text-slate-400 font-mono">
                    {isVi ? 'Người thực hiện:' : 'Actor:'} {currentUser?.name || 'SystemAdmin'}
                  </span>
                </div>

                {actionError && (
                  <div className="p-2.5 rounded-xl bg-rose-50 text-rose-700 text-xs font-semibold flex items-center gap-2">
                    <AlertCircle className="w-4 h-4 shrink-0" />
                    <span>{actionError}</span>
                  </div>
                )}

                <textarea
                  rows={3}
                  placeholder={
                    isVi
                      ? "Nhập nội dung phản hồi cho người dùng, giải thích lý do cập nhật hoặc câu hỏi yêu cầu bổ sung thông tin..."
                      : "Enter reply to user, status update rationale, or request for additional information..."
                  }
                  value={adminMessage}
                  onChange={(e) => setAdminMessage(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-normal resize-none text-xs focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
                />

                <div className="flex flex-wrap items-center justify-between gap-3 pt-1">
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-slate-500 font-medium">
                      {isVi ? 'Chuyển trạng thái:' : 'Update Status:'}
                    </span>
                    <Select
                      value={targetStatus}
                      onChange={(val) => setTargetStatus(val as SupportCaseStatus)}
                      className="text-xs py-1"
                    >
                      <option value="">{isVi ? '-- Giữ nguyên --' : '-- Keep Current --'}</option>
                      <option value="in_progress">{isVi ? 'Đang xử lý' : 'In Progress'}</option>
                      <option value="need_more_info">{isVi ? 'Yêu cầu bổ sung thông tin' : 'Need More Info'}</option>
                      <option value="resolved">{isVi ? 'Đã giải quyết' : 'Resolved'}</option>
                      <option value="rejected">{isVi ? 'Từ chối' : 'Rejected'}</option>
                      <option value="closed">{isVi ? 'Đóng vé' : 'Closed'}</option>
                    </Select>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => handleAdminAction('need_more_info')}
                      disabled={actionSubmitting}
                      className="text-orange-700 dark:text-orange-300 border-orange-200 dark:border-orange-800 hover:bg-orange-50 dark:hover:bg-orange-950/40 text-xs font-bold cursor-pointer"
                    >
                      <FileQuestion className="w-3.5 h-3.5 mr-1" />
                      {isVi ? 'Yêu cầu thêm thông tin' : 'Request More Info'}
                    </Button>

                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => handleAdminAction('rejected')}
                      disabled={actionSubmitting}
                      className="text-rose-700 dark:text-rose-300 border-rose-200 dark:border-rose-800 hover:bg-rose-50 dark:hover:bg-rose-950/40 text-xs font-bold cursor-pointer"
                    >
                      <Ban className="w-3.5 h-3.5 mr-1" />
                      {isVi ? 'Từ chối' : 'Reject'}
                    </Button>

                    <Button
                      size="sm"
                      onClick={() => handleAdminAction()}
                      disabled={actionSubmitting}
                      className="bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold gap-1 cursor-pointer"
                    >
                      {actionSubmitting ? (
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                      ) : (
                        <Send className="w-3.5 h-3.5" />
                      )}
                      <span>{isVi ? 'Gửi phản hồi' : 'Send Reply'}</span>
                    </Button>

                    <Button
                      size="sm"
                      onClick={() => handleAdminAction('resolved')}
                      disabled={actionSubmitting}
                      className="bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold gap-1 cursor-pointer"
                    >
                      <CheckCircle2 className="w-3.5 h-3.5" />
                      <span>{isVi ? 'Hoàn tất & Giải quyết' : 'Resolve Ticket'}</span>
                    </Button>
                  </div>
                </div>
              </div>
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
        </>
      ) : (
        /* RECOVERY TAB CONTENT */
        <>
          {/* Recovery Filters & Search */}
          <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
            <div className="relative flex-1 max-w-md">
              <input
                type="text"
                placeholder={isVi ? "Tìm theo họ tên, số điện thoại, email..." : "Search by name, phone, email..."}
                value={recoverySearchQuery}
                onChange={(e) => setRecoverySearchQuery(e.target.value)}
                className="w-full h-9 pl-9 pr-3 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-800 dark:text-slate-200 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
              />
              <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            </div>

            <div className="flex items-center gap-2">
              <Select
                value={recoveryStatusFilter}
                onChange={(val) => setRecoveryStatusFilter(val)}
                size="sm"
                variant="compact"
                className="w-48"
              >
                <option value="all">{isVi ? 'Tất cả trạng thái' : 'All Statuses'}</option>
                <option value="Pending">{isVi ? 'Chờ xem xét' : 'Pending'}</option>
                <option value="MoreInfoNeeded">{isVi ? 'Cần thêm thông tin' : 'Need More Info'}</option>
                <option value="Approved">{isVi ? 'Đã phê duyệt' : 'Approved'}</option>
                <option value="Rejected">{isVi ? 'Đã từ chối' : 'Rejected'}</option>
              </Select>

              <Button
                variant="outline"
                size="sm"
                onClick={refreshRecoveryRequests}
                className="cursor-pointer gap-1.5 text-xs"
                title={isVi ? "Làm mới danh sách" : "Refresh list"}
              >
                <RefreshCw className="w-3.5 h-3.5" />
                <span className="hidden sm:inline">{isVi ? 'Làm mới' : 'Refresh'}</span>
              </Button>
            </div>
          </div>

          {/* Recovery List */}
          <div className="space-y-4">
            {filteredRecoveryRequests.length === 0 ? (
              <EmptyState
                icon={KeyRound}
                title={isVi ? 'Không có yêu cầu khôi phục tài khoản nào' : 'No account recovery requests'}
                description={
                  isVi
                    ? 'Khi người dùng gặp sự cố quên mật khẩu và mất quyền truy cập email, yêu cầu gửi qua số điện thoại sẽ xuất hiện tại đây để Quản trị viên thẩm định và cấp lại quyền truy cập.'
                    : 'When users lose access to email and request recovery, requests will appear here for administrator verification.'
                }
              />
            ) : (
              filteredRecoveryRequests.map((req) => {
                const badge = getRecoveryStatusBadge(req.status);
                const targetUser = getTargetUserDisplay(req);
                const statusLower = (req.status || '').toLowerCase();
                const isTerminal = statusLower === 'approved' || statusLower === 'rejected';

                return (
                  <div
                    key={req.id}
                    className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs space-y-4"
                  >
                    {/* Top Row: User identity & Status Badge */}
                    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-3 border-b border-slate-100 dark:border-slate-800">
                      <div className="space-y-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-bold text-sm text-slate-900 dark:text-white flex items-center gap-1.5">
                            <UserIcon className="w-4 h-4 text-slate-400" />
                            {targetUser.name}
                          </span>
                          <span className="font-mono text-xs font-semibold text-slate-500 dark:text-slate-400">
                            #{req.id}
                          </span>
                          <span className={`px-2.5 py-0.5 rounded-full text-xs font-semibold ${badge.className}`}>
                            {badge.label}
                          </span>
                          {req.resolvedUserId && (
                            <span className="px-2 py-0.5 rounded-md text-[11px] font-mono font-medium bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300 border border-blue-200/50">
                              UID: {req.resolvedUserId}
                            </span>
                          )}
                        </div>

                        {/* Contact details */}
                        <div className="flex flex-wrap items-center gap-4 text-xs text-slate-500 dark:text-slate-400">
                          <span className="flex items-center gap-1">
                            <Phone className="w-3.5 h-3.5 text-slate-400" />
                            <span>{isVi ? 'Số điện thoại: ' : 'Phone: '}</span>
                            <strong className="text-slate-700 dark:text-slate-200 font-mono">{targetUser.phone || '—'}</strong>
                          </span>
                          <span className="flex items-center gap-1">
                            <Mail className="w-3.5 h-3.5 text-slate-400" />
                            <span>{isVi ? 'Email: ' : 'Email: '}</span>
                            <strong className="text-slate-700 dark:text-slate-200">
                              {targetUser.email || (isVi ? 'Chưa cung cấp' : 'Not provided')}
                            </strong>
                          </span>
                        </div>
                      </div>

                      <div className="text-right text-xs text-slate-400 font-mono">
                        <div className="flex items-center sm:justify-end gap-1">
                          <Clock className="w-3.5 h-3.5 text-slate-400" />
                          <span>{formatDate(new Date(req.createdAt).toISOString())}</span>
                        </div>
                      </div>
                    </div>

                    {/* User Provided Description / Notes */}
                    <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-100 dark:border-slate-800/80 space-y-1">
                      <div className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                        {isVi ? 'Nội dung và lý do người dùng gửi' : 'User message / notes'}
                      </div>
                      <p className="text-xs text-slate-800 dark:text-slate-200 leading-relaxed whitespace-pre-wrap">
                        {req.description || (isVi ? 'Không có ghi chú' : 'No description')}
                      </p>
                    </div>

                    {/* Admin Timeline / Review History if present */}
                    {(req.reviewedAt || req.adminNote) && (
                      <div className="p-3.5 rounded-xl bg-slate-50/80 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-700/60 space-y-2 text-xs">
                        <div className="flex items-center justify-between text-slate-500 dark:text-slate-400 font-medium">
                          <span className="flex items-center gap-1.5 font-semibold text-slate-700 dark:text-slate-300">
                            <ShieldCheck className="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
                            {(() => {
                              const display = req.reviewedByAdminId || 'Admin';
                              return isVi
                                ? `Xử lý bởi Quản trị viên: ${display}`
                                : `Processed by Admin: ${display}`;
                            })()}
                          </span>
                          {req.reviewedAt && (
                            <span className="font-mono text-[11px]">
                              {formatDate(new Date(req.reviewedAt).toISOString())}
                            </span>
                          )}
                        </div>

                        {req.adminNote && (
                          <div className="text-slate-700 dark:text-slate-300">
                            <span className="font-semibold text-slate-600 dark:text-slate-400">
                              {isVi ? 'Lý do / Ghi chú: ' : 'Admin Note: '}
                            </span>
                            <span>{req.adminNote}</span>
                          </div>
                        )}
                      </div>
                    )}

                    {/* Action buttons (only if not terminal) */}
                    {!isTerminal ? (
                      <div className="flex flex-wrap items-center justify-end gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => handleInitiateReject(req)}
                          className="text-rose-600 hover:text-rose-700 hover:bg-rose-50 dark:hover:bg-rose-950/40 border-rose-200 dark:border-rose-900/60 cursor-pointer text-xs font-semibold gap-1.5"
                        >
                          <Ban className="w-3.5 h-3.5" />
                          <span>{isVi ? 'Từ chối' : 'Reject'}</span>
                        </Button>

                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => handleInitiateMoreInfo(req)}
                          className="text-amber-700 hover:text-amber-800 hover:bg-amber-50 dark:text-amber-400 dark:hover:bg-amber-950/40 border-amber-200 dark:border-amber-900/60 cursor-pointer text-xs font-semibold gap-1.5"
                        >
                          <FileQuestion className="w-3.5 h-3.5" />
                          <span>{isVi ? 'Yêu cầu thêm thông tin' : 'Request More Info'}</span>
                        </Button>

                        {targetUser && (
                          <Button
                            size="sm"
                            onClick={() => handleInitiateApprove(req)}
                            className="bg-emerald-600 hover:bg-emerald-700 text-white cursor-pointer text-xs font-bold gap-1.5 shadow-xs"
                          >
                            <CheckCircle2 className="w-3.5 h-3.5" />
                            <span>{isVi ? 'Phê duyệt (Cần PIN)' : 'Approve (PIN Req.)'}</span>
                          </Button>
                        )}
                      </div>
                    ) : (
                      <div className="flex items-center justify-end pt-2 border-t border-slate-100 dark:border-slate-800 text-xs font-semibold text-slate-400">
                        <span>
                          {statusLower === 'approved'
                            ? (isVi ? '✓ Hồ sơ đã hoàn tất phê duyệt' : '✓ Request approved')
                            : (isVi ? '✗ Hồ sơ đã bị từ chối' : '✗ Request rejected')}
                        </span>
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </div>
        </>
      )}

      {/* APPROVE RECOVERY MODAL */}
      {approveModalRequest && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-200 dark:border-slate-800 shadow-xl space-y-4 animate-in fade-in zoom-in-95">
            <div className="flex items-start justify-between gap-3 pb-3 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2.5">
                <div className="w-9 h-9 rounded-xl bg-emerald-50 dark:bg-emerald-950/50 flex items-center justify-center text-emerald-600 dark:text-emerald-400">
                  <KeyRound className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Phê duyệt khôi phục tài khoản' : 'Approve Account Recovery'}
                  </h3>
                  <p className="text-xs text-slate-500">
                    {isVi ? `Yêu cầu: #${approveModalRequest.id}` : `Request: #${approveModalRequest.id}`}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setApproveModalRequest(null)}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Target summary & consequence info */}
            <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-700/60 text-xs space-y-2">
              <div className="grid grid-cols-2 gap-2 text-slate-600 dark:text-slate-300">
                <div>
                  <span className="text-slate-400">{isVi ? 'Người dùng (hiển thị): ' : 'User: '}</span>
                  <strong>{approveModalRequest.fullName}</strong>
                </div>
                <div>
                  <span className="text-slate-400">{isVi ? 'Số điện thoại: ' : 'Phone: '}</span>
                  <strong className="font-mono">{approveModalRequest.phone}</strong>
                </div>
              </div>

              {(() => {
                const target = getTargetUserDisplay(approveModalRequest);
                if (target.email || target.phone) {
                  return (
                    <div className="pt-2 border-t border-slate-200/60 dark:border-slate-700/60 text-slate-600 dark:text-slate-300">
                      <span className="text-slate-400">{isVi ? 'Tài khoản hệ thống khớp: ' : 'Matched account: '}</span>
                      <strong className="text-blue-600 dark:text-blue-400 font-semibold">{target.name}</strong> {target.email ? `(${target.email})` : ''}
                    </div>
                  );
                }
                return null;
              })()}

              <div className="pt-2 border-t border-slate-200/60 dark:border-slate-700/60 space-y-1 text-slate-600 dark:text-slate-300">
                <div className="font-semibold text-emerald-600 dark:text-emerald-400 flex items-center gap-1">
                  <Info className="w-3.5 h-3.5" />
                  <span>{isVi ? 'Kết quả sau khi phê duyệt:' : 'Result after approval:'}</span>
                </div>
                <ul className="list-disc list-inside space-y-0.5 text-[11px] text-slate-500 dark:text-slate-400">
                  <li>{isVi ? 'Mật khẩu tạm thời sẽ được máy chủ tạo ngẫu nhiên và hiển thị tại đây' : 'A secure temporary password will be generated and displayed'}</li>
                  <li>{isVi ? 'Bắt buộc đổi mật khẩu khi đăng nhập (mustChangePassword=true)' : 'Must change password on next login'}</li>
                  <li>{isVi ? 'Hủy toàn bộ phiên đăng nhập đang hoạt động của người dùng' : 'Target active sessions will be revoked'}</li>
                  <li>{isVi ? 'Gửi thông báo hệ thống tới tài khoản người dùng' : 'User receives persisted system notification'}</li>
                </ul>
              </div>
            </div>

            {/* Mandatory Reason Input */}
            <div className="space-y-1.5">
              <label className="text-xs font-bold text-slate-700 dark:text-slate-300">
                {isVi ? 'Lý do phê duyệt (Bắt buộc)' : 'Approval Reason (Mandatory)'}
              </label>
              <textarea
                rows={3}
                value={approveReason}
                onChange={(e) => {
                  setApproveReason(e.target.value);
                  if (approveError) setApproveError(null);
                }}
                placeholder={isVi ? "Nhập lý do xác minh, căn cứ phê duyệt (bắt buộc)..." : "Enter verification reason (mandatory)..."}
                className="w-full p-2.5 text-xs rounded-xl bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
              />
              {approveError && (
                <p className="text-xs text-rose-600 dark:text-rose-400 flex items-center gap-1 font-medium">
                  <AlertCircle className="w-3.5 h-3.5 shrink-0" />
                  <span>{approveError}</span>
                </p>
              )}
            </div>

            {/* PIN Notice */}
            <div className="p-2.5 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200/60 dark:border-amber-800/40 text-[11px] text-amber-800 dark:text-amber-300 flex items-center gap-2">
              <ShieldAlert className="w-4 h-4 shrink-0 text-amber-600" />
              <span>
                {isVi
                  ? 'Thao tác này yêu cầu nhập mã PIN bảo mật 6 số của Quản trị viên để xác thực danh tính.'
                  : 'This action requires entering your 6-digit Admin Security PIN.'}
              </span>
            </div>

            {/* Actions */}
            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setApproveModalRequest(null)}
                disabled={approveIsSubmitting}
                className="cursor-pointer text-xs"
              >
                {t('common.cancel')}
              </Button>
              <Button
                size="sm"
                onClick={handleConfirmApprove}
                disabled={approveIsSubmitting || !approveReason.trim()}
                className="bg-emerald-600 hover:bg-emerald-700 text-white cursor-pointer text-xs font-bold gap-1.5 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {approveIsSubmitting ? (
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                ) : (
                  <CheckCircle2 className="w-3.5 h-3.5" />
                )}
                <span>{isVi ? 'Xác nhận & Nhập mã PIN' : 'Confirm & Enter PIN'}</span>
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* TEMPORARY PASSWORD RESULT MODAL */}
      {tempPasswordResult && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fadeIn">
          <div className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-200 dark:border-slate-800 shadow-2xl w-full max-w-md space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400">
                <CheckCircle2 className="w-5 h-5" />
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {isVi ? 'Phê duyệt thành công' : 'Recovery Approved'}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setTempPasswordResult(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 p-1 rounded-full cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <p className="text-xs text-slate-600 dark:text-slate-300">
              {isVi
                ? `Mật khẩu tạm thời đã được tạo cho tài khoản "${tempPasswordResult.userName}". Vui lòng sao chép và chuyển an toàn cho người dùng:`
                : `A temporary password has been generated for "${tempPasswordResult.userName}". Please copy and deliver it to the user:`}
            </p>

            <div className="p-4 rounded-2xl bg-slate-100 dark:bg-slate-800 flex items-center justify-between gap-3 border border-slate-200 dark:border-slate-700">
              <span className="font-mono text-base font-bold tracking-wider text-slate-900 dark:text-white select-all">
                {tempPasswordResult.temporaryPassword}
              </span>
              <button
                type="button"
                onClick={() => {
                  navigator.clipboard.writeText(tempPasswordResult.temporaryPassword);
                  setIsCopiedTempPassword(true);
                  setTimeout(() => setIsCopiedTempPassword(false), 2000);
                }}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-white dark:bg-slate-700 text-xs font-semibold text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-600 shadow-xs transition cursor-pointer"
              >
                {isCopiedTempPassword ? (
                  <>
                    <Check className="w-3.5 h-3.5 text-emerald-500" />
                    <span>{isVi ? 'Đã chép' : 'Copied'}</span>
                  </>
                ) : (
                  <>
                    <KeyRound className="w-3.5 h-3.5" />
                    <span>{isVi ? 'Sao chép' : 'Copy'}</span>
                  </>
                )}
              </button>
            </div>

            <div className="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200/60 dark:border-amber-800/40 text-[11px] text-amber-800 dark:text-amber-300 space-y-1">
              <p className="font-semibold flex items-center gap-1">
                <AlertCircle className="w-3.5 h-3.5" />
                <span>{isVi ? 'Lưu ý bảo mật quan trọng:' : 'Security Notice:'}</span>
              </p>
              <p>
                {isVi
                  ? 'Người dùng sẽ được yêu cầu đổi mật khẩu ngay trong lần đăng nhập đầu tiên. Mật khẩu này chỉ hiển thị một lần duy nhất tại đây.'
                  : 'The user will be required to change this password on their next login. This password will not be shown again.'}
              </p>
            </div>

            <div className="pt-2 flex justify-end">
              <Button
                size="sm"
                onClick={() => setTempPasswordResult(null)}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer"
              >
                {t('common.close')}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* REQUEST MORE INFO MODAL */}
      {moreInfoModalRequest && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-200 dark:border-slate-800 shadow-xl space-y-4 animate-in fade-in zoom-in-95">
            <div className="flex items-start justify-between gap-3 pb-3 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2.5">
                <div className="w-9 h-9 rounded-xl bg-amber-50 dark:bg-amber-950/50 flex items-center justify-center text-amber-600 dark:text-amber-400">
                  <FileQuestion className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Yêu cầu bổ sung thông tin' : 'Request More Information'}
                  </h3>
                  <p className="text-xs text-slate-500">
                    {moreInfoModalRequest.fullName} ({moreInfoModalRequest.phone})
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setMoreInfoModalRequest(null)}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-1.5">
              <label className="text-xs font-bold text-slate-700 dark:text-slate-300">
                {isVi ? 'Nội dung câu hỏi / Yêu cầu người dùng bổ sung (Bắt buộc)' : 'Question / Required Info (Mandatory)'}
              </label>
              <textarea
                rows={4}
                value={moreInfoNote}
                onChange={(e) => {
                  setMoreInfoNote(e.target.value);
                  if (moreInfoError) setMoreInfoError(null);
                }}
                placeholder={isVi ? "Ví dụ: Vui lòng cung cấp 4 số cuối CMND/CCCD hoặc ngày đăng ký tài khoản để xác minh..." : "Enter details needed from user..."}
                className="w-full p-2.5 text-xs rounded-xl bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-amber-500"
              />
              {moreInfoError && (
                <p className="text-xs text-rose-600 dark:text-rose-400 flex items-center gap-1 font-medium">
                  <AlertCircle className="w-3.5 h-3.5 shrink-0" />
                  <span>{moreInfoError}</span>
                </p>
              )}
            </div>

            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setMoreInfoModalRequest(null)}
                disabled={moreInfoIsSubmitting}
                className="cursor-pointer text-xs"
              >
                {t('common.cancel')}
              </Button>
              <Button
                size="sm"
                onClick={handleConfirmMoreInfo}
                disabled={moreInfoIsSubmitting || !moreInfoNote.trim()}
                className="bg-amber-600 hover:bg-amber-700 text-white cursor-pointer text-xs font-bold gap-1.5 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {moreInfoIsSubmitting ? (
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                ) : (
                  <Send className="w-3.5 h-3.5" />
                )}
                <span>{isVi ? 'Gửi yêu cầu bổ sung' : 'Send Request'}</span>
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* REJECT RECOVERY MODAL */}
      {rejectModalRequest && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs">
          <div className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-200 dark:border-slate-800 shadow-xl space-y-4 animate-in fade-in zoom-in-95">
            <div className="flex items-start justify-between gap-3 pb-3 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2.5">
                <div className="w-9 h-9 rounded-xl bg-rose-50 dark:bg-rose-950/50 flex items-center justify-center text-rose-600 dark:text-rose-400">
                  <Ban className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Từ chối yêu cầu khôi phục tài khoản' : 'Reject Account Recovery'}
                  </h3>
                  <p className="text-xs text-slate-500">
                    {rejectModalRequest.fullName} ({rejectModalRequest.phone})
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setRejectModalRequest(null)}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200/60 dark:border-rose-900/40 text-xs text-rose-800 dark:text-rose-300 flex items-start gap-2">
              <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5 text-rose-600" />
              <span>
                {isVi
                  ? 'Yêu cầu khôi phục sẽ bị từ chối và chuyển sang trạng thái đã hoàn tất. Hành động này không thể hoàn tác.'
                  : 'This recovery request will be rejected. This action cannot be undone.'}
              </span>
            </div>

            <div className="space-y-1.5">
              <label className="text-xs font-bold text-slate-700 dark:text-slate-300">
                {isVi ? 'Lý do từ chối (Bắt buộc)' : 'Rejection Reason (Mandatory)'}
              </label>
              <textarea
                rows={3}
                value={rejectReason}
                onChange={(e) => {
                  setRejectReason(e.target.value);
                  if (rejectError) setRejectError(null);
                }}
                placeholder={isVi ? "Nhập lý do từ chối yêu cầu (bắt buộc)..." : "Enter rejection reason (mandatory)..."}
                className="w-full p-2.5 text-xs rounded-xl bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-rose-500"
              />
              {rejectError && (
                <p className="text-xs text-rose-600 dark:text-rose-400 flex items-center gap-1 font-medium">
                  <AlertCircle className="w-3.5 h-3.5 shrink-0" />
                  <span>{rejectError}</span>
                </p>
              )}
            </div>

            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setRejectModalRequest(null)}
                disabled={rejectIsSubmitting}
                className="cursor-pointer text-xs"
              >
                {t('common.cancel')}
              </Button>
              <Button
                size="sm"
                onClick={handleConfirmReject}
                disabled={rejectIsSubmitting || !rejectReason.trim()}
                className="bg-rose-600 hover:bg-rose-700 text-white cursor-pointer text-xs font-bold gap-1.5 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {rejectIsSubmitting ? (
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                ) : (
                  <Ban className="w-3.5 h-3.5" />
                )}
                <span>{isVi ? 'Xác nhận từ chối' : 'Confirm Rejection'}</span>
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
