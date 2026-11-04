import React, { useState, useMemo, useEffect } from 'react';
import {
  Search,
  LifeBuoy,
  UserCheck,
  Lock,
  Unlock,
  Send,
  HelpCircle,
  CheckCircle2,
  XCircle,
  RotateCcw,
  AlertTriangle,
  FileCheck2,
  ShieldAlert,
  ShieldCheck,
  Users,
  Scale,
  MessageSquare,
  Check,
  RefreshCw,
  X,
} from 'lucide-react';
import { formatVND, useSpendWise } from '../context/SpendWiseContext';
import { casesApiService } from '../services/casesApiService';
import {
  ADMIN_SUPPORT_REPLY_TEMPLATES,
  AdminGlobalAuditEvent,
  AdminRefundDecisionState,
  AdminRefundSettlementStatus,
  AdminSupportCaseCategory,
  AdminSupportCaseEntry,
  AdminSupportCaseStatus,
  CURRENT_ADMIN_OPERATOR,
} from '../data/adminFixtures';

interface AdminSupportSectionProps {
  cases: AdminSupportCaseEntry[];
  onUpdateCases: React.Dispatch<React.SetStateAction<AdminSupportCaseEntry[]>>;
  onAppendAuditEvent: (event: AdminGlobalAuditEvent) => void;
}

type PendingSupportModalAction =
  | {
      kind: 'approve_refund';
      caseId: string;
      caseCode: string;
      claimCode: string;
      amountVnd: number;
      targetWalletCode: string;
    }
  | {
      kind: 'reject_refund';
      caseId: string;
      caseCode: string;
      claimCode: string;
      amountVnd: number;
      targetWalletCode: string;
    }
  | {
      kind: 'simulate_refund_ledger_credit';
      caseId: string;
      caseCode: string;
      claimCode: string;
      amountVnd: number;
      targetWalletCode: string;
    }
  | {
      kind: 'resolve_case';
      caseId: string;
      caseCode: string;
      subjectVi: string;
      subjectEn: string;
    }
  | {
      kind: 'close_case';
      caseId: string;
      caseCode: string;
      subjectVi: string;
      subjectEn: string;
    };

interface SupportActionReceiptBanner {
  referenceCode: string;
  timestamp: string;
  caseCode: string;
  titleVi: string;
  titleEn: string;
  summaryVi: string;
  summaryEn: string;
  reason: string;
  settlementNoticeVi: string;
  settlementNoticeEn: string;
}

function getSupportStatusMeta(
  status: AdminSupportCaseStatus,
  tr: (vi: string, en: string) => string
): {
  label: string;
  textClass: string;
} {
  switch (status) {
    case 'open':
      return {
        label: tr('Mới tiếp nhận', 'Open'),
        textClass: 'text-[var(--amber)]',
      };
    case 'in_progress':
      return {
        label: tr('Đang xử lý', 'In Progress'),
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'awaiting_user_info':
      return {
        label: tr('Chờ bổ sung thông tin', 'Awaiting User Info'),
        textClass: 'text-[var(--amber)]',
      };
    case 'resolved':
      return {
        label: tr('Đã giải quyết', 'Resolved'),
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'closed':
      return {
        label: tr('Đã đóng hồ sơ', 'Closed'),
        textClass: 'text-[var(--ink3)]',
      };
  }
}

function getPriorityMeta(
  priority: AdminSupportCaseEntry['priority'],
  tr: (vi: string, en: string) => string
): {
  label: string;
  textClass: string;
} {
  switch (priority) {
    case 'critical':
      return {
        label: tr('Khẩn cấp (P1)', 'Critical (P1)'),
        textClass: 'text-[var(--rose)]',
      };
    case 'high':
      return {
        label: tr('Ưu tiên cao (P2)', 'High (P2)'),
        textClass: 'text-[var(--amber)]',
      };
    case 'medium':
      return {
        label: tr('Trung bình (P3)', 'Medium (P3)'),
        textClass: 'text-[var(--ink2)]',
      };
  }
}

function getRefundDecisionMeta(
  decision: AdminRefundDecisionState,
  tr: (vi: string, en: string) => string
): {
  label: string;
  textClass: string;
} {
  switch (decision) {
    case 'none':
      return {
        label: tr('Không yêu cầu hoàn tiền', 'No Refund Claim'),
        textClass: 'text-[var(--ink3)]',
      };
    case 'pending_approval':
      return {
        label: tr('Chờ phê duyệt hoàn tiền (DEMO)', 'Pending Refund Approval (DEMO)'),
        textClass: 'text-[var(--amber)]',
      };
    case 'approved_demo':
      return {
        label: tr('Đã phê duyệt hoàn tiền (DEMO)', 'Refund Approved (DEMO)'),
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'rejected_demo':
      return {
        label: tr('Đã từ chối hoàn tiền (DEMO)', 'Refund Rejected (DEMO)'),
        textClass: 'text-[var(--rose)]',
      };
  }
}

function getRefundSettlementMeta(
  settlement: AdminRefundSettlementStatus,
  tr: (vi: string, en: string) => string
): {
  label: string;
  textClass: string;
} {
  switch (settlement) {
    case 'not_applicable':
      return {
        label: tr('Không áp dụng quyết toán', 'Not Applicable'),
        textClass: 'text-[var(--ink3)]',
      };
    case 'unsettled_pending_decision':
      return {
        label: tr(
          'Chưa quyết toán · Chờ quyết định phê duyệt',
          'Unsettled · Awaiting Approval Decision'
        ),
        textClass: 'text-[var(--amber)]',
      };
    case 'approved_awaiting_ledger_batch':
      return {
        label: tr(
          'Đã duyệt · Chờ lô hạch toán sổ cái DEMO (Tách biệt ngân hàng)',
          'Approved · Awaiting DEMO Ledger Batch (Separate from Bank)'
        ),
        textClass: 'text-[var(--amber)]',
      };
    case 'simulated_ledger_credited_no_bank':
      return {
        label: tr(
          'Đã ghi Có ví mẫu DEMO · Không quyết toán ngân hàng thực',
          'Simulated DEMO Wallet Credit · No Real Bank Settlement'
        ),
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'settlement_cancelled':
      return {
        label: tr(
          'Đã hủy quyết toán (Do từ chối yêu cầu hoàn tiền)',
          'Settlement Cancelled (Refund Rejected)'
        ),
        textClass: 'text-[var(--rose)]',
      };
  }
}

export const AdminSupportSection: React.FC<AdminSupportSectionProps> = ({
  cases,
  onUpdateCases,
  onAppendAuditEvent,
}) => {
  const { tr, language } = useSpendWise();

  // Search & Filter states
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<
    'all' | AdminSupportCaseStatus
  >('all');
  const [categoryFilter, setCategoryFilter] = useState<
    'all' | AdminSupportCaseCategory
  >('all');
  const [assignmentFilter, setAssignmentFilter] = useState<
    'all' | 'assigned_to_me' | 'unassigned' | 'other_operator'
  >('all');
  const [simulatedError, setSimulatedError] = useState(false);

  // Selected Case & Privacy Scope Toggle
  const [selectedCaseId, setSelectedCaseId] = useState<string>('scase_01');
  const [isScopedInspectionEnabled, setIsScopedInspectionEnabled] =
    useState<boolean>(true);

  // Reply & Request Info composer state
  const [replyDraft, setReplyDraft] = useState('');
  const [replyFormError, setReplyFormError] = useState<string | null>(null);

  // Deliberate confirmation modal state for Refund Approval, Resolve, and Close
  const [pendingModalAction, setPendingModalAction] =
    useState<PendingSupportModalAction | null>(null);
  const [modalReasonInput, setModalReasonInput] = useState('');
  const [isModalConfirmed, setIsModalConfirmed] = useState(false);
  const [modalError, setModalError] = useState<string | null>(null);

  // Explicit DEMO action receipt banner
  const [latestReceipt, setLatestReceipt] =
    useState<SupportActionReceiptBanner | null>(null);

  // Summary metrics
  const queueMetrics = useMemo(() => {
    let openOrUnassigned = 0;
    let activeProcessing = 0;
    let pendingRefundCount = 0;
    let resolvedOrClosed = 0;

    for (const c of cases) {
      if (c.status === 'open' || !c.assignedOperator) openOrUnassigned += 1;
      if (c.status === 'in_progress' || c.status === 'awaiting_user_info') {
        activeProcessing += 1;
      }
      if (c.refundClaim?.decisionState === 'pending_approval') {
        pendingRefundCount += 1;
      }
      if (c.status === 'resolved' || c.status === 'closed') {
        resolvedOrClosed += 1;
      }
    }

    return {
      total: cases.length,
      openOrUnassigned,
      activeProcessing,
      pendingRefundCount,
      resolvedOrClosed,
    };
  }, [cases]);

  // Filtered cases
  const filteredCases = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return cases.filter((c) => {
      if (statusFilter !== 'all' && c.status !== statusFilter) return false;
      if (categoryFilter !== 'all' && c.category !== categoryFilter) {
        return false;
      }
      if (assignmentFilter === 'assigned_to_me') {
        if (c.assignedOperator !== CURRENT_ADMIN_OPERATOR) return false;
      } else if (assignmentFilter === 'unassigned') {
        if (c.assignedOperator !== null) return false;
      } else if (assignmentFilter === 'other_operator') {
        if (
          c.assignedOperator === null ||
          c.assignedOperator === CURRENT_ADMIN_OPERATOR
        ) {
          return false;
        }
      }

      if (!q) return true;
      return (
        c.caseCode.toLowerCase().includes(q) ||
        c.subjectVi.toLowerCase().includes(q) ||
        c.subjectEn.toLowerCase().includes(q) ||
        c.scopedUser.fullName.toLowerCase().includes(q) ||
        c.scopedUser.spendId.toLowerCase().includes(q) ||
        (c.scopedFund?.fundNameVi.toLowerCase().includes(q) ?? false) ||
        (c.scopedFund?.fundNameEn.toLowerCase().includes(q) ?? false) ||
        (c.scopedFund?.fundCode.toLowerCase().includes(q) ?? false) ||
        (c.refundClaim?.claimCode.toLowerCase().includes(q) ?? false)
      );
    });
  }, [cases, searchQuery, statusFilter, categoryFilter, assignmentFilter]);

  const selectedCase = useMemo(
    () =>
      cases.find((c) => c.id === selectedCaseId) ||
      filteredCases[0] ||
      cases[0] ||
      null,
    [cases, filteredCases, selectedCaseId]
  );

  const isAssignedToMe =
    selectedCase?.assignedOperator === CURRENT_ADMIN_OPERATOR;
  const canViewScopedSensitiveDetails =
    Boolean(selectedCase) && isAssignedToMe && isScopedInspectionEnabled;

  const handleSelectCase = (item: AdminSupportCaseEntry) => {
    setSelectedCaseId(item.id);
    setReplyFormError(null);
    if (item.assignedOperator === CURRENT_ADMIN_OPERATOR) {
      setIsScopedInspectionEnabled(true);
    }
  };

  // Assign Self Handler
  const handleAssignSelf = (targetCase: AdminSupportCaseEntry) => {
    const nowStamp = '03/11/2026 · 10:40';
    const refSuffix = String(Math.floor(1000 + Math.random() * 9000));
    const refCode = `AUD-SUP-20261103-${refSuffix}`;

    const nextStatus: AdminSupportCaseStatus =
      targetCase.status === 'open' ? 'in_progress' : targetCase.status;

    onUpdateCases((prev) =>
      prev.map((c) => {
        if (c.id !== targetCase.id) return c;
        return {
          ...c,
          assignedOperator: CURRENT_ADMIN_OPERATOR,
          status: nextStatus,
          updatedAt: nowStamp,
          messages: [
            ...c.messages,
            {
              id: `msg_assign_${Date.now()}`,
              senderRole: 'system',
              senderName: 'Hệ thống Phân phối Hồ sơ',
              timestamp: nowStamp,
              bodyVi: `Hồ sơ ${c.caseCode} đã được tiếp nhận xử lý bởi ${CURRENT_ADMIN_OPERATOR}. Đã mở phạm vi dữ liệu nhạy cảm phục vụ thẩm định.`,
              bodyEn: `Case ${c.caseCode} assigned to ${CURRENT_ADMIN_OPERATOR}. Scoped sensitive user and fund context unlocked for review.`,
            },
          ],
        };
      })
    );

    setIsScopedInspectionEnabled(true);

    onAppendAuditEvent({
      id: `gaud_${Date.now()}`,
      referenceCode: refCode,
      timestamp: nowStamp,
      module: 'support',
      moduleLabelVi: 'Hỗ trợ & Khiếu nại',
      moduleLabelEn: 'Support & Complaints',
      actionTitleVi: `Tiếp nhận xử lý hồ sơ ${targetCase.caseCode} (Assign Self)`,
      actionTitleEn: `Assigned support case ${targetCase.caseCode} to self`,
      targetEntity: `${targetCase.caseCode} · ${targetCase.scopedUser.fullName} (${targetCase.scopedUser.spendId})`,
      detailVi: `Người xử lý mới: ${CURRENT_ADMIN_OPERATOR} · Mở phạm vi dữ liệu theo Case`,
      detailEn: `Assigned operator: ${CURRENT_ADMIN_OPERATOR} · Enabled case-scoped data access`,
      reason: 'Tiếp nhận xử lý hồ sơ từ hàng đợi vận hành ca trực.',
      operator: CURRENT_ADMIN_OPERATOR,
    });

    setLatestReceipt({
      referenceCode: refCode,
      timestamp: nowStamp,
      caseCode: targetCase.caseCode,
      titleVi: `Đã tiếp nhận xử lý hồ sơ ${targetCase.caseCode}`,
      titleEn: `Assigned case ${targetCase.caseCode} to ${CURRENT_ADMIN_OPERATOR}`,
      summaryVi: `Hồ sơ của ${targetCase.scopedUser.fullName} (${targetCase.scopedUser.spendId}) đã được gán cho bạn và mở quyền truy cập dữ liệu người dùng / quỹ nhóm trong phạm vi Case.`,
      summaryEn: `Case for ${targetCase.scopedUser.fullName} (${targetCase.scopedUser.spendId}) is now assigned to you with scoped user and group fund inspection enabled.`,
      reason: tr(
        'Nhận xử lý trực tiếp trên hàng đợi Hỗ trợ & Khiếu nại',
        'Direct self-assignment from Support & Complaints queue'
      ),
      settlementNoticeVi:
        'Bảo mật theo phạm vi Case: Chỉ hiển thị thông tin người dùng và quỹ nhóm liên quan trực tiếp tới hồ sơ đã nhận.',
      settlementNoticeEn:
        'Case-Scoped Privacy: Displays only user and group fund context strictly tied to the assigned support case.',
    });
  };

  // Send Reply or Request Information Handler
  const handleSendReplyOrRequestInfo = (mode: 'reply' | 'request_info') => {
    if (!selectedCase) return;
    const trimmed = replyDraft.trim();

    if (trimmed.length < 4) {
      setReplyFormError(
        tr(
          'Vui lòng nhập nội dung phản hồi hoặc chọn một mẫu câu trả lời (tối thiểu 4 ký tự).',
          'Please enter a reply message or select a template (at least 4 characters).'
        )
      );
      return;
    }

    const nowStamp = '03/11/2026 · 10:42';
    const refSuffix = String(Math.floor(1000 + Math.random() * 9000));
    const refCode = `AUD-SUP-20261103-${refSuffix}`;

    const isInfoReq = mode === 'request_info';
    const prefixVi = isInfoReq ? '[YÊU CẦU BỔ SUNG THÔNG TIN] ' : '';
    const prefixEn = isInfoReq ? '[INFORMATION REQUEST] ' : '';

    const nextStatus: AdminSupportCaseStatus = isInfoReq
      ? 'awaiting_user_info'
      : selectedCase.status === 'open' ||
        selectedCase.status === 'awaiting_user_info'
      ? 'in_progress'
      : selectedCase.status;

    // Send reply to real backend API if possible
    casesApiService.adminReplySupportCase(selectedCase.id, { message: `${prefixVi}${trimmed}` }).catch((err) => {
      console.warn('Backend API reply notice:', err);
    });

    onUpdateCases((prev) =>
      prev.map((c) => {
        if (c.id !== selectedCase.id) return c;
        return {
          ...c,
          assignedOperator: c.assignedOperator || CURRENT_ADMIN_OPERATOR,
          status: nextStatus,
          updatedAt: nowStamp,
          messages: [
            ...c.messages,
            {
              id: `msg_${Date.now()}`,
              senderRole: 'operator',
              senderName: CURRENT_ADMIN_OPERATOR,
              timestamp: nowStamp,
              bodyVi: `${prefixVi}${trimmed}`,
              bodyEn: `${prefixEn}${trimmed}`,
              isInfoRequest: isInfoReq,
            },
          ],
        };
      })
    );

    setIsScopedInspectionEnabled(true);
    setReplyDraft('');
    setReplyFormError(null);

    onAppendAuditEvent({
      id: `gaud_${Date.now()}`,
      referenceCode: refCode,
      timestamp: nowStamp,
      module: 'support',
      moduleLabelVi: 'Hỗ trợ & Khiếu nại',
      moduleLabelEn: 'Support & Complaints',
      actionTitleVi: isInfoReq
        ? `Gửi yêu cầu bổ sung thông tin hồ sơ ${selectedCase.caseCode}`
        : `Gửi phản hồi vận hành hồ sơ ${selectedCase.caseCode}`,
      actionTitleEn: isInfoReq
        ? `Requested additional user information on ${selectedCase.caseCode}`
        : `Sent operator reply on ${selectedCase.caseCode}`,
      targetEntity: `${selectedCase.caseCode} · ${selectedCase.scopedUser.fullName}`,
      detailVi: isInfoReq
        ? 'Chuyển trạng thái hồ sơ sang Chờ bổ sung thông tin'
        : 'Đã ghi nhận phản hồi vào luồng trao đổi hồ sơ',
      detailEn: isInfoReq
        ? 'Transitioned case status to Awaiting User Info'
        : 'Recorded operator reply in case conversation thread',
      reason: trimmed,
      operator: CURRENT_ADMIN_OPERATOR,
    });

    setLatestReceipt({
      referenceCode: refCode,
      timestamp: nowStamp,
      caseCode: selectedCase.caseCode,
      titleVi: isInfoReq
        ? `Đã gửi yêu cầu bổ sung thông tin (${selectedCase.caseCode})`
        : `Đã gửi phản hồi tới khách hàng (${selectedCase.caseCode})`,
      titleEn: isInfoReq
        ? `Information request sent (${selectedCase.caseCode})`
        : `Operator reply sent (${selectedCase.caseCode})`,
      summaryVi: isInfoReq
        ? `Hồ sơ ${selectedCase.caseCode} đã chuyển sang trạng thái "Chờ bổ sung thông tin" kèm nội dung yêu cầu chứng từ.`
        : `Phản hồi đã được lưu vào luồng trao đổi của hồ sơ ${selectedCase.caseCode}.`,
      summaryEn: isInfoReq
        ? `Case ${selectedCase.caseCode} moved to "Awaiting User Info" with your evidence request.`
        : `Your reply was appended to the conversation timeline for ${selectedCase.caseCode}.`,
      reason: trimmed,
      settlementNoticeVi:
        'Chế độ DEMO: Tin nhắn chỉ lưu trong trạng thái giả lập cục bộ, không gửi email/SMS thực.',
      settlementNoticeEn:
        'DEMO Mode: Message is stored in local synthetic state only; no external email/SMS is dispatched.',
    });
  };

  // Reopen resolved/closed case
  const handleReopenCase = (targetCase: AdminSupportCaseEntry) => {
    const nowStamp = '03/11/2026 · 10:45';
    const refSuffix = String(Math.floor(1000 + Math.random() * 9000));
    const refCode = `AUD-SUP-20261103-${refSuffix}`;

    onUpdateCases((prev) =>
      prev.map((c) =>
        c.id === targetCase.id
          ? {
              ...c,
              status: 'in_progress',
              assignedOperator: c.assignedOperator || CURRENT_ADMIN_OPERATOR,
              updatedAt: nowStamp,
              messages: [
                ...c.messages,
                {
                  id: `msg_reopen_${Date.now()}`,
                  senderRole: 'system',
                  senderName: CURRENT_ADMIN_OPERATOR,
                  timestamp: nowStamp,
                  bodyVi: `Đã mở lại hồ sơ ${c.caseCode} để tiếp tục rà soát bổ sung.`,
                  bodyEn: `Reopened case ${c.caseCode} for additional operational review.`,
                },
              ],
            }
          : c
      )
    );

    onAppendAuditEvent({
      id: `gaud_${Date.now()}`,
      referenceCode: refCode,
      timestamp: nowStamp,
      module: 'support',
      moduleLabelVi: 'Hỗ trợ & Khiếu nại',
      moduleLabelEn: 'Support & Complaints',
      actionTitleVi: `Mở lại hồ sơ hỗ trợ ${targetCase.caseCode}`,
      actionTitleEn: `Reopened support case ${targetCase.caseCode}`,
      targetEntity: `${targetCase.caseCode} · ${targetCase.scopedUser.fullName}`,
      detailVi: 'Chuyển trạng thái: Đã giải quyết/Đóng → Đang xử lý',
      detailEn: 'Status transition: Resolved/Closed → In Progress',
      reason: 'Mở lại hồ sơ theo yêu cầu kiểm tra bổ sung của vận hành viên.',
      operator: CURRENT_ADMIN_OPERATOR,
    });
  };

  // Modal helpers
  const openConfirmationModal = (action: PendingSupportModalAction) => {
    setPendingModalAction(action);
    setModalReasonInput('');
    setIsModalConfirmed(false);
    setModalError(null);
  };

  const closeConfirmationModal = () => {
    setPendingModalAction(null);
    setModalReasonInput('');
    setIsModalConfirmed(false);
    setModalError(null);
  };

  useEffect(() => {
    if (!pendingModalAction) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        closeConfirmationModal();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [pendingModalAction]);

  const modalPresetReasons = useMemo(() => {
    if (!pendingModalAction) return [];
    switch (pendingModalAction.kind) {
      case 'approve_refund':
        return [
          tr(
            'Đã đối chiếu khớp hóa đơn gốc & sao kê sổ cái, phê duyệt hoàn tiền trên trạng thái DEMO',
            'Verified original invoice and ledger statement; approving refund in DEMO state'
          ),
          tr(
            'Chủ quỹ và thành viên đã thống nhất hoàn trả khoản chênh lệch về quỹ chung',
            'Fund owner and members agreed to return the disputed variance to the shared pool'
          ),
        ];
      case 'reject_refund':
        return [
          tr(
            'Chứng từ không hợp lệ hoặc giao dịch đã xác thực đúng quy định, từ chối hoàn tiền',
            'Invalid supporting evidence or transaction was properly authorized; rejecting refund claim'
          ),
        ];
      case 'simulate_refund_ledger_credit':
        return [
          tr(
            'Thực thi lô ghi Có hoàn tiền vào ví mẫu DEMO (tách biệt quyết toán ngân hàng thực)',
            'Execute simulated DEMO wallet credit batch (strictly separate from real bank settlement)'
          ),
        ];
      case 'resolve_case':
        return [
          tr(
            'Đã hoàn tất đối soát chứng từ, phản hồi kết quả và giải quyết dứt điểm yêu cầu của khách hàng',
            'Completed document verification, communicated outcome, and resolved customer inquiry'
          ),
        ];
      case 'close_case':
        return [
          tr(
            'Khách hàng đã xác nhận đồng ý phương án xử lý, đóng hồ sơ lưu trữ kiểm toán',
            'Customer confirmed resolution; closing support case for audit archive'
          ),
        ];
    }
  }, [pendingModalAction, tr]);

  const handleConfirmModalSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!pendingModalAction) return;

    const trimmedReason = modalReasonInput.trim();
    if (trimmedReason.length < 6) {
      setModalError(
        tr(
          'Vui lòng nhập lý do thẩm định / giải quyết cụ thể (tối thiểu 6 ký tự).',
          'Please enter a specific review or resolution reason (at least 6 characters).'
        )
      );
      return;
    }

    if (!isModalConfirmed) {
      setModalError(
        tr(
          'Vui lòng đánh dấu xác nhận chủ đích trước khi thực thi quyết định.',
          'Please check the deliberate confirmation box before executing this decision.'
        )
      );
      return;
    }

    const nowStamp = '03/11/2026 · 10:48';
    const refSuffix = String(Math.floor(1000 + Math.random() * 9000));
    const refCode = `AUD-SUP-20261103-${refSuffix}`;

    if (pendingModalAction.kind === 'approve_refund') {
      onUpdateCases((prev) =>
        prev.map((c) => {
          if (c.id !== pendingModalAction.caseId || !c.refundClaim) return c;
          return {
            ...c,
            assignedOperator: c.assignedOperator || CURRENT_ADMIN_OPERATOR,
            updatedAt: nowStamp,
            refundClaim: {
              ...c.refundClaim,
              decisionState: 'approved_demo',
              settlementStatus: 'approved_awaiting_ledger_batch',
              decidedByOperator: CURRENT_ADMIN_OPERATOR,
              decidedAt: nowStamp,
              decisionReason: trimmedReason,
            },
            messages: [
              ...c.messages,
              {
                id: `msg_ref_app_${Date.now()}`,
                senderRole: 'operator',
                senderName: CURRENT_ADMIN_OPERATOR,
                timestamp: nowStamp,
                bodyVi: `[PHÊ DUYỆT HOÀN TIỀN DEMO] Đã phê duyệt yêu cầu ${
                  c.refundClaim.claimCode
                } (${formatVND(
                  c.refundClaim.requestedAmountVnd
                )} → ${c.refundClaim.targetWalletCode}). Trạng thái quyết toán độc lập: Chờ lô hạch toán sổ cái DEMO (chưa quyết toán ngân hàng thực). Lý do: ${trimmedReason}`,
                bodyEn: `[DEMO REFUND APPROVED] Approved claim ${
                  c.refundClaim.claimCode
                } (${formatVND(
                  c.refundClaim.requestedAmountVnd
                )} → ${c.refundClaim.targetWalletCode}). Separate settlement status: Awaiting DEMO ledger batch (no real bank settlement). Reason: ${trimmedReason}`,
              },
            ],
          };
        })
      );

      onAppendAuditEvent({
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'support',
        moduleLabelVi: 'Hỗ trợ & Hoàn tiền',
        moduleLabelEn: 'Support & Refunds',
        actionTitleVi: `Phê duyệt hoàn tiền DEMO ${pendingModalAction.claimCode} (${formatVND(
          pendingModalAction.amountVnd
        )})`,
        actionTitleEn: `Approved DEMO refund ${pendingModalAction.claimCode} (${formatVND(
          pendingModalAction.amountVnd
        )})`,
        targetEntity: `${pendingModalAction.caseCode} · Ví nhận: ${pendingModalAction.targetWalletCode}`,
        detailVi:
          'Quyết định: Đã phê duyệt (DEMO) · Trạng thái quyết toán độc lập: Chờ lô hạch toán sổ cái DEMO',
        detailEn:
          'Decision: Approved (DEMO) · Separate settlement status: Awaiting DEMO ledger batch',
        reason: trimmedReason,
        operator: CURRENT_ADMIN_OPERATOR,
      });

      setLatestReceipt({
        referenceCode: refCode,
        timestamp: nowStamp,
        caseCode: pendingModalAction.caseCode,
        titleVi: `Đã phê duyệt hoàn tiền DEMO (${pendingModalAction.claimCode})`,
        titleEn: `DEMO Refund Decision Approved (${pendingModalAction.claimCode})`,
        summaryVi: `Khoản hoàn ${formatVND(
          pendingModalAction.amountVnd
        )} về ví ${pendingModalAction.targetWalletCode} đã chuyển sang trạng thái "Đã phê duyệt (DEMO)". Trạng thái quyết toán được giữ tách biệt ở bước "Chờ lô hạch toán sổ cái DEMO".`,
        summaryEn: `Refund of ${formatVND(
          pendingModalAction.amountVnd
        )} to ${pendingModalAction.targetWalletCode} is now "Approved (DEMO)". Settlement status remains explicitly separate at "Awaiting DEMO Ledger Batch".`,
        reason: trimmedReason,
        settlementNoticeVi:
          'Tách biệt Phê duyệt & Quyết toán: Quyết định phê duyệt không tự động chuyển tiền ngân hàng thực.',
        settlementNoticeEn:
          'Approval vs. Settlement Separation: Approving a refund claim does not trigger real bank settlement.',
      });

      closeConfirmationModal();
      return;
    }

    if (pendingModalAction.kind === 'reject_refund') {
      onUpdateCases((prev) =>
        prev.map((c) => {
          if (c.id !== pendingModalAction.caseId || !c.refundClaim) return c;
          return {
            ...c,
            assignedOperator: c.assignedOperator || CURRENT_ADMIN_OPERATOR,
            updatedAt: nowStamp,
            refundClaim: {
              ...c.refundClaim,
              decisionState: 'rejected_demo',
              settlementStatus: 'settlement_cancelled',
              decidedByOperator: CURRENT_ADMIN_OPERATOR,
              decidedAt: nowStamp,
              decisionReason: trimmedReason,
            },
            messages: [
              ...c.messages,
              {
                id: `msg_ref_rej_${Date.now()}`,
                senderRole: 'operator',
                senderName: CURRENT_ADMIN_OPERATOR,
                timestamp: nowStamp,
                bodyVi: `[TỪ CHỐI HOÀN TIỀN DEMO] Đã từ chối yêu cầu ${c.refundClaim.claimCode}. Trạng thái quyết toán: Đã hủy quyết toán. Lý do: ${trimmedReason}`,
                bodyEn: `[DEMO REFUND REJECTED] Rejected claim ${c.refundClaim.claimCode}. Settlement status: Settlement Cancelled. Reason: ${trimmedReason}`,
              },
            ],
          };
        })
      );

      onAppendAuditEvent({
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'support',
        moduleLabelVi: 'Hỗ trợ & Hoàn tiền',
        moduleLabelEn: 'Support & Refunds',
        actionTitleVi: `Từ chối yêu cầu hoàn tiền DEMO ${pendingModalAction.claimCode}`,
        actionTitleEn: `Rejected DEMO refund claim ${pendingModalAction.claimCode}`,
        targetEntity: `${pendingModalAction.caseCode} · ${pendingModalAction.claimCode}`,
        detailVi:
          'Quyết định: Đã từ chối (DEMO) · Trạng thái quyết toán: Đã hủy quyết toán',
        detailEn:
          'Decision: Rejected (DEMO) · Settlement status: Settlement Cancelled',
        reason: trimmedReason,
        operator: CURRENT_ADMIN_OPERATOR,
      });

      setLatestReceipt({
        referenceCode: refCode,
        timestamp: nowStamp,
        caseCode: pendingModalAction.caseCode,
        titleVi: `Đã từ chối yêu cầu hoàn tiền (${pendingModalAction.claimCode})`,
        titleEn: `Rejected DEMO Refund Claim (${pendingModalAction.claimCode})`,
        summaryVi: `Yêu cầu hoàn tiền ${pendingModalAction.claimCode} đã chuyển sang "Đã từ chối (DEMO)" và trạng thái quyết toán chuyển sang "Đã hủy quyết toán".`,
        summaryEn: `Claim ${pendingModalAction.claimCode} is now "Rejected (DEMO)" with settlement status set to "Settlement Cancelled".`,
        reason: trimmedReason,
        settlementNoticeVi:
          'Mô phỏng DEMO: Đã ghi nhận quyết định từ chối vào Nhật ký Kiểm toán, không phát sinh biến động số dư.',
        settlementNoticeEn:
          'DEMO Simulation: Rejection recorded in Audit Trail with zero balance mutation.',
      });

      closeConfirmationModal();
      return;
    }

    if (pendingModalAction.kind === 'simulate_refund_ledger_credit') {
      onUpdateCases((prev) =>
        prev.map((c) => {
          if (c.id !== pendingModalAction.caseId || !c.refundClaim) return c;
          return {
            ...c,
            updatedAt: nowStamp,
            refundClaim: {
              ...c.refundClaim,
              settlementStatus: 'simulated_ledger_credited_no_bank',
            },
            messages: [
              ...c.messages,
              {
                id: `msg_ref_sim_${Date.now()}`,
                senderRole: 'system',
                senderName: 'Bộ mô phỏng Sổ cái Nội bộ (DEMO)',
                timestamp: nowStamp,
                bodyVi: `[MÔ PHỎNG HẠCH TOÁN SỔ CÁI DEMO] Đã cập nhật trạng thái quyết toán của ${
                  c.refundClaim.claimCode
                } sang "Đã ghi Có ví mẫu DEMO (${
                  c.refundClaim.targetWalletCode
                }) · Không quyết toán ngân hàng thực". Lý do: ${trimmedReason}`,
                bodyEn: `[SIMULATED DEMO LEDGER CREDIT] Updated settlement status for ${
                  c.refundClaim.claimCode
                } to "Simulated DEMO Wallet Credit (${
                  c.refundClaim.targetWalletCode
                }) · No Real Bank Settlement". Reason: ${trimmedReason}`,
              },
            ],
          };
        })
      );

      onAppendAuditEvent({
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'support',
        moduleLabelVi: 'Hỗ trợ & Hoàn tiền',
        moduleLabelEn: 'Support & Refunds',
        actionTitleVi: `Mô phỏng ghi Có lô sổ cái cho hoàn tiền ${pendingModalAction.claimCode}`,
        actionTitleEn: `Simulated DEMO ledger credit batch for refund ${pendingModalAction.claimCode}`,
        targetEntity: `${pendingModalAction.caseCode} · ${pendingModalAction.targetWalletCode}`,
        detailVi: `Trạng thái quyết toán độc lập: Đã ghi Có ví mẫu DEMO (${formatVND(
          pendingModalAction.amountVnd
        )}) · Không quyết toán ngân hàng thực`,
        detailEn: `Separate settlement status: Simulated DEMO Wallet Credit (${formatVND(
          pendingModalAction.amountVnd
        )}) · No Real Bank Settlement`,
        reason: trimmedReason,
        operator: CURRENT_ADMIN_OPERATOR,
      });

      setLatestReceipt({
        referenceCode: refCode,
        timestamp: nowStamp,
        caseCode: pendingModalAction.caseCode,
        titleVi: `Đã mô phỏng ghi Có ví mẫu DEMO (${pendingModalAction.claimCode})`,
        titleEn: `Simulated DEMO Wallet Credit Completed (${pendingModalAction.claimCode})`,
        summaryVi: `Trạng thái quyết toán độc lập của ${pendingModalAction.claimCode} đã chuyển sang "Đã ghi Có ví mẫu DEMO · Không quyết toán ngân hàng thực".`,
        summaryEn: `Separate settlement status for ${pendingModalAction.claimCode} updated to "Simulated DEMO Wallet Credit · No Real Bank Settlement".`,
        reason: trimmedReason,
        settlementNoticeVi:
          'Minh bạch tuyệt đối: Đây là trạng thái mô phỏng sổ cái nội bộ DEMO, không phát sinh lệnh chuyển tiền ngân hàng thực.',
        settlementNoticeEn:
          'Full Transparency: This is a synthetic internal ledger simulation with zero real banking settlement.',
      });

      closeConfirmationModal();
      return;
    }

    // resolve_case or close_case
    const isResolving = pendingModalAction.kind === 'resolve_case';
    const nextCaseStatus: AdminSupportCaseStatus = isResolving
      ? 'resolved'
      : 'closed';

    onUpdateCases((prev) =>
      prev.map((c) => {
        if (c.id !== pendingModalAction.caseId) return c;
        return {
          ...c,
          assignedOperator: c.assignedOperator || CURRENT_ADMIN_OPERATOR,
          status: nextCaseStatus,
          updatedAt: nowStamp,
          resolutionSummaryVi: trimmedReason,
          resolutionSummaryEn: trimmedReason,
          messages: [
            ...c.messages,
            {
              id: `msg_res_${Date.now()}`,
              senderRole: 'operator',
              senderName: CURRENT_ADMIN_OPERATOR,
              timestamp: nowStamp,
              bodyVi: isResolving
                ? `[ĐÃ GIẢI QUYẾT HỒ SƠ] Kết luận xử lý: ${trimmedReason}`
                : `[ĐÃ ĐÓNG HỒ SƠ LƯU TRỮ] Lý do đóng: ${trimmedReason}`,
              bodyEn: isResolving
                ? `[CASE RESOLVED] Resolution summary: ${trimmedReason}`
                : `[CASE CLOSED] Closing note: ${trimmedReason}`,
            },
          ],
        };
      })
    );

    onAppendAuditEvent({
      id: `gaud_${Date.now()}`,
      referenceCode: refCode,
      timestamp: nowStamp,
      module: 'support',
      moduleLabelVi: 'Hỗ trợ & Khiếu nại',
      moduleLabelEn: 'Support & Complaints',
      actionTitleVi: isResolving
        ? `Giải quyết hoàn tất hồ sơ ${pendingModalAction.caseCode}`
        : `Đóng lưu trữ hồ sơ ${pendingModalAction.caseCode}`,
      actionTitleEn: isResolving
        ? `Resolved support case ${pendingModalAction.caseCode}`
        : `Closed support case ${pendingModalAction.caseCode}`,
      targetEntity: `${pendingModalAction.caseCode} · ${pendingModalAction.subjectVi}`,
      detailVi: isResolving
        ? 'Trạng thái mới: Đã giải quyết (Resolved)'
        : 'Trạng thái mới: Đã đóng hồ sơ (Closed)',
      detailEn: isResolving
        ? 'New status: Resolved'
        : 'New status: Closed',
      reason: trimmedReason,
      operator: CURRENT_ADMIN_OPERATOR,
    });

    setLatestReceipt({
      referenceCode: refCode,
      timestamp: nowStamp,
      caseCode: pendingModalAction.caseCode,
      titleVi: isResolving
        ? `Đã đánh dấu giải quyết hồ sơ ${pendingModalAction.caseCode}`
        : `Đã đóng hồ sơ hỗ trợ ${pendingModalAction.caseCode}`,
      titleEn: isResolving
        ? `Marked case ${pendingModalAction.caseCode} as Resolved`
        : `Closed support case ${pendingModalAction.caseCode}`,
      summaryVi: `Hồ sơ ${pendingModalAction.caseCode} đã được cập nhật trạng thái và lưu kết luận xử lý vào Nhật ký Kiểm toán.`,
      summaryEn: `Case ${pendingModalAction.caseCode} status updated and resolution recorded in the Audit Log.`,
      reason: trimmedReason,
      settlementNoticeVi:
        'Đã ghi nhận đầy đủ lịch sử xử lý trên dữ liệu mẫu DEMO.',
      settlementNoticeEn:
        'Full case lifecycle recorded in synthetic DEMO state.',
    });

    closeConfirmationModal();
  };

  const resetFilters = () => {
    setSearchQuery('');
    setStatusFilter('all');
    setCategoryFilter('all');
    setAssignmentFilter('all');
    setSimulatedError(false);
  };

  return (
    <div
      data-testid="sw-admin-screen-support"
      className="space-y-6"
    >
      {/* Flat 4-KPI Summary Strip (no nested cards or repeated page title) */}
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-3.5">
        <div className="sw-card p-4 min-w-0 space-y-1">
          <div className="text-[0.75rem] font-bold text-[var(--ink2)] truncate">
            {tr('Tổng hồ sơ hỗ trợ', 'Total Support Cases')}
          </div>
          <div className="font-display text-[1.25rem] font-extrabold text-[var(--ink)] tabular-nums truncate">
            {queueMetrics.total}
          </div>
        </div>

        <div className="sw-card p-4 min-w-0 space-y-1">
          <div className="text-[0.75rem] font-bold text-[var(--ink2)] truncate">
            {tr('Mới / Chưa phân công', 'Open / Unassigned')}
          </div>
          <div className="font-display text-[1.25rem] font-extrabold text-[var(--amber)] tabular-nums truncate">
            {queueMetrics.openOrUnassigned}
          </div>
        </div>

        <div className="sw-card p-4 min-w-0 space-y-1">
          <div className="text-[0.75rem] font-bold text-[var(--ink2)] truncate">
            {tr('Chờ phê duyệt hoàn tiền (DEMO)', 'Pending Refund Decisions')}
          </div>
          <div
            data-testid="sw-admin-support-kpi-pending-refunds"
            className="font-display text-[1.25rem] font-extrabold text-[var(--amber)] tabular-nums truncate"
          >
            {queueMetrics.pendingRefundCount}
          </div>
        </div>

        <div className="sw-card p-4 min-w-0 space-y-1">
          <div className="text-[0.75rem] font-bold text-[var(--ink2)] truncate">
            {tr('Đã giải quyết / Đã đóng', 'Resolved / Closed')}
          </div>
          <div className="font-display text-[1.25rem] font-extrabold text-[var(--sw-em-d)] tabular-nums truncate">
            {queueMetrics.resolvedOrClosed}
          </div>
        </div>
      </div>

      {/* Compact Operator Scope & Queue Simulation Bar */}
      <div className="flex flex-wrap items-center justify-between gap-2 text-[0.78125rem] text-[var(--ink2)]">
        <span>
          {tr('Vận hành viên đang trực:', 'Active Operator:')}{' '}
          <strong className="font-bold text-[var(--ink)]">
            {CURRENT_ADMIN_OPERATOR}
          </strong>{' '}
          ·{' '}
          {tr(
            'Dữ liệu nhạy cảm chỉ mở khi hồ sơ được gán cho bạn',
            'Sensitive data is scoped to your assigned cases'
          )}
        </span>

        <button
          type="button"
          data-testid="sw-admin-support-simulate-error-btn"
          aria-pressed={simulatedError}
          aria-label={
            simulatedError
              ? tr('Khôi phục hàng đợi', 'Restore queue')
              : tr('Mô phỏng lỗi tải Case', 'Simulate queue error')
          }
          title={
            simulatedError
              ? tr('Khôi phục hàng đợi', 'Restore queue')
              : tr('Mô phỏng lỗi tải Case', 'Simulate queue error')
          }
          onClick={() => setSimulatedError((prev) => !prev)}
          className="group relative sw-focus-ring sw-btn ghost sm !h-9 !w-9 !min-h-[36px] !p-0 flex items-center justify-center shrink-0"
        >
          <AlertTriangle
            aria-hidden="true"
            className={`h-4 w-4 ${simulatedError ? 'text-amber-500' : ''}`}
          />
          <span className="sr-only">
            {simulatedError
              ? tr('Khôi phục hàng đợi', 'Restore queue')
              : tr('Mô phỏng lỗi tải Case', 'Simulate queue error')}
          </span>
          <span
            role="tooltip"
            className="pointer-events-none absolute right-0 top-[calc(100%+6px)] z-50 hidden whitespace-nowrap rounded-lg border border-[var(--line)] bg-[var(--card-solid)] px-2.5 py-1 text-[0.75rem] font-extrabold text-[var(--ink)] shadow-lg group-hover:block group-focus-visible:block"
          >
            {simulatedError
              ? tr('Khôi phục hàng đợi', 'Restore queue')
              : tr('Mô phỏng lỗi tải Case', 'Simulate queue error')}
          </span>
        </button>
      </div>

      {/* Explicit Demo Action Result Banner */}
      {latestReceipt && (
        <div
          role="status"
          aria-live="polite"
          data-testid="sw-admin-support-action-result"
          className="sw-card p-4 sm:p-5 border border-[var(--sw-em)] bg-[var(--sw-em-s)]/60 space-y-2"
        >
          <div className="flex items-start justify-between gap-3">
            <div className="flex items-center gap-2 text-[0.8125rem] font-extrabold text-[var(--sw-em-d)]">
              <FileCheck2 aria-hidden="true" className="h-4 w-4 shrink-0" />
              <span>
                {tr('Biên nhận Thao tác Hỗ trợ DEMO', 'DEMO Support Action Receipt')} ·{' '}
                <span className="tabular-nums">{latestReceipt.referenceCode}</span>
              </span>
            </div>
            <button
              type="button"
              data-testid="sw-admin-dismiss-support-result-btn"
              onClick={() => setLatestReceipt(null)}
              aria-label={tr('Đóng biên nhận kết quả', 'Dismiss action receipt')}
              className="sw-focus-ring sw-ib !h-8 !w-8 !min-h-[32px] !min-w-[32px]"
            >
              <X aria-hidden="true" className="h-4 w-4" />
            </button>
          </div>

          <div className="font-display text-[1rem] font-extrabold text-[var(--ink)]">
            {tr(latestReceipt.titleVi, latestReceipt.titleEn)}
          </div>
          <p className="text-[0.84375rem] text-[var(--ink)] leading-relaxed">
            {tr(latestReceipt.summaryVi, latestReceipt.summaryEn)}
          </p>
          <div className="text-[0.8125rem] text-[var(--ink2)]">
            <span className="font-bold">
              {tr('Nội dung / Lý do ghi nhận:', 'Recorded note / reason:')}
            </span>{' '}
            {latestReceipt.reason}
          </div>
          <div className="pt-1 text-[0.75rem] font-semibold text-[var(--sw-em-d)]">
            {tr(
              latestReceipt.settlementNoticeVi,
              latestReceipt.settlementNoticeEn
            )}
          </div>
        </div>
      )}

      {/* Search & Filter Bar */}
      <div className="sw-card p-4 sm:p-5">
        <div className="grid grid-cols-1 md:grid-cols-12 gap-3">
          {/* Search input */}
          <div className="md:col-span-4 relative">
            <Search
              aria-hidden="true"
              className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-[var(--ink3)]"
            />
            <input
              type="search"
              data-testid="sw-admin-support-search"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={tr(
                'Tìm mã SUP-..., tên KH, SpendID, tên quỹ, mã CLM-...',
                'Search SUP-... code, user, SpendID, fund name, CLM-...'
              )}
              aria-label={tr(
                'Tìm kiếm hồ sơ hỗ trợ và khiếu nại',
                'Search support and complaint cases'
              )}
              className="sw-in w-full !pl-10 !py-2.5 text-[0.84375rem]"
            />
          </div>

          {/* Status filter */}
          <div className="md:col-span-3">
            <label htmlFor="sw-admin-support-status-select" className="sr-only">
              {tr('Lọc theo trạng thái hồ sơ', 'Filter by case status')}
            </label>
            <select
              id="sw-admin-support-status-select"
              data-testid="sw-admin-support-status-filter"
              value={statusFilter}
              onChange={(e) =>
                setStatusFilter(e.target.value as 'all' | AdminSupportCaseStatus)
              }
              className="sw-in w-full !py-2.5 text-[0.84375rem]"
            >
              <option value="all">
                {tr('Tất cả trạng thái hồ sơ', 'All case statuses')}
              </option>
              <option value="open">
                {tr('Mới tiếp nhận (Open)', 'Open')}
              </option>
              <option value="in_progress">
                {tr('Đang xử lý (In Progress)', 'In Progress')}
              </option>
              <option value="awaiting_user_info">
                {tr('Chờ bổ sung thông tin (Awaiting Info)', 'Awaiting User Info')}
              </option>
              <option value="resolved">
                {tr('Đã giải quyết (Resolved)', 'Resolved')}
              </option>
              <option value="closed">
                {tr('Đã đóng hồ sơ (Closed)', 'Closed')}
              </option>
            </select>
          </div>

          {/* Category filter */}
          <div className="md:col-span-3">
            <label htmlFor="sw-admin-support-category-select" className="sr-only">
              {tr('Lọc theo phân loại khiếu nại', 'Filter by case category')}
            </label>
            <select
              id="sw-admin-support-category-select"
              data-testid="sw-admin-support-category-filter"
              value={categoryFilter}
              onChange={(e) =>
                setCategoryFilter(
                  e.target.value as 'all' | AdminSupportCaseCategory
                )
              }
              className="sw-in w-full !py-2.5 text-[0.84375rem]"
            >
              <option value="all">
                {tr('Tất cả nhóm nghiệp vụ', 'All categories')}
              </option>
              <option value="fund_dispute">
                {tr('Khiếu nại Quỹ nhóm', 'Group Fund Dispute')}
              </option>
              <option value="bank_topup">
                {tr('Đối soát Nạp/Rút Ngân hàng', 'Bank Top-Up & Withdrawal')}
              </option>
              <option value="p2p_refund">
                {tr('Hoàn tiền & Chuyển khoản P2P', 'P2P Transfer & Refund')}
              </option>
              <option value="security_pin">
                {tr('Bảo mật & Mã PIN', 'PIN & Account Security')}
              </option>
              <option value="device_loss">
                {tr('Khẩn cấp · Mất thiết bị', 'Emergency · Lost Device')}
              </option>
            </select>
          </div>

          {/* Assignment scope filter */}
          <div className="md:col-span-2">
            <label htmlFor="sw-admin-support-assign-select" className="sr-only">
              {tr('Lọc theo phân công xử lý', 'Filter by operator assignment')}
            </label>
            <select
              id="sw-admin-support-assign-select"
              data-testid="sw-admin-support-assignment-filter"
              value={assignmentFilter}
              onChange={(e) =>
                setAssignmentFilter(
                  e.target.value as
                    | 'all'
                    | 'assigned_to_me'
                    | 'unassigned'
                    | 'other_operator'
                )
              }
              className="sw-in w-full !py-2.5 text-[0.84375rem]"
            >
              <option value="all">
                {tr('Mọi phân công', 'All assignments')}
              </option>
              <option value="assigned_to_me">
                {tr('Đã gán cho tôi', 'Assigned to me')}
              </option>
              <option value="unassigned">
                {tr('Chưa gán (Unassigned)', 'Unassigned')}
              </option>
              <option value="other_operator">
                {tr('Vận hành viên khác', 'Other operator')}
              </option>
            </select>
          </div>
        </div>
      </div>

      {simulatedError ? (
        <div
          role="alert"
          data-testid="sw-admin-support-error-state"
          className="sw-card p-6 text-center space-y-3"
        >
          <div className="mx-auto flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--rose-s)] text-[var(--rose)]">
            <AlertTriangle aria-hidden="true" className="h-5 w-5" />
          </div>
          <h3 className="font-display text-[1rem] font-extrabold text-[var(--ink)]">
            {tr(
              'Không thể tải hàng đợi Hỗ trợ & Khiếu nại (Mô phỏng lỗi)',
              'Unable to load Support & Complaints case queue (Simulated error)'
            )}
          </h3>
          <p className="text-[0.84375rem] text-[var(--ink2)] max-w-md mx-auto">
            {tr(
              'Kết nối tới bộ đọc hàng đợi hồ sơ mẫu đang tạm dừng để kiểm thử trạng thái chịu lỗi của giao diện.',
              'Connection to the synthetic support queue reader is paused to verify error state resilience.'
            )}
          </p>
          <div>
            <button
              type="button"
              data-testid="sw-admin-support-error-retry-btn"
              onClick={() => setSimulatedError(false)}
              className="sw-focus-ring sw-btn sw-btn-pri sm"
            >
              <RefreshCw aria-hidden="true" className="h-4 w-4" />
              <span>{tr('Thử tải lại hàng đợi', 'Retry loading queue')}</span>
            </button>
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-1 xl:grid-cols-12 gap-6 items-start min-w-0">
          {/* Left 5 cols: Case Queue Table */}
          <div className="xl:col-span-5 min-w-0 sw-card overflow-hidden">
            <div className="flex flex-wrap items-center justify-between gap-2 p-4 sm:px-5 border-b border-[var(--line)]">
              <div>
                <h3 className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                  {tr(
                    'Hàng đợi Hồ sơ Hỗ trợ & Khiếu nại',
                    'Support & Complaints Case Queue'
                  )}
                </h3>
                <div className="text-[0.75rem] text-[var(--ink2)]">
                  {tr(
                    `Hiển thị ${filteredCases.length} / ${cases.length} hồ sơ`,
                    `Showing ${filteredCases.length} of ${cases.length} cases`
                  )}
                </div>
              </div>

              {(searchQuery ||
                statusFilter !== 'all' ||
                categoryFilter !== 'all' ||
                assignmentFilter !== 'all') && (
                <button
                  type="button"
                  data-testid="sw-admin-support-reset-filters-btn"
                  onClick={resetFilters}
                  className="sw-focus-ring sw-btn ghost sm"
                >
                  <span>{tr('Đặt lại bộ lọc', 'Reset filters')}</span>
                </button>
              )}
            </div>

            {filteredCases.length === 0 ? (
              <div
                data-testid="sw-admin-support-empty"
                className="p-8 text-center space-y-3"
              >
                <div className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                  {tr(
                    'Không tìm thấy hồ sơ hỗ trợ phù hợp',
                    'No matching support cases found'
                  )}
                </div>
                <p className="text-[0.8125rem] text-[var(--ink2)] max-w-sm mx-auto">
                  {tr(
                    'Thử xóa từ khóa tìm kiếm hoặc chọn lại trạng thái hồ sơ và bộ lọc phân công.',
                    'Try clearing your search query or adjusting the status and assignment filters.'
                  )}
                </p>
                <button
                  type="button"
                  onClick={resetFilters}
                  className="sw-focus-ring sw-btn ghost sm"
                >
                  <span>{tr('Hiển thị toàn bộ hồ sơ', 'Show all cases')}</span>
                </button>
              </div>
            ) : (
              <div className="w-full max-w-full overflow-x-auto">
                <table
                  data-testid="sw-admin-support-queue-table"
                  aria-label={tr(
                    'Bảng hàng đợi hồ sơ hỗ trợ và khiếu nại',
                    'Support and complaints case queue table'
                  )}
                  className="w-full text-left border-collapse"
                >
                  <thead>
                    <tr className="border-b border-[var(--line)] text-[0.75rem] font-bold text-[var(--ink3)]">
                      <th scope="col" className="py-3 pl-4 pr-2">
                        {tr('Mã Case & Ưu tiên', 'Case & Priority')}
                      </th>
                      <th scope="col" className="py-3 px-2">
                        {tr('Nội dung & Khách hàng', 'Subject & User')}
                      </th>
                      <th scope="col" className="py-3 pl-2 pr-4">
                        {tr('Trạng thái & Phân công', 'Status & Assignee')}
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--line)] text-[0.8125rem]">
                    {filteredCases.map((c) => {
                      const isSelected = selectedCase?.id === c.id;
                      const statusMeta = getSupportStatusMeta(c.status, tr);
                      const prioMeta = getPriorityMeta(c.priority, tr);
                      const isMine =
                        c.assignedOperator === CURRENT_ADMIN_OPERATOR;

                      return (
                        <tr
                          key={c.id}
                          data-testid={`sw-admin-support-case-row-${c.caseCode}`}
                          aria-selected={isSelected}
                          onClick={() => handleSelectCase(c)}
                          className={`cursor-pointer transition-colors ${
                            isSelected
                              ? 'bg-[var(--sw-em-s)]/75'
                              : 'hover:bg-[var(--sw-surface-muted)]/60'
                          }`}
                        >
                          <td className="py-3.5 pl-4 pr-2 align-top whitespace-nowrap">
                            <div className="font-bold text-[var(--ink)] tabular-nums">
                              {c.caseCode}
                            </div>
                            <div
                              className={`text-[0.75rem] font-bold ${prioMeta.textClass}`}
                            >
                              {prioMeta.label}
                            </div>
                            <div className="text-[0.6875rem] text-[var(--ink3)] tabular-nums">
                              {c.updatedAt}
                            </div>
                          </td>

                          <td className="py-3.5 px-2 align-top">
                            <div className="font-bold text-[var(--ink)] line-clamp-2">
                              {tr(c.subjectVi, c.subjectEn)}
                            </div>
                            <div className="text-[0.75rem] text-[var(--ink2)] mt-0.5">
                              {c.scopedUser.fullName} ({c.scopedUser.spendId})
                            </div>
                            <div className="text-[0.6875rem] text-[var(--ink3)] mt-0.5">
                              {tr(c.categoryLabelVi, c.categoryLabelEn)}
                              {c.scopedFund
                                ? ` · ${c.scopedFund.fundCode}`
                                : ''}
                              {c.refundClaim
                                ? ` · ${formatVND(
                                    c.refundClaim.requestedAmountVnd
                                  )}`
                                : ''}
                            </div>
                          </td>

                          <td className="py-3.5 pl-2 pr-4 align-top">
                            <div
                              data-testid={`sw-admin-support-row-status-${c.caseCode}`}
                              className={`font-bold ${statusMeta.textClass}`}
                            >
                              {statusMeta.label}
                            </div>
                            <div
                              className={`text-[0.75rem] mt-0.5 ${
                                isMine
                                  ? 'font-bold text-[var(--sw-em-d)]'
                                  : c.assignedOperator
                                  ? 'text-[var(--ink2)]'
                                  : 'font-bold text-[var(--amber)]'
                              }`}
                            >
                              {c.assignedOperator
                                ? isMine
                                  ? tr('Của tôi (Ca 1)', 'Assigned to me')
                                  : c.assignedOperator
                                : tr('Chưa gán xử lý', 'Unassigned')}
                            </div>
                            {c.refundClaim && (
                              <div className="text-[0.6875rem] text-[var(--ink3)] mt-0.5">
                                {
                                  getRefundDecisionMeta(
                                    c.refundClaim.decisionState,
                                    tr
                                  ).label
                                }
                              </div>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Right 7 cols: Case Details, Scoped User/Fund Context, Refund Workflow & Reply Composer */}
          <aside
            aria-label={tr(
              'Chi tiết hồ sơ hỗ trợ, dữ liệu phạm vi Case và phê duyệt hoàn tiền',
              'Support case details, scoped user/fund data, and refund approval inspector'
            )}
            data-testid="sw-admin-support-case-inspector"
            className="xl:col-span-7 min-w-0 space-y-5"
          >
            {selectedCase ? (
              <>
                {/* Card 1: Case Header, Assignment & Resolve/Close Controls */}
                <div className="sw-card p-5 space-y-4">
                  <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3 border-b border-[var(--line)] pb-4">
                    <div className="space-y-1">
                      <div className="flex flex-wrap items-center gap-2 text-[0.75rem]">
                        <span className="font-extrabold text-[var(--sw-em-d)] tabular-nums">
                          {selectedCase.caseCode}
                        </span>
                        <span className="text-[var(--ink3)]">·</span>
                        <span className="font-semibold text-[var(--ink2)]">
                          {tr(
                            selectedCase.categoryLabelVi,
                            selectedCase.categoryLabelEn
                          )}
                        </span>
                        <span className="text-[var(--ink3)]">·</span>
                        <span
                          className={`font-bold ${
                            getPriorityMeta(selectedCase.priority, tr).textClass
                          }`}
                        >
                          {getPriorityMeta(selectedCase.priority, tr).label}
                        </span>
                      </div>

                      <h3 className="font-display text-[1.125rem] font-extrabold text-[var(--ink)]">
                        {tr(selectedCase.subjectVi, selectedCase.subjectEn)}
                      </h3>

                      <p className="text-[0.8125rem] text-[var(--ink2)] leading-relaxed">
                        {tr(selectedCase.summaryVi, selectedCase.summaryEn)}
                      </p>
                    </div>

                    <div className="sm:text-right shrink-0 space-y-1">
                      <div
                        data-testid="sw-admin-support-inspector-status"
                        className={`font-display text-[0.9375rem] font-extrabold ${
                          getSupportStatusMeta(selectedCase.status, tr)
                            .textClass
                        }`}
                      >
                        {getSupportStatusMeta(selectedCase.status, tr).label}
                      </div>
                      <div className="text-[0.75rem] text-[var(--ink2)] tabular-nums">
                        {tr('Cập nhật:', 'Updated:')} {selectedCase.updatedAt}
                      </div>
                    </div>
                  </div>

                  {/* Operator Assignment & Case Lifecycle Bar */}
                  <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
                    <div className="text-[0.8125rem]">
                      <span className="text-[var(--ink2)]">
                        {tr('Vận hành viên phụ trách:', 'Assigned Operator:')}
                      </span>{' '}
                      <strong
                        data-testid="sw-admin-support-inspector-assignee"
                        className={`font-bold ${
                          isAssignedToMe
                            ? 'text-[var(--sw-em-d)]'
                            : selectedCase.assignedOperator
                            ? 'text-[var(--ink)]'
                            : 'text-[var(--amber)]'
                        }`}
                      >
                        {selectedCase.assignedOperator ||
                          tr(
                            'Chưa gán (Cần nhận xử lý để mở phạm vi dữ liệu)',
                            'Unassigned (Assign self to unlock scoped data)'
                          )}
                      </strong>
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                      {!isAssignedToMe && (
                        <button
                          type="button"
                          data-testid="sw-admin-support-assign-self-btn"
                          onClick={() => handleAssignSelf(selectedCase)}
                          className="sw-focus-ring sw-btn sw-btn-pri sm whitespace-nowrap"
                        >
                          <UserCheck aria-hidden="true" className="h-4 w-4" />
                          <span>
                            {tr(
                              'Nhận xử lý hồ sơ (Assign Self)',
                              'Assign to Me (Assign Self)'
                            )}
                          </span>
                        </button>
                      )}

                      {selectedCase.status !== 'resolved' &&
                        selectedCase.status !== 'closed' && (
                          <>
                            <button
                              type="button"
                              data-testid="sw-admin-support-resolve-btn"
                              onClick={() =>
                                openConfirmationModal({
                                  kind: 'resolve_case',
                                  caseId: selectedCase.id,
                                  caseCode: selectedCase.caseCode,
                                  subjectVi: selectedCase.subjectVi,
                                  subjectEn: selectedCase.subjectEn,
                                })
                              }
                              className="sw-focus-ring sw-btn ghost sm whitespace-nowrap"
                            >
                              <CheckCircle2
                                aria-hidden="true"
                                className="h-4 w-4 text-[var(--sw-em-d)]"
                              />
                              <span>
                                {tr('Giải quyết (Resolve)', 'Resolve Case')}
                              </span>
                            </button>

                            <button
                              type="button"
                              data-testid="sw-admin-support-close-btn"
                              onClick={() =>
                                openConfirmationModal({
                                  kind: 'close_case',
                                  caseId: selectedCase.id,
                                  caseCode: selectedCase.caseCode,
                                  subjectVi: selectedCase.subjectVi,
                                  subjectEn: selectedCase.subjectEn,
                                })
                              }
                              className="sw-focus-ring sw-btn ghost sm whitespace-nowrap"
                            >
                              <XCircle aria-hidden="true" className="h-4 w-4" />
                              <span>{tr('Đóng hồ sơ', 'Close Case')}</span>
                            </button>
                          </>
                        )}

                      {(selectedCase.status === 'resolved' ||
                        selectedCase.status === 'closed') && (
                        <button
                          type="button"
                          data-testid="sw-admin-support-reopen-btn"
                          onClick={() => handleReopenCase(selectedCase)}
                          className="sw-focus-ring sw-btn ghost sm whitespace-nowrap"
                        >
                          <RotateCcw aria-hidden="true" className="h-4 w-4" />
                          <span>{tr('Mở lại hồ sơ', 'Reopen Case')}</span>
                        </button>
                      )}
                    </div>
                  </div>

                  {selectedCase.resolutionSummaryVi && (
                    <div className="pt-3 border-t border-[var(--line)] text-[0.8125rem]">
                      <span className="font-bold text-[var(--sw-em-d)]">
                        {tr('Kết luận giải quyết:', 'Resolution Summary:')}
                      </span>{' '}
                      <span className="text-[var(--ink)]">
                        {tr(
                          selectedCase.resolutionSummaryVi,
                          selectedCase.resolutionSummaryEn ||
                            selectedCase.resolutionSummaryVi
                        )}
                      </span>
                    </div>
                  )}
                </div>

                {/* Card 2: Case-Scoped Sensitive User & Group Fund Context */}
                <div className="sw-card p-5 space-y-4">
                  <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 border-b border-[var(--line)] pb-3">
                    <div>
                      <div className="text-[0.75rem] font-bold text-[var(--sw-em-d)]">
                        {tr(
                          'Dữ liệu Nhạy cảm theo Phạm vi Hồ sơ (Scoped User & Fund Context)',
                          'Case-Scoped Sensitive User & Fund Context'
                        )}
                      </div>
                      <h4 className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                        {selectedCase.scopedUser.fullName} (
                        {selectedCase.scopedUser.spendId})
                        {selectedCase.scopedFund
                          ? ` · ${tr(
                              selectedCase.scopedFund.fundNameVi,
                              selectedCase.scopedFund.fundNameEn
                            )}`
                          : ''}
                      </h4>
                    </div>

                    {isAssignedToMe && (
                      <button
                        type="button"
                        data-testid="sw-admin-support-scope-toggle"
                        onClick={() =>
                          setIsScopedInspectionEnabled((prev) => !prev)
                        }
                        className="sw-focus-ring sw-btn ghost sm whitespace-nowrap"
                      >
                        {isScopedInspectionEnabled ? (
                          <>
                            <Lock aria-hidden="true" className="h-3.5 w-3.5" />
                            <span>
                              {tr(
                                'Ẩn dữ liệu phạm vi Case',
                                'Lock Scoped Details'
                              )}
                            </span>
                          </>
                        ) : (
                          <>
                            <Unlock aria-hidden="true" className="h-3.5 w-3.5" />
                            <span>
                              {tr(
                                'Mở dữ liệu phạm vi Case',
                                'Unlock Scoped Details'
                              )}
                            </span>
                          </>
                        )}
                      </button>
                    )}
                  </div>

                  {!canViewScopedSensitiveDetails ? (
                    <div
                      data-testid="sw-admin-support-scope-gate"
                      className="rounded-2xl border border-[var(--line)] bg-[var(--sw-surface-muted)]/70 p-4 space-y-3"
                    >
                      <div className="flex items-start gap-3">
                        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-[var(--amber-s)] text-[var(--amber)]">
                          <Lock aria-hidden="true" className="h-4 w-4" />
                        </div>
                        <div className="space-y-1">
                          <div className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                            {tr(
                              'Dữ liệu nhạy cảm của Người dùng & Quỹ nhóm đang được khóa bảo vệ',
                              'Sensitive User & Group Fund Details are Scope-Restricted'
                            )}
                          </div>
                          <p className="text-[0.8125rem] text-[var(--ink2)] leading-relaxed">
                            {!isAssignedToMe
                              ? tr(
                                  `Hồ sơ ${selectedCase.caseCode} hiện chưa được gán cho bạn (${CURRENT_ADMIN_OPERATOR}). Theo chính sách bảo mật tối thiểu (Least-Privilege), vui lòng bấm "Nhận xử lý hồ sơ" để mở phạm vi tra cứu hồ sơ người dùng và chi tiết quỹ nhóm.`,
                                  `Case ${selectedCase.caseCode} is not currently assigned to you (${CURRENT_ADMIN_OPERATOR}). Under least-privilege privacy policy, click "Assign to Me" to unlock scoped user and group fund details.`
                                )
                              : tr(
                                  'Bạn đang tạm khóa chế độ xem dữ liệu theo phạm vi Case. Bấm mở lại để kiểm tra chi tiết người dùng và chứng từ quỹ nhóm.',
                                  'Scoped case inspection is currently toggled off. Enable it to view user and group fund details.'
                                )}
                          </p>
                        </div>
                      </div>

                      <div>
                        {!isAssignedToMe ? (
                          <button
                            type="button"
                            data-testid="sw-admin-support-gate-assign-self-btn"
                            onClick={() => handleAssignSelf(selectedCase)}
                            className="sw-focus-ring sw-btn sw-btn-pri sm"
                          >
                            <UserCheck aria-hidden="true" className="h-4 w-4" />
                            <span>
                              {tr(
                                'Nhận xử lý & Mở phạm vi dữ liệu Case',
                                'Assign Self & Unlock Scoped Case Data'
                              )}
                            </span>
                          </button>
                        ) : (
                          <button
                            type="button"
                            onClick={() => setIsScopedInspectionEnabled(true)}
                            className="sw-focus-ring sw-btn sw-btn-pri sm"
                          >
                            <Unlock aria-hidden="true" className="h-4 w-4" />
                            <span>
                              {tr(
                                'Mở phạm vi dữ liệu Case',
                                'Unlock Scoped Case Data'
                              )}
                            </span>
                          </button>
                        )}
                      </div>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      {/* Scoped User Details */}
                      <div
                        data-testid="sw-admin-support-scoped-user-details"
                        className="space-y-2 text-[0.8125rem]"
                      >
                        <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                          {tr(
                            '1. Hồ sơ Người dùng trong phạm vi Case',
                            '1. Scoped User Profile & Security Context'
                          )}
                        </div>
                        <div className="sw-kv">
                          <span className="text-[var(--ink2)]">
                            {tr('Liên hệ đã che (Masked)', 'Masked Contact')}
                          </span>
                          <span className="font-semibold text-[var(--ink)] tabular-nums">
                            {selectedCase.scopedUser.emailMasked} ·{' '}
                            {selectedCase.scopedUser.phoneMasked}
                          </span>
                        </div>
                        <div className="sw-kv">
                          <span className="text-[var(--ink2)]">
                            {tr('Cấp độ định danh & Ví chính', 'KYC Tier & Wallet')}
                          </span>
                          <span className="font-bold text-[var(--ink)] tabular-nums text-right">
                            {tr(
                              selectedCase.scopedUser.kycTierVi,
                              selectedCase.scopedUser.kycTierEn
                            )}{' '}
                            · {selectedCase.scopedUser.walletCode} (
                            {formatVND(selectedCase.scopedUser.walletBalanceVnd)})
                          </span>
                        </div>
                        <div className="sw-kv">
                          <span className="text-[var(--ink2)]">
                            {tr('Phiên thiết bị gần nhất', 'Recent Device Session')}
                          </span>
                          <span className="font-semibold text-[var(--ink)] text-right">
                            {tr(
                              selectedCase.scopedUser.recentDeviceSummaryVi,
                              selectedCase.scopedUser.recentDeviceSummaryEn
                            )}
                          </span>
                        </div>
                        <div className="sw-kv">
                          <span className="text-[var(--ink2)]">
                            {tr('Ghi chú an toàn tài khoản', 'Security Standing')}
                          </span>
                          <span className="font-semibold text-[var(--sw-em-d)] text-right">
                            {tr(
                              selectedCase.scopedUser.securityNoteVi,
                              selectedCase.scopedUser.securityNoteEn
                            )}
                          </span>
                        </div>
                      </div>

                      {/* Scoped Group Fund Details (if case involves a Group Fund) */}
                      {selectedCase.scopedFund && (
                        <div
                          data-testid="sw-admin-support-scoped-fund-details"
                          className="pt-3 border-t border-[var(--line)] space-y-2 text-[0.8125rem]"
                        >
                          <div className="flex items-center gap-2 text-[0.75rem] font-bold text-[var(--sw-em-d)]">
                            <Users aria-hidden="true" className="h-4 w-4" />
                            <span>
                              {tr(
                                '2. Chi tiết Quỹ nhóm trong phạm vi Case (Scoped Fund Case Details)',
                                '2. Scoped Group Fund Case Details'
                              )}
                            </span>
                          </div>

                          <div className="sw-kv">
                            <span className="text-[var(--ink2)]">
                              {tr('Tên quỹ & Mã sổ cái quỹ', 'Fund Name & Code')}
                            </span>
                            <span className="font-bold text-[var(--ink)] text-right">
                              {tr(
                                selectedCase.scopedFund.fundNameVi,
                                selectedCase.scopedFund.fundNameEn
                              )}{' '}
                              ({selectedCase.scopedFund.fundCode})
                            </span>
                          </div>

                          <div className="sw-kv">
                            <span className="text-[var(--ink2)]">
                              {tr('Cơ chế vận hành & Chủ quỹ', 'Fund Mode & Owner')}
                            </span>
                            <span className="font-semibold text-[var(--ink)] text-right">
                              {tr(
                                selectedCase.scopedFund.fundModeVi,
                                selectedCase.scopedFund.fundModeEn
                              )}{' '}
                              · {selectedCase.scopedFund.ownerName} (
                              {selectedCase.scopedFund.memberCount}{' '}
                              {tr('thành viên', 'members')})
                            </span>
                          </div>

                          <div className="sw-kv">
                            <span className="text-[var(--ink2)]">
                              {tr(
                                'Số dư Quỹ khả dụng / Đang giữ chỗ 24h',
                                'Fund Liquid / 24h Reserved Balance'
                              )}
                            </span>
                            <span className="font-bold text-[var(--ink)] tabular-nums text-right">
                              {formatVND(
                                selectedCase.scopedFund.fundAvailableBalanceVnd
                              )}{' '}
                              /{' '}
                              <span className="text-[var(--amber)]">
                                {formatVND(
                                  selectedCase.scopedFund.fundHeld24hBalanceVnd
                                )}{' '}
                                (24h Hold)
                              </span>
                            </span>
                          </div>

                          <div className="sw-kv">
                            <span className="text-[var(--ink2)]">
                              {tr(
                                'Bút toán & Giá trị đang thẩm định',
                                'Disputed TX & Amount'
                              )}
                            </span>
                            <span className="font-bold text-[var(--ink)] tabular-nums text-right">
                              {selectedCase.scopedFund.disputedTxCode} ·{' '}
                              {formatVND(selectedCase.scopedFund.disputedAmountVnd)}
                            </span>
                          </div>

                          <div className="pt-1 space-y-1.5">
                            <div>
                              <span className="text-[0.75rem] font-bold text-[var(--ink2)]">
                                {tr(
                                  'Chi tiết khoản mục quỹ:',
                                  'Fund Item Breakdown:'
                                )}
                              </span>
                              <p className="text-[var(--ink)] mt-0.5 leading-relaxed">
                                {tr(
                                  selectedCase.scopedFund.disputedItemSummaryVi,
                                  selectedCase.scopedFund.disputedItemSummaryEn
                                )}
                              </p>
                            </div>
                            <div>
                              <span className="text-[0.75rem] font-bold text-[var(--ink2)]">
                                {tr(
                                  'Đối chiếu chứng từ & Nghĩa vụ thành viên:',
                                  'Receipt Verification & Member Standing:'
                                )}
                              </span>
                              <p className="font-semibold text-[var(--sw-em-d)] mt-0.5 leading-relaxed">
                                {tr(
                                  selectedCase.scopedFund.receiptVerificationVi,
                                  selectedCase.scopedFund.receiptVerificationEn
                                )}{' '}
                                —{' '}
                                {tr(
                                  selectedCase.scopedFund.memberStandingSummaryVi,
                                  selectedCase.scopedFund.memberStandingSummaryEn
                                )}
                              </p>
                            </div>
                          </div>
                        </div>
                      )}
                    </div>
                  )}
                </div>

                {/* Card 3: Refund Approval Workflow (Synthetic Decision State + Separate Settlement Status) */}
                {selectedCase.refundClaim && (
                  <div
                    data-testid="sw-admin-support-refund-panel"
                    className="sw-card p-5 space-y-4"
                  >
                    <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-2 border-b border-[var(--line)] pb-3">
                      <div>
                        <div className="flex items-center gap-2 text-[0.75rem] font-bold text-[var(--sw-em-d)]">
                          <Scale aria-hidden="true" className="h-4 w-4" />
                          <span>
                            {tr(
                              'Quy trình Phê duyệt Hoàn tiền & Trạng thái Quyết toán Độc lập (DEMO)',
                              'Refund Approval Workflow & Separate Settlement Status (DEMO)'
                            )}
                          </span>
                        </div>
                        <h4 className="mt-1 font-display text-[1rem] font-extrabold text-[var(--ink)] tabular-nums">
                          {selectedCase.refundClaim.claimCode} ·{' '}
                          {formatVND(
                            selectedCase.refundClaim.requestedAmountVnd
                          )}
                        </h4>
                      </div>

                      <div className="text-[0.75rem] text-[var(--ink2)] tabular-nums sm:text-right">
                        <div>
                          {tr('Bút toán gốc:', 'Source TX:')}{' '}
                          <strong className="text-[var(--ink)]">
                            {selectedCase.refundClaim.sourceTxCode}
                          </strong>
                        </div>
                        <div>
                          {tr('Ví nhận hoàn:', 'Target Wallet:')}{' '}
                          <strong className="text-[var(--ink)]">
                            {selectedCase.refundClaim.targetWalletCode}
                          </strong>
                        </div>
                      </div>
                    </div>

                    <p className="text-[0.8125rem] text-[var(--ink)] leading-relaxed">
                      {tr(
                        selectedCase.refundClaim.claimReasonVi,
                        selectedCase.refundClaim.claimReasonEn
                      )}
                    </p>

                    {/* Explicit Two-State Separation: Decision State vs Settlement Status */}
                    <div className="space-y-2 text-[0.8125rem] pt-1">
                      <div className="sw-kv">
                        <span className="text-[var(--ink2)] font-semibold">
                          {tr(
                            '1. Trạng thái Quyết định Hoàn tiền (DEMO Decision State)',
                            '1. Synthetic Refund Decision State'
                          )}
                        </span>
                        <span
                          data-testid="sw-admin-support-refund-decision-state"
                          className={`font-extrabold ${
                            getRefundDecisionMeta(
                              selectedCase.refundClaim.decisionState,
                              tr
                            ).textClass
                          }`}
                        >
                          {
                            getRefundDecisionMeta(
                              selectedCase.refundClaim.decisionState,
                              tr
                            ).label
                          }
                        </span>
                      </div>

                      <div className="sw-kv">
                        <span className="text-[var(--ink2)] font-semibold">
                          {tr(
                            '2. Trạng thái Quyết toán Độc lập (Separate Settlement Status)',
                            '2. Explicit Separate Settlement Status'
                          )}
                        </span>
                        <span
                          data-testid="sw-admin-support-refund-settlement-status"
                          className={`font-extrabold text-right ${
                            getRefundSettlementMeta(
                              selectedCase.refundClaim.settlementStatus,
                              tr
                            ).textClass
                          }`}
                        >
                          {
                            getRefundSettlementMeta(
                              selectedCase.refundClaim.settlementStatus,
                              tr
                            ).label
                          }
                        </span>
                      </div>

                      {selectedCase.refundClaim.decisionReason && (
                        <div className="sw-kv">
                          <span className="text-[var(--ink2)]">
                            {tr(
                              'Lý do & Người ra quyết định',
                              'Decision Reason & Operator'
                            )}
                          </span>
                          <span className="font-semibold text-[var(--ink)] text-right">
                            {selectedCase.refundClaim.decisionReason} (
                            {selectedCase.refundClaim.decidedByOperator} ·{' '}
                            {selectedCase.refundClaim.decidedAt})
                          </span>
                        </div>
                      )}
                    </div>

                    {/* Refund Decision & Settlement Action Buttons */}
                    <div className="flex flex-wrap items-center gap-2 pt-3 border-t border-[var(--line)]">
                      {selectedCase.refundClaim.decisionState ===
                        'pending_approval' && (
                        <>
                          <button
                            type="button"
                            data-testid="sw-admin-support-refund-approve-btn"
                            onClick={() =>
                              openConfirmationModal({
                                kind: 'approve_refund',
                                caseId: selectedCase.id,
                                caseCode: selectedCase.caseCode,
                                claimCode: selectedCase.refundClaim!.claimCode,
                                amountVnd:
                                  selectedCase.refundClaim!.requestedAmountVnd,
                                targetWalletCode:
                                  selectedCase.refundClaim!.targetWalletCode,
                              })
                            }
                            className="sw-focus-ring sw-btn sw-btn-pri sm whitespace-nowrap"
                          >
                            <Check aria-hidden="true" className="h-4 w-4" />
                            <span>
                              {tr(
                                'Phê duyệt hoàn tiền (DEMO)',
                                'Approve Refund (DEMO)'
                              )}
                            </span>
                          </button>

                          <button
                            type="button"
                            data-testid="sw-admin-support-refund-reject-btn"
                            onClick={() =>
                              openConfirmationModal({
                                kind: 'reject_refund',
                                caseId: selectedCase.id,
                                caseCode: selectedCase.caseCode,
                                claimCode: selectedCase.refundClaim!.claimCode,
                                amountVnd:
                                  selectedCase.refundClaim!.requestedAmountVnd,
                                targetWalletCode:
                                  selectedCase.refundClaim!.targetWalletCode,
                              })
                            }
                            className="sw-focus-ring sw-btn ghost sm whitespace-nowrap"
                          >
                            <XCircle aria-hidden="true" className="h-4 w-4" />
                            <span>
                              {tr(
                                'Từ chối hoàn tiền (DEMO)',
                                'Reject Refund (DEMO)'
                              )}
                            </span>
                          </button>
                        </>
                      )}

                      {selectedCase.refundClaim.decisionState ===
                        'approved_demo' &&
                        selectedCase.refundClaim.settlementStatus ===
                          'approved_awaiting_ledger_batch' && (
                          <button
                            type="button"
                            data-testid="sw-admin-support-refund-simulate-credit-btn"
                            onClick={() =>
                              openConfirmationModal({
                                kind: 'simulate_refund_ledger_credit',
                                caseId: selectedCase.id,
                                caseCode: selectedCase.caseCode,
                                claimCode: selectedCase.refundClaim!.claimCode,
                                amountVnd:
                                  selectedCase.refundClaim!.requestedAmountVnd,
                                targetWalletCode:
                                  selectedCase.refundClaim!.targetWalletCode,
                              })
                            }
                            className="sw-focus-ring sw-btn sw-btn-pri sm whitespace-nowrap"
                          >
                            <ShieldCheck
                              aria-hidden="true"
                              className="h-4 w-4"
                            />
                            <span>
                              {tr(
                                'Mô phỏng chạy lô ghi Có ví mẫu (DEMO)',
                                'Simulate DEMO Ledger Credit Batch'
                              )}
                            </span>
                          </button>
                        )}
                    </div>

                    <div className="text-[0.75rem] text-[var(--ink3)] leading-relaxed">
                      {tr(
                        'Nguyên tắc tách biệt: Quyết định phê duyệt hoàn tiền trên Web Admin chỉ cập nhật trạng thái phê duyệt DEMO; trạng thái quyết toán được theo dõi riêng và không bao giờ tự động gọi cổng thanh toán ngân hàng thực.',
                        'Separation Principle: Refund approval decisions update synthetic DEMO state only; settlement status is tracked separately and never triggers real bank payment rails.'
                      )}
                    </div>
                  </div>
                )}

                {/* Card 4: Conversation Timeline, Reply Templates & Request Information */}
                <div className="sw-card p-5 space-y-4">
                  <div className="flex items-center justify-between gap-2 border-b border-[var(--line)] pb-3">
                    <div className="flex items-center gap-2">
                      <MessageSquare
                        aria-hidden="true"
                        className="h-4 w-4 text-[var(--sw-em-d)]"
                      />
                      <h4 className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                        {tr(
                          `Luồng Trao đổi & Phản hồi (${selectedCase.messages.length})`,
                          `Case Timeline & Replies (${selectedCase.messages.length})`
                        )}
                      </h4>
                    </div>
                  </div>

                  {/* Messages Thread */}
                  <div
                    data-testid="sw-admin-support-messages-list"
                    className="space-y-3 max-h-[340px] overflow-y-auto pr-1"
                  >
                    {selectedCase.messages.map((msg) => {
                      const isOperator = msg.senderRole === 'operator';
                      const isSystem = msg.senderRole === 'system';

                      return (
                        <div
                          key={msg.id}
                          data-testid={`sw-admin-support-msg-${msg.id}`}
                          className={`rounded-2xl p-3.5 text-[0.8125rem] space-y-1 ${
                            isOperator
                              ? 'bg-[var(--sw-em-s)]/65 border border-[var(--sw-em)]/40'
                              : isSystem
                              ? 'bg-[var(--sw-surface-muted)]/80 border border-[var(--line)]'
                              : 'bg-[var(--sw-surface-muted)]/45 border border-[var(--line)]'
                          }`}
                        >
                          <div className="flex flex-wrap items-center justify-between gap-2 text-[0.75rem]">
                            <span className="font-extrabold text-[var(--ink)]">
                              {msg.senderName}
                            </span>
                            <span className="text-[var(--ink3)] tabular-nums">
                              {msg.timestamp}
                            </span>
                          </div>
                          <p className="text-[var(--ink)] leading-relaxed">
                            {tr(msg.bodyVi, msg.bodyEn)}
                          </p>
                        </div>
                      );
                    })}
                  </div>

                  {/* Reply Templates */}
                  <div className="pt-3 border-t border-[var(--line)] space-y-2">
                    <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                      {tr(
                        'Chèn nhanh mẫu câu trả lời chuẩn (Reply Templates):',
                        'Insert quick operational reply template:'
                      )}
                    </div>
                    <div className="flex flex-wrap gap-1.5">
                      {ADMIN_SUPPORT_REPLY_TEMPLATES.map((tpl) => (
                        <button
                          key={tpl.id}
                          type="button"
                          data-testid={`sw-admin-support-template-${tpl.id}`}
                          onClick={() => {
                            setReplyDraft(
                              language === 'en' ? tpl.bodyEn : tpl.bodyVi
                            );
                            setReplyFormError(null);
                          }}
                          className="sw-focus-ring rounded-lg border border-[var(--line)] bg-[var(--sw-surface-muted)] px-2.5 py-1.5 text-left text-[0.75rem] font-semibold text-[var(--ink2)] hover:text-[var(--ink)] cursor-pointer"
                        >
                          {tr(tpl.labelVi, tpl.labelEn)}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Reply & Request Information Composer */}
                  <div
                    data-testid="sw-admin-support-reply-form"
                    className="space-y-2.5 pt-1"
                  >
                    <label
                      htmlFor="sw-admin-support-reply-textarea"
                      className="block text-[0.8125rem] font-bold text-[var(--ink)]"
                    >
                      {tr(
                        'Nội dung phản hồi khách hàng hoặc yêu cầu bổ sung chứng từ',
                        'Operator reply or request for additional information'
                      )}
                    </label>
                    <textarea
                      id="sw-admin-support-reply-textarea"
                      data-testid="sw-admin-support-reply-input"
                      rows={3}
                      value={replyDraft}
                      onChange={(e) => {
                        setReplyDraft(e.target.value);
                        if (replyFormError) setReplyFormError(null);
                      }}
                      placeholder={tr(
                        'Nhập phản hồi cho khách hàng hoặc liệt kê chứng từ cần bổ sung...',
                        'Type your reply to the user or specify required supporting documents...'
                      )}
                      className="sw-in w-full text-[0.84375rem]"
                    />

                    {replyFormError && (
                      <div
                        role="alert"
                        data-testid="sw-admin-support-reply-error"
                        className="rounded-xl bg-[var(--rose-s)] px-3 py-2 text-[0.8125rem] font-bold text-[var(--rose)]"
                      >
                        {replyFormError}
                      </div>
                    )}

                    <div className="flex flex-wrap items-center justify-end gap-2">
                      <button
                        type="button"
                        data-testid="sw-admin-support-request-info-btn"
                        onClick={() =>
                          handleSendReplyOrRequestInfo('request_info')
                        }
                        className="sw-focus-ring sw-btn ghost sm whitespace-nowrap"
                      >
                        <HelpCircle aria-hidden="true" className="h-4 w-4" />
                        <span>
                          {tr(
                            'Yêu cầu bổ sung thông tin (Request Info)',
                            'Request Information'
                          )}
                        </span>
                      </button>

                      <button
                        type="button"
                        data-testid="sw-admin-support-send-reply-btn"
                        onClick={() => handleSendReplyOrRequestInfo('reply')}
                        className="sw-focus-ring sw-btn sw-btn-pri sm whitespace-nowrap"
                      >
                        <Send aria-hidden="true" className="h-4 w-4" />
                        <span>
                          {tr('Gửi phản hồi (DEMO)', 'Send Reply (DEMO)')}
                        </span>
                      </button>
                    </div>
                  </div>
                </div>
              </>
            ) : null}
          </aside>
        </div>
      )}

      {/* =====================================================================
          DELIBERATE CONFIRMATION MODAL (REFUND APPROVAL/REJECTION, RESOLVE, CLOSE)
          ===================================================================== */}
      {pendingModalAction && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="sw-admin-support-confirm-title"
          data-testid="sw-admin-support-confirm-modal"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
        >
          <form
            onSubmit={handleConfirmModalSubmit}
            className="sw-card w-full max-w-lg max-h-[calc(100dvh-2rem)] flex flex-col overflow-hidden p-0 shadow-xl bg-[var(--sw-sheet-bg)]"
          >
            <div className="shrink-0 flex items-start justify-between gap-3 p-5 sm:px-6 sm:pt-5 sm:pb-3.5 border-b border-[var(--line)]">
              <div className="flex items-center gap-2.5 min-w-0">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-[var(--amber-s)] text-[var(--amber)]">
                  <ShieldAlert aria-hidden="true" className="h-5 w-5" />
                </div>
                <div className="min-w-0">
                  <h3
                    id="sw-admin-support-confirm-title"
                    className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]"
                  >
                    {pendingModalAction.kind === 'approve_refund'
                      ? tr(
                          `Xác nhận Phê duyệt Hoàn tiền DEMO (${pendingModalAction.claimCode})`,
                          `Confirm DEMO Refund Approval (${pendingModalAction.claimCode})`
                        )
                      : pendingModalAction.kind === 'reject_refund'
                      ? tr(
                          `Xác nhận Từ chối Hoàn tiền DEMO (${pendingModalAction.claimCode})`,
                          `Confirm DEMO Refund Rejection (${pendingModalAction.claimCode})`
                        )
                      : pendingModalAction.kind ===
                        'simulate_refund_ledger_credit'
                      ? tr(
                          `Xác nhận Mô phỏng Ghi Có Ví mẫu (${pendingModalAction.claimCode})`,
                          `Confirm Simulated DEMO Wallet Credit (${pendingModalAction.claimCode})`
                        )
                      : pendingModalAction.kind === 'resolve_case'
                      ? tr(
                          `Xác nhận Giải quyết Hồ sơ ${pendingModalAction.caseCode}`,
                          `Confirm Resolving Case ${pendingModalAction.caseCode}`
                        )
                      : tr(
                          `Xác nhận Đóng Hồ sơ ${pendingModalAction.caseCode}`,
                          `Confirm Closing Case ${pendingModalAction.caseCode}`
                        )}
                  </h3>
                  <div className="text-[0.75rem] text-[var(--ink2)]">
                    {tr(
                      'Yêu cầu nhập lý do thẩm định và ghi vào Nhật ký Kiểm toán DEMO',
                      'Requires review reason and records a DEMO Audit Log entry'
                    )}
                  </div>
                </div>
              </div>

              <button
                type="button"
                onClick={closeConfirmationModal}
                aria-label={tr('Đóng hộp thoại xác nhận', 'Close modal')}
                className="sw-focus-ring sw-ib !h-9 !w-9 !min-h-[36px] !min-w-[36px] shrink-0"
              >
                <X aria-hidden="true" className="h-4 w-4" />
              </button>
            </div>

            <div className="flex-1 min-h-0 overflow-y-auto overscroll-contain p-5 sm:px-6 py-4 space-y-4 sw-sheet-body">
              {/* Summary box */}
              <div className="rounded-xl border border-[var(--line)] bg-[var(--sw-surface-muted)]/60 p-3.5 text-[0.8125rem] space-y-1">
                {(pendingModalAction.kind === 'approve_refund' ||
                  pendingModalAction.kind === 'reject_refund' ||
                  pendingModalAction.kind ===
                    'simulate_refund_ledger_credit') && (
                  <>
                    <div>
                      <span className="font-bold text-[var(--ink2)]">
                        {tr('Mã yêu cầu & Số tiền:', 'Claim & Amount:')}
                      </span>{' '}
                      <span className="font-extrabold text-[var(--ink)] tabular-nums">
                        {pendingModalAction.claimCode} ·{' '}
                        {formatVND(pendingModalAction.amountVnd)}
                      </span>
                    </div>
                    <div>
                      <span className="font-bold text-[var(--ink2)]">
                        {tr('Ví đích:', 'Target Wallet:')}
                      </span>{' '}
                      <span className="font-bold text-[var(--ink)] tabular-nums">
                        {pendingModalAction.targetWalletCode}
                      </span>
                    </div>
                  </>
                )}
                {(pendingModalAction.kind === 'resolve_case' ||
                  pendingModalAction.kind === 'close_case') && (
                  <div>
                    <span className="font-bold text-[var(--ink2)]">
                      {tr('Hồ sơ:', 'Case:')}
                    </span>{' '}
                    <span className="font-bold text-[var(--ink)]">
                      {pendingModalAction.caseCode} —{' '}
                      {tr(
                        pendingModalAction.subjectVi,
                        pendingModalAction.subjectEn
                      )}
                    </span>
                  </div>
                )}
                <div className="text-[0.75rem] text-[var(--sw-em-d)] font-semibold pt-1">
                  {tr(
                    'Minh bạch DEMO: Quyết định hoàn tiền và trạng thái quyết toán được quản lý trên dữ liệu mẫu, không gọi lệnh ngân hàng thực.',
                    'DEMO Transparency: Refund decision and settlement status operate strictly on synthetic state with zero real bank calls.'
                  )}
                </div>
              </div>

              {/* Quick preset reasons */}
              {modalPresetReasons.length > 0 && (
                <div className="space-y-1.5">
                  <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                    {tr('Gợi ý lý do nhanh:', 'Quick reason templates:')}
                  </div>
                  <div className="flex flex-wrap gap-1.5">
                    {modalPresetReasons.map((preset, idx) => (
                      <button
                        key={idx}
                        type="button"
                        data-testid={`sw-admin-support-preset-reason-${idx}`}
                        onClick={() => {
                          setModalReasonInput(preset);
                          setModalError(null);
                        }}
                        className="sw-focus-ring rounded-lg border border-[var(--line)] bg-[var(--sw-surface-muted)] px-2.5 py-1.5 text-left text-[0.75rem] font-semibold text-[var(--ink2)] hover:text-[var(--ink)] cursor-pointer"
                      >
                        {preset}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Mandatory reason textarea */}
              <div className="space-y-1.5">
                <label
                  htmlFor="sw-admin-support-modal-reason"
                  className="block text-[0.8125rem] font-bold text-[var(--ink)]"
                >
                  {tr(
                    'Lý do thẩm định / Kết luận xử lý (Bắt buộc) *',
                    'Review justification / Resolution note (Required) *'
                  )}
                </label>
                <textarea
                  id="sw-admin-support-modal-reason"
                  data-testid="sw-admin-support-modal-reason-input"
                  rows={3}
                  value={modalReasonInput}
                  onChange={(e) => {
                    setModalReasonInput(e.target.value);
                    if (modalError) setModalError(null);
                  }}
                  placeholder={tr(
                    'Nhập căn cứ chứng từ hoặc kết luận xử lý (tối thiểu 6 ký tự)...',
                    'Enter supporting evidence reference or resolution note (min 6 chars)...'
                  )}
                  className="sw-in w-full text-[0.84375rem]"
                />
              </div>

              {/* Deliberate confirmation checkbox */}
              <label className="flex items-start gap-2.5 text-[0.8125rem] text-[var(--ink)] cursor-pointer select-none">
                <input
                  type="checkbox"
                  data-testid="sw-admin-support-modal-confirm-checkbox"
                  checked={isModalConfirmed}
                  onChange={(e) => {
                    setIsModalConfirmed(e.target.checked);
                    if (modalError) setModalError(null);
                  }}
                  className="mt-1 h-4 w-4 rounded border-[var(--line)] accent-[#059669] shrink-0"
                />
                <span>
                  {tr(
                    'Tôi xác nhận thực hiện quyết định DEMO này và hiểu rằng trạng thái quyết toán được theo dõi tách biệt (không phát sinh chuyển tiền thực).',
                    'I deliberately confirm this DEMO decision and understand settlement status is tracked separately (no real money movement).'
                  )}
                </span>
              </label>

              {modalError && (
                <div
                  role="alert"
                  data-testid="sw-admin-support-modal-error"
                  className="rounded-xl bg-[var(--rose-s)] px-3 py-2 text-[0.8125rem] font-bold text-[var(--rose)]"
                >
                  {modalError}
                </div>
              )}
            </div>

            <div className="shrink-0 flex items-center justify-end gap-2 px-5 sm:px-6 py-3.5 border-t border-[var(--line)] bg-[var(--sw-sheet-bg)]">
              <button
                type="button"
                data-testid="sw-admin-support-modal-cancel-btn"
                onClick={closeConfirmationModal}
                className="sw-focus-ring sw-btn ghost sm"
              >
                <span>{tr('Hủy bỏ', 'Cancel')}</span>
              </button>
              <button
                type="submit"
                data-testid="sw-admin-support-modal-submit-btn"
                className="sw-focus-ring sw-btn sw-btn-pri sm"
              >
                <Check aria-hidden="true" className="h-4 w-4" />
                <span>
                  {tr('Xác nhận & Lưu Kiểm toán', 'Confirm & Record Audit')}
                </span>
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
};
