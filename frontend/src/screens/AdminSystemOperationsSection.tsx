import React, { useState, useMemo, useEffect } from 'react';
import {
  Search,
  Megaphone,
  Cpu,
  Building2,
  Settings,
  ClipboardList,
  Eye,
  Send,
  Save,
  PauseCircle,
  PlayCircle,
  Wrench,
  CheckCircle2,
  AlertTriangle,
  ShieldAlert,
  ShieldCheck,
  Lock,
  FileCheck2,
  RefreshCw,
  Check,
  X,
} from 'lucide-react';
import { useSpendWise } from '../context/SpendWiseContext';
import {
  ADMIN_BROADCAST_AUDIENCE_OPTIONS,
  ADMIN_BROADCAST_TEMPLATES,
  AdminAiPipelineDomain,
  AdminAiPipelineEntry,
  AdminAiPipelineState,
  AdminBankGatewayEntry,
  AdminBankGatewayState,
  AdminBankRailType,
  AdminBroadcastAudienceId,
  AdminBroadcastCategory,
  AdminBroadcastEntry,
  AdminBroadcastStatus,
  AdminGlobalAuditEvent,
  AdminSystemConfigParameter,
  CURRENT_ADMIN_OPERATOR,
} from '../data/adminFixtures';

export type AdminSystemSubTabId =
  | 'all'
  | 'broadcast'
  | 'ai_ops'
  | 'bank_gateways'
  | 'config_audit';

interface AdminSystemOperationsSectionProps {
  broadcasts: AdminBroadcastEntry[];
  onUpdateBroadcasts: React.Dispatch<React.SetStateAction<AdminBroadcastEntry[]>>;
  aiPipelines: AdminAiPipelineEntry[];
  onUpdateAiPipelines: React.Dispatch<
    React.SetStateAction<AdminAiPipelineEntry[]>
  >;
  bankGateways: AdminBankGatewayEntry[];
  onUpdateBankGateways: React.Dispatch<
    React.SetStateAction<AdminBankGatewayEntry[]>
  >;
  configParams: AdminSystemConfigParameter[];
  onUpdateConfigParams: React.Dispatch<
    React.SetStateAction<AdminSystemConfigParameter[]>
  >;
  auditEvents: AdminGlobalAuditEvent[];
  onAppendAuditEvent: (event: AdminGlobalAuditEvent) => void;
  activeSubTab?: AdminSystemSubTabId;
  onChangeSubTab?: (tab: AdminSystemSubTabId) => void;
}

type PendingA5ConfirmationAction =
  | {
      kind: 'publish_authored_broadcast';
      titleVi: string;
      titleEn: string;
      bodyVi: string;
      bodyEn: string;
      category: AdminBroadcastCategory;
      audienceId: AdminBroadcastAudienceId;
      channelVi: string;
      channelEn: string;
    }
  | {
      kind: 'publish_existing_broadcast';
      broadcastId: string;
      broadcastCode: string;
      titleVi: string;
      titleEn: string;
      audienceLabelVi: string;
      audienceLabelEn: string;
      estimatedRecipients: number;
    }
  | {
      kind: 'cancel_existing_broadcast';
      broadcastId: string;
      broadcastCode: string;
      titleVi: string;
      titleEn: string;
    }
  | {
      kind: 'toggle_ai_pipeline_state';
      pipelineId: string;
      pipelineCode: string;
      nameVi: string;
      nameEn: string;
      currentState: AdminAiPipelineState;
      targetState: AdminAiPipelineState;
    }
  | {
      kind: 'switch_bank_gateway_state';
      gatewayId: string;
      gatewayCode: string;
      bankName: string;
      currentState: AdminBankGatewayState;
      targetState: AdminBankGatewayState;
    }
  | {
      kind: 'verify_system_config';
      paramCount: number;
    };

interface A5ActionReceiptBanner {
  referenceCode: string;
  timestamp: string;
  domainLabelVi: string;
  domainLabelEn: string;
  titleVi: string;
  titleEn: string;
  summaryVi: string;
  summaryEn: string;
  reason: string;
  demoSafetyNoticeVi: string;
  demoSafetyNoticeEn: string;
}

function getBroadcastCategoryMeta(
  cat: AdminBroadcastCategory,
  tr: (vi: string, en: string) => string
): { labelVi: string; labelEn: string; textClass: string } {
  switch (cat) {
    case 'system_maintenance':
      return {
        labelVi: 'Bảo trì & Vận hành',
        labelEn: 'Maintenance & Ops',
        textClass: 'text-[var(--amber)]',
      };
    case 'security_advisory':
      return {
        labelVi: 'Cảnh báo Bảo mật',
        labelEn: 'Security Advisory',
        textClass: 'text-[var(--rose)]',
      };
    case 'fund_cycle_reminder':
      return {
        labelVi: 'Nhắc lịch Quỹ nhóm',
        labelEn: 'Group Fund Reminder',
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'feature_update':
      return {
        labelVi: 'Cập nhật Tính năng',
        labelEn: 'Feature Update',
        textClass: 'text-[var(--ink)]',
      };
  }
}

function getBroadcastStatusMeta(
  status: AdminBroadcastStatus,
  tr: (vi: string, en: string) => string
): { label: string; textClass: string } {
  switch (status) {
    case 'published_demo':
      return {
        label: tr('Đã phát hành (DEMO)', 'Published (DEMO)'),
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'scheduled_demo':
      return {
        label: tr('Đang lên lịch (DEMO)', 'Scheduled (DEMO)'),
        textClass: 'text-[var(--amber)]',
      };
    case 'draft':
      return {
        label: tr('Bản nháp (Draft)', 'Draft'),
        textClass: 'text-[var(--ink2)]',
      };
    case 'cancelled_demo':
      return {
        label: tr('Đã thu hồi (DEMO)', 'Cancelled (DEMO)'),
        textClass: 'text-[var(--rose)]',
      };
  }
}

function getAiPipelineStateMeta(
  state: AdminAiPipelineState,
  tr: (vi: string, en: string) => string
): { label: string; textClass: string } {
  switch (state) {
    case 'healthy_demo':
      return {
        label: tr('Hoạt động tốt (DEMO)', 'Healthy (DEMO)'),
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'degraded_latency_demo':
      return {
        label: tr('Độ trễ cao (DEMO)', 'Elevated Latency (DEMO)'),
        textClass: 'text-[var(--amber)]',
      };
    case 'guardrail_blocked_demo':
      return {
        label: tr('Đang chặn lọc chính sách (DEMO)', 'Guardrail Intercept Active (DEMO)'),
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'paused_demo':
      return {
        label: tr('Đang tạm dừng (DEMO)', 'Paused (DEMO)'),
        textClass: 'text-[var(--rose)]',
      };
  }
}

function getBankGatewayStateMeta(
  state: AdminBankGatewayState,
  tr: (vi: string, en: string) => string
): { label: string; textClass: string } {
  switch (state) {
    case 'operational_demo':
      return {
        label: tr('Hoạt động ổn định (DEMO)', 'Operational (DEMO)'),
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'degraded_latency_demo':
      return {
        label: tr('Độ trễ cao · Cảnh báo (DEMO)', 'Degraded Latency (DEMO)'),
        textClass: 'text-[var(--amber)]',
      };
    case 'maintenance_demo':
      return {
        label: tr('Đang bảo trì (DEMO)', 'Maintenance Mode (DEMO)'),
        textClass: 'text-[var(--rose)]',
      };
  }
}

export const AdminSystemOperationsSection: React.FC<
  AdminSystemOperationsSectionProps
> = ({
  broadcasts,
  onUpdateBroadcasts,
  aiPipelines,
  onUpdateAiPipelines,
  bankGateways,
  onUpdateBankGateways,
  configParams,
  onUpdateConfigParams,
  auditEvents,
  onAppendAuditEvent,
  activeSubTab: controlledSubTab,
  onChangeSubTab,
}) => {
  const { tr, language } = useSpendWise();

  const [internalSubTab, setInternalSubTab] = useState<AdminSystemSubTabId>(
    controlledSubTab || 'broadcast'
  );
  const activeSubTab = controlledSubTab || internalSubTab;
  const setActiveSubTab = (next: AdminSystemSubTabId) => {
    setInternalSubTab(next);
    if (onChangeSubTab) {
      onChangeSubTab(next);
    }
  };
  const [simulatedSectionError, setSimulatedSectionError] = useState(false);

  // 1. Broadcast Authoring state
  const [draftTitle, setDraftTitle] = useState(
    'Thông báo bảo trì định kỳ cổng liên kết ngân hàng Napas 247 (01:00 – 02:30)'
  );
  const [draftBody, setDraftBody] = useState(
    'Cổng nạp/rút liên ngân hàng sẽ bảo trì nâng cấp định kỳ từ 01:00 đến 02:30 sáng mai. Chuyển tiền P2P nội bộ và đóng góp Quỹ nhóm vẫn hoạt động bình thường 100%.'
  );
  const [draftCategory, setDraftCategory] =
    useState<AdminBroadcastCategory>('system_maintenance');
  const [draftAudienceId, setDraftAudienceId] =
    useState<AdminBroadcastAudienceId>('bank_linked_wallets');
  const [draftChannel, setDraftChannel] = useState<'in_app_banner' | 'inbox_only'>(
    'in_app_banner'
  );
  const [authorFormError, setAuthorFormError] = useState<string | null>(null);

  // Broadcast history filter state
  const [broadcastSearchQuery, setBroadcastSearchQuery] = useState('');
  const [broadcastStatusFilter, setBroadcastStatusFilter] = useState<
    'all' | AdminBroadcastStatus
  >('all');

  // 2. AI Operations Monitoring filter state
  const [aiSearchQuery, setAiSearchQuery] = useState('');
  const [aiDomainFilter, setAiDomainFilter] = useState<
    'all' | AdminAiPipelineDomain
  >('all');
  const [aiStateFilter, setAiStateFilter] = useState<
    'all' | AdminAiPipelineState
  >('all');

  // 3. Bank Gateway Monitoring filter state
  const [bankSearchQuery, setBankSearchQuery] = useState('');
  const [bankStateFilter, setBankStateFilter] = useState<
    'all' | AdminBankGatewayState
  >('all');
  const [bankRailFilter, setBankRailFilter] = useState<
    'all' | AdminBankRailType
  >('all');

  // 4. Config & Audit filter state
  const [auditSearchQuery, setAuditSearchQuery] = useState('');
  const [auditModuleFilter, setAuditModuleFilter] = useState<
    'all' | AdminGlobalAuditEvent['module']
  >('all');

  // 5. Deliberate confirmation modal state
  const [pendingAction, setPendingAction] =
    useState<PendingA5ConfirmationAction | null>(null);
  const [modalReasonInput, setModalReasonInput] = useState('');
  const [isModalConfirmed, setIsModalConfirmed] = useState(false);
  const [modalError, setModalError] = useState<string | null>(null);

  // 6. Explicit DEMO action receipt banner
  const [latestReceipt, setLatestReceipt] =
    useState<A5ActionReceiptBanner | null>(null);

  const selectedAudienceOption = useMemo(
    () =>
      ADMIN_BROADCAST_AUDIENCE_OPTIONS.find((a) => a.id === draftAudienceId) ||
      ADMIN_BROADCAST_AUDIENCE_OPTIONS[0],
    [draftAudienceId]
  );

  // Filtered broadcasts
  const filteredBroadcasts = useMemo(() => {
    const q = broadcastSearchQuery.trim().toLowerCase();
    return broadcasts.filter((b) => {
      if (broadcastStatusFilter !== 'all' && b.status !== broadcastStatusFilter) {
        return false;
      }
      if (!q) return true;
      return (
        b.broadcastCode.toLowerCase().includes(q) ||
        b.titleVi.toLowerCase().includes(q) ||
        b.titleEn.toLowerCase().includes(q) ||
        b.audienceLabelVi.toLowerCase().includes(q) ||
        b.audienceLabelEn.toLowerCase().includes(q)
      );
    });
  }, [broadcasts, broadcastSearchQuery, broadcastStatusFilter]);

  // Filtered AI pipelines
  const filteredAiPipelines = useMemo(() => {
    const q = aiSearchQuery.trim().toLowerCase();
    return aiPipelines.filter((p) => {
      if (aiDomainFilter !== 'all' && p.domain !== aiDomainFilter) return false;
      if (aiStateFilter !== 'all' && p.state !== aiStateFilter) return false;
      if (!q) return true;
      return (
        p.pipelineCode.toLowerCase().includes(q) ||
        p.nameVi.toLowerCase().includes(q) ||
        p.nameEn.toLowerCase().includes(q) ||
        p.modelAliasDisplay.toLowerCase().includes(q)
      );
    });
  }, [aiPipelines, aiSearchQuery, aiDomainFilter, aiStateFilter]);

  // Filtered Bank Gateways
  const filteredBankGateways = useMemo(() => {
    const q = bankSearchQuery.trim().toLowerCase();
    return bankGateways.filter((gw) => {
      if (bankStateFilter !== 'all' && gw.state !== bankStateFilter) return false;
      if (bankRailFilter !== 'all' && gw.railType !== bankRailFilter) return false;
      if (!q) return true;
      return (
        gw.gatewayCode.toLowerCase().includes(q) ||
        gw.bankName.toLowerCase().includes(q) ||
        gw.shortName.toLowerCase().includes(q) ||
        gw.railLabelVi.toLowerCase().includes(q) ||
        gw.railLabelEn.toLowerCase().includes(q)
      );
    });
  }, [bankGateways, bankSearchQuery, bankStateFilter, bankRailFilter]);

  // Filtered Audit Log
  const filteredAuditEvents = useMemo(() => {
    const q = auditSearchQuery.trim().toLowerCase();
    return auditEvents.filter((ev) => {
      if (auditModuleFilter !== 'all' && ev.module !== auditModuleFilter) {
        return false;
      }
      if (!q) return true;
      return (
        ev.referenceCode.toLowerCase().includes(q) ||
        ev.actionTitleVi.toLowerCase().includes(q) ||
        ev.actionTitleEn.toLowerCase().includes(q) ||
        ev.targetEntity.toLowerCase().includes(q) ||
        ev.reason.toLowerCase().includes(q)
      );
    });
  }, [auditEvents, auditSearchQuery, auditModuleFilter]);

  // Summary metrics for AI & Bank Gateways
  const aiSummaryMetrics = useMemo(() => {
    let totalReq = 0;
    let passSum = 0;
    let activeCount = 0;
    let pausedCount = 0;

    for (const p of aiPipelines) {
      totalReq += p.syntheticRequests24h;
      passSum += p.guardrailPassRatePct;
      if (p.state === 'paused_demo') pausedCount += 1;
      else activeCount += 1;
    }

    const avgGuardrailPct =
      aiPipelines.length > 0
        ? (passSum / aiPipelines.length).toFixed(2)
        : '100.00';

    return {
      totalReq,
      avgGuardrailPct,
      activeCount,
      pausedCount,
    };
  }, [aiPipelines]);

  const bankSummaryMetrics = useMemo(() => {
    let operationalCount = 0;
    let degradedCount = 0;
    let maintenanceCount = 0;
    let pendingReconcileTotal = 0;

    for (const gw of bankGateways) {
      if (gw.state === 'operational_demo') operationalCount += 1;
      else if (gw.state === 'degraded_latency_demo') degradedCount += 1;
      else maintenanceCount += 1;
      pendingReconcileTotal += gw.pendingReconcileCount;
    }

    return {
      operationalCount,
      degradedCount,
      maintenanceCount,
      pendingReconcileTotal,
    };
  }, [bankGateways]);

  // Save Broadcast Draft (no modal needed for saving a local draft, or records immediately)
  const handleSaveBroadcastDraft = () => {
    const trimmedTitle = draftTitle.trim();
    const trimmedBody = draftBody.trim();

    if (trimmedTitle.length < 5 || trimmedBody.length < 10) {
      setAuthorFormError(
        tr(
          'Vui lòng nhập tiêu đề (tối thiểu 5 ký tự) và nội dung thông báo (tối thiểu 10 ký tự).',
          'Please enter a title (min 5 chars) and broadcast message body (min 10 chars).'
        )
      );
      return;
    }

    const nowStamp = '03/11/2026 · 10:52';
    const codeSuffix = String(Math.floor(200 + Math.random() * 799));
    const brcCode = `BRC-20261103-${codeSuffix}`;
    const refCode = `AUD-SYS-20261103-${codeSuffix}`;
    const catMeta = getBroadcastCategoryMeta(draftCategory, tr);

    const newDraft: AdminBroadcastEntry = {
      id: `bc_${Date.now()}`,
      broadcastCode: brcCode,
      titleVi: trimmedTitle,
      titleEn: trimmedTitle,
      bodyVi: trimmedBody,
      bodyEn: trimmedBody,
      category: draftCategory,
      categoryLabelVi: catMeta.labelVi,
      categoryLabelEn: catMeta.labelEn,
      audienceId: selectedAudienceOption.id,
      audienceLabelVi: selectedAudienceOption.labelVi,
      audienceLabelEn: selectedAudienceOption.labelEn,
      estimatedRecipients: selectedAudienceOption.estimatedRecipients,
      channelVi:
        draftChannel === 'in_app_banner'
          ? 'Biểu ngữ In-App + Hộp thư Thông báo (DEMO)'
          : 'Hộp thư Thông báo In-App (DEMO)',
      channelEn:
        draftChannel === 'in_app_banner'
          ? 'In-App Banner + Notification Inbox (DEMO)'
          : 'In-App Notification Inbox (DEMO)',
      status: 'draft',
      createdAt: nowStamp,
      publishedOrUpdatedAt: nowStamp,
      authorOperator: CURRENT_ADMIN_OPERATOR,
      deliveryNoticeVi:
        'Đã lưu bản nháp nội bộ DEMO — Chưa kích hoạt hiển thị và không gửi thông báo thực.',
      deliveryNoticeEn:
        'Saved as internal DEMO draft — Not yet published and zero real notifications sent.',
    };

    onUpdateBroadcasts((prev) => [newDraft, ...prev]);
    setAuthorFormError(null);

    onAppendAuditEvent({
      id: `gaud_${Date.now()}`,
      referenceCode: refCode,
      timestamp: nowStamp,
      module: 'system',
      moduleLabelVi: 'Hệ thống & Broadcast',
      moduleLabelEn: 'System & Broadcast',
      actionTitleVi: `Lưu bản nháp thông báo Broadcast ${brcCode}`,
      actionTitleEn: `Saved broadcast draft ${brcCode}`,
      targetEntity: `${brcCode} · ${selectedAudienceOption.labelVi} (${selectedAudienceOption.estimatedRecipients.toLocaleString(
        'vi-VN'
      )} người nhận mẫu)`,
      detailVi: `Tiêu đề: ${trimmedTitle}`,
      detailEn: `Title: ${trimmedTitle}`,
      reason: 'Lưu bản nháp thông báo để rà soát nội dung trước khi phát hành DEMO.',
      operator: CURRENT_ADMIN_OPERATOR,
    });

    setLatestReceipt({
      referenceCode: refCode,
      timestamp: nowStamp,
      domainLabelVi: 'Thông báo Hệ thống (Broadcast)',
      domainLabelEn: 'System Broadcast Authoring',
      titleVi: `Đã lưu bản nháp thông báo ${brcCode}`,
      titleEn: `Saved broadcast draft ${brcCode}`,
      summaryVi: `Bản nháp "${trimmedTitle}" nhắm tới nhóm "${selectedAudienceOption.labelVi}" đã được thêm vào danh sách quản lý.`,
      summaryEn: `Draft "${trimmedTitle}" targeting "${selectedAudienceOption.labelEn}" has been saved to the broadcast list.`,
      reason: tr(
        'Lưu bản nháp từ trình soạn thảo Broadcast',
        'Saved draft from Broadcast Authoring studio'
      ),
      demoSafetyNoticeVi:
        'Chế độ DEMO: Không có thông báo đẩy (Push), SMS hay Email thực nào được gửi đi.',
      demoSafetyNoticeEn:
        'DEMO Mode: Zero external Push, SMS, or Email notifications are sent.',
    });
  };

  // Trigger Publish Authored Broadcast with Confirmation
  const handleInitiatePublishAuthoredBroadcast = () => {
    const trimmedTitle = draftTitle.trim();
    const trimmedBody = draftBody.trim();

    if (trimmedTitle.length < 5 || trimmedBody.length < 10) {
      setAuthorFormError(
        tr(
          'Vui lòng nhập tiêu đề (tối thiểu 5 ký tự) và nội dung thông báo (tối thiểu 10 ký tự) trước khi phát hành.',
          'Please enter a title (min 5 chars) and message body (min 10 chars) before publishing.'
        )
      );
      return;
    }

    setAuthorFormError(null);
    openModal({
      kind: 'publish_authored_broadcast',
      titleVi: trimmedTitle,
      titleEn: trimmedTitle,
      bodyVi: trimmedBody,
      bodyEn: trimmedBody,
      category: draftCategory,
      audienceId: draftAudienceId,
      channelVi:
        draftChannel === 'in_app_banner'
          ? 'Biểu ngữ In-App + Hộp thư Thông báo (DEMO)'
          : 'Hộp thư Thông báo In-App (DEMO)',
      channelEn:
        draftChannel === 'in_app_banner'
          ? 'In-App Banner + Notification Inbox (DEMO)'
          : 'In-App Notification Inbox (DEMO)',
    });
  };

  const openModal = (action: PendingA5ConfirmationAction) => {
    setPendingAction(action);
    setModalReasonInput('');
    setIsModalConfirmed(false);
    setModalError(null);
  };

  const closeModal = () => {
    setPendingAction(null);
    setModalReasonInput('');
    setIsModalConfirmed(false);
    setModalError(null);
  };

  useEffect(() => {
    if (!pendingAction) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        closeModal();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [pendingAction]);

  const modalPresetReasons = useMemo(() => {
    if (!pendingAction) return [];
    switch (pendingAction.kind) {
      case 'publish_authored_broadcast':
      case 'publish_existing_broadcast':
        return [
          tr(
            'Đã kiểm duyệt nội dung & tệp đối tượng nhận, phát hành thông báo trên môi trường DEMO',
            'Reviewed message copy and target audience segment; publishing in DEMO state'
          ),
          tr(
            'Thông báo lịch bảo trì cổng ngân hàng định kỳ cho người dùng chủ động kế hoạch giao dịch',
            'Scheduled bank gateway maintenance notice for user awareness'
          ),
        ];
      case 'cancel_existing_broadcast':
        return [
          tr(
            'Đã hoàn tất bảo trì sớm hơn dự kiến, thu hồi thông báo hiển thị trên ứng dụng DEMO',
            'Maintenance completed ahead of schedule; retracting DEMO broadcast banner'
          ),
        ];
      case 'toggle_ai_pipeline_state':
        return [
          tr(
            'Điều chỉnh trạng thái pipeline AI phục vụ kiểm thử tải và độ trễ ca trực (DEMO)',
            'Adjusted synthetic AI pipeline state for shift latency and load verification (DEMO)'
          ),
        ];
      case 'switch_bank_gateway_state':
        return [
          tr(
            'Chuyển chế độ vận hành cổng ngân hàng mẫu để kiểm thử điều hướng dự phòng Napas 247 (DEMO)',
            'Switched synthetic bank gateway mode to verify Napas 247 fallback routing (DEMO)'
          ),
        ];
      case 'verify_system_config':
        return [
          tr(
            'Xác nhận kiểm tra các tham số bất biến sổ cái và chính sách ẩn khóa bí mật đầu ca trực',
            'Verified double-entry invariants and redacted secret hygiene for current shift'
          ),
        ];
    }
  }, [pendingAction, tr]);

  const handleConfirmModalSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!pendingAction) return;

    const trimmedReason = modalReasonInput.trim();
    if (trimmedReason.length < 6) {
      setModalError(
        tr(
          'Vui lòng nhập lý do vận hành cụ thể (tối thiểu 6 ký tự).',
          'Please enter a specific operational reason (at least 6 characters).'
        )
      );
      return;
    }

    if (!isModalConfirmed) {
      setModalError(
        tr(
          'Vui lòng đánh dấu xác nhận chủ đích trước khi thực thi thao tác.',
          'Please check the deliberate confirmation box before executing this action.'
        )
      );
      return;
    }

    const nowStamp = '03/11/2026 · 10:55';
    const codeSuffix = String(Math.floor(1000 + Math.random() * 9000));
    const refCode = `AUD-SYS-20261103-${codeSuffix}`;

    if (pendingAction.kind === 'publish_authored_broadcast') {
      const brcCode = `BRC-20261103-${codeSuffix.slice(0, 3)}`;
      const catMeta = getBroadcastCategoryMeta(pendingAction.category, tr);
      const audOpt =
        ADMIN_BROADCAST_AUDIENCE_OPTIONS.find(
          (a) => a.id === pendingAction.audienceId
        ) || ADMIN_BROADCAST_AUDIENCE_OPTIONS[0];

      const publishedEntry: AdminBroadcastEntry = {
        id: `bc_${Date.now()}`,
        broadcastCode: brcCode,
        titleVi: pendingAction.titleVi,
        titleEn: pendingAction.titleEn,
        bodyVi: pendingAction.bodyVi,
        bodyEn: pendingAction.bodyEn,
        category: pendingAction.category,
        categoryLabelVi: catMeta.labelVi,
        categoryLabelEn: catMeta.labelEn,
        audienceId: audOpt.id,
        audienceLabelVi: audOpt.labelVi,
        audienceLabelEn: audOpt.labelEn,
        estimatedRecipients: audOpt.estimatedRecipients,
        channelVi: pendingAction.channelVi,
        channelEn: pendingAction.channelEn,
        status: 'published_demo',
        createdAt: nowStamp,
        publishedOrUpdatedAt: nowStamp,
        authorOperator: CURRENT_ADMIN_OPERATOR,
        deliveryNoticeVi:
          'Đã phát hành trạng thái DEMO cục bộ — Không gửi thông báo đẩy (Push/SMS/Email) ra bên ngoài.',
        deliveryNoticeEn:
          'Published in local DEMO state — Zero external Push, SMS, or Email messages sent.',
      };

      onUpdateBroadcasts((prev) => [publishedEntry, ...prev]);

      onAppendAuditEvent({
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'system',
        moduleLabelVi: 'Hệ thống & Broadcast',
        moduleLabelEn: 'System & Broadcast',
        actionTitleVi: `Phát hành thông báo Broadcast DEMO ${brcCode}`,
        actionTitleEn: `Published DEMO broadcast ${brcCode}`,
        targetEntity: `${brcCode} · ${audOpt.labelVi} (${audOpt.estimatedRecipients.toLocaleString(
          'vi-VN'
        )} người nhận mẫu)`,
        detailVi: `Tiêu đề: "${pendingAction.titleVi}" · Kênh: ${pendingAction.channelVi}`,
        detailEn: `Title: "${pendingAction.titleEn}" · Channel: ${pendingAction.channelEn}`,
        reason: trimmedReason,
        operator: CURRENT_ADMIN_OPERATOR,
      });

      setLatestReceipt({
        referenceCode: refCode,
        timestamp: nowStamp,
        domainLabelVi: 'Thông báo Hệ thống (Broadcast)',
        domainLabelEn: 'System Broadcast Authoring',
        titleVi: `Đã phát hành thông báo Broadcast DEMO (${brcCode})`,
        titleEn: `Published DEMO Broadcast (${brcCode})`,
        summaryVi: `Thông báo "${pendingAction.titleVi}" đã chuyển sang trạng thái "Đã phát hành (DEMO)" cho nhóm "${audOpt.labelVi}" (${audOpt.estimatedRecipients.toLocaleString(
          'vi-VN'
        )} người nhận mẫu).`,
        summaryEn: `Broadcast "${pendingAction.titleEn}" is now "Published (DEMO)" for audience "${audOpt.labelEn}" (${audOpt.estimatedRecipients.toLocaleString(
          'en-US'
        )} synthetic recipients).`,
        reason: trimmedReason,
        demoSafetyNoticeVi:
          'Cam kết DEMO: Chỉ cập nhật trạng thái thông báo mẫu trong phiên, không gọi dịch vụ gửi Push/SMS/Email bên ngoài.',
        demoSafetyNoticeEn:
          'DEMO Guarantee: Updates in-memory synthetic broadcast state only; zero external Push/SMS/Email provider calls.',
      });

      closeModal();
      return;
    }

    if (pendingAction.kind === 'publish_existing_broadcast') {
      onUpdateBroadcasts((prev) =>
        prev.map((b) =>
          b.id === pendingAction.broadcastId
            ? {
                ...b,
                status: 'published_demo',
                publishedOrUpdatedAt: nowStamp,
                deliveryNoticeVi:
                  'Đã kích hoạt phát hành DEMO cục bộ — Không gửi thông báo thực.',
                deliveryNoticeEn:
                  'Activated in local DEMO state — No real external notifications sent.',
              }
            : b
        )
      );

      onAppendAuditEvent({
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'system',
        moduleLabelVi: 'Hệ thống & Broadcast',
        moduleLabelEn: 'System & Broadcast',
        actionTitleVi: `Kích hoạt phát hành thông báo ${pendingAction.broadcastCode}`,
        actionTitleEn: `Published existing broadcast ${pendingAction.broadcastCode}`,
        targetEntity: `${pendingAction.broadcastCode} · ${pendingAction.audienceLabelVi}`,
        detailVi: `Chuyển trạng thái sang Đã phát hành (DEMO) cho ${pendingAction.estimatedRecipients.toLocaleString(
          'vi-VN'
        )} người nhận mẫu`,
        detailEn: `Transitioned to Published (DEMO) for ${pendingAction.estimatedRecipients.toLocaleString(
          'en-US'
        )} synthetic recipients`,
        reason: trimmedReason,
        operator: CURRENT_ADMIN_OPERATOR,
      });

      setLatestReceipt({
        referenceCode: refCode,
        timestamp: nowStamp,
        domainLabelVi: 'Thông báo Hệ thống (Broadcast)',
        domainLabelEn: 'System Broadcast',
        titleVi: `Đã kích hoạt phát hành ${pendingAction.broadcastCode} (DEMO)`,
        titleEn: `Activated DEMO Broadcast ${pendingAction.broadcastCode}`,
        summaryVi: `Thông báo "${pendingAction.titleVi}" đã được chuyển từ Bản nháp/Lên lịch sang "Đã phát hành (DEMO)".`,
        summaryEn: `Broadcast "${pendingAction.titleEn}" transitioned to "Published (DEMO)".`,
        reason: trimmedReason,
        demoSafetyNoticeVi:
          'Chế độ DEMO: Không gửi tin nhắn hoặc thông báo đẩy ra bên ngoài.',
        demoSafetyNoticeEn:
          'DEMO Mode: Zero external push or messaging provider calls.',
      });

      closeModal();
      return;
    }

    if (pendingAction.kind === 'cancel_existing_broadcast') {
      onUpdateBroadcasts((prev) =>
        prev.map((b) =>
          b.id === pendingAction.broadcastId
            ? {
                ...b,
                status: 'cancelled_demo',
                publishedOrUpdatedAt: nowStamp,
                deliveryNoticeVi: `Đã thu hồi hiển thị DEMO lúc ${nowStamp}.`,
                deliveryNoticeEn: `Retracted from DEMO display at ${nowStamp}.`,
              }
            : b
        )
      );

      onAppendAuditEvent({
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'system',
        moduleLabelVi: 'Hệ thống & Broadcast',
        moduleLabelEn: 'System & Broadcast',
        actionTitleVi: `Thu hồi thông báo Broadcast ${pendingAction.broadcastCode}`,
        actionTitleEn: `Cancelled DEMO broadcast ${pendingAction.broadcastCode}`,
        targetEntity: `${pendingAction.broadcastCode} · ${pendingAction.titleVi}`,
        detailVi: 'Chuyển trạng thái sang Đã thu hồi (DEMO)',
        detailEn: 'Transitioned broadcast status to Cancelled (DEMO)',
        reason: trimmedReason,
        operator: CURRENT_ADMIN_OPERATOR,
      });

      setLatestReceipt({
        referenceCode: refCode,
        timestamp: nowStamp,
        domainLabelVi: 'Thông báo Hệ thống (Broadcast)',
        domainLabelEn: 'System Broadcast',
        titleVi: `Đã thu hồi thông báo ${pendingAction.broadcastCode} (DEMO)`,
        titleEn: `Cancelled DEMO Broadcast ${pendingAction.broadcastCode}`,
        summaryVi: `Thông báo "${pendingAction.titleVi}" đã chuyển sang trạng thái "Đã thu hồi (DEMO)".`,
        summaryEn: `Broadcast "${pendingAction.titleEn}" is now marked "Cancelled (DEMO)".`,
        reason: trimmedReason,
        demoSafetyNoticeVi:
          'Đã lưu sự kiện thu hồi vào Nhật ký Kiểm toán nội bộ.',
        demoSafetyNoticeEn:
          'Recorded broadcast cancellation in the internal Audit Log.',
      });

      closeModal();
      return;
    }

    if (pendingAction.kind === 'toggle_ai_pipeline_state') {
      onUpdateAiPipelines((prev) =>
        prev.map((p) =>
          p.id === pendingAction.pipelineId
            ? {
                ...p,
                state: pendingAction.targetState,
                lastEvaluatedAt: nowStamp,
                operatorNoteVi: `Cập nhật lúc ${nowStamp}: ${trimmedReason}`,
                operatorNoteEn: `Updated at ${nowStamp}: ${trimmedReason}`,
              }
            : p
        )
      );

      const nextStateLabelVi = getAiPipelineStateMeta(
        pendingAction.targetState,
        (v) => v
      ).label;
      const nextStateLabelEn = getAiPipelineStateMeta(
        pendingAction.targetState,
        (_v, e) => e
      ).label;

      onAppendAuditEvent({
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'system',
        moduleLabelVi: 'Giám sát Vận hành AI',
        moduleLabelEn: 'AI Operations Monitoring',
        actionTitleVi: `Cập nhật trạng thái Pipeline AI ${pendingAction.pipelineCode} → ${nextStateLabelVi}`,
        actionTitleEn: `Updated AI Pipeline ${pendingAction.pipelineCode} → ${nextStateLabelEn}`,
        targetEntity: `${pendingAction.pipelineCode} · ${pendingAction.nameVi}`,
        detailVi: `Trạng thái mới: ${nextStateLabelVi} (Không gọi API nhà cung cấp AI thực)`,
        detailEn: `New state: ${nextStateLabelEn} (Zero external AI provider API calls)`,
        reason: trimmedReason,
        operator: CURRENT_ADMIN_OPERATOR,
      });

      setLatestReceipt({
        referenceCode: refCode,
        timestamp: nowStamp,
        domainLabelVi: 'Giám sát Vận hành AI (DEMO)',
        domainLabelEn: 'AI Operations Monitoring (DEMO)',
        titleVi: `Đã cập nhật Pipeline ${pendingAction.pipelineCode} sang "${nextStateLabelVi}"`,
        titleEn: `Updated Pipeline ${pendingAction.pipelineCode} to "${nextStateLabelEn}"`,
        summaryVi: `Pipeline "${pendingAction.nameVi}" đã chuyển trạng thái trên bảng giám sát DEMO và ghi nhận vào Nhật ký Kiểm toán.`,
        summaryEn: `Pipeline "${pendingAction.nameEn}" state updated in DEMO monitoring and logged in the Audit Trail.`,
        reason: trimmedReason,
        demoSafetyNoticeVi:
          'Chế độ DEMO: Không phát sinh lệnh gọi mô hình LLM hay dịch vụ AI bên ngoài.',
        demoSafetyNoticeEn:
          'DEMO Mode: Zero external LLM or AI provider calls are executed.',
      });

      closeModal();
      return;
    }

    if (pendingAction.kind === 'switch_bank_gateway_state') {
      onUpdateBankGateways((prev) =>
        prev.map((gw) =>
          gw.id === pendingAction.gatewayId
            ? {
                ...gw,
                state: pendingAction.targetState,
                lastHeartbeatAt: nowStamp,
                statusSummaryVi: `Đã chuyển sang "${
                  getBankGatewayStateMeta(pendingAction.targetState, (v) => v)
                    .label
                }" lúc ${nowStamp} — Lý do: ${trimmedReason}`,
                statusSummaryEn: `Switched to "${
                  getBankGatewayStateMeta(
                    pendingAction.targetState,
                    (_v, e) => e
                  ).label
                }" at ${nowStamp} — Reason: ${trimmedReason}`,
              }
            : gw
        )
      );

      const nextGwLabelVi = getBankGatewayStateMeta(
        pendingAction.targetState,
        (v) => v
      ).label;
      const nextGwLabelEn = getBankGatewayStateMeta(
        pendingAction.targetState,
        (_v, e) => e
      ).label;

      onAppendAuditEvent({
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'system',
        moduleLabelVi: 'Giám sát Cổng Ngân hàng',
        moduleLabelEn: 'Bank Gateway Monitoring',
        actionTitleVi: `Chuyển chế độ cổng ${pendingAction.gatewayCode} → ${nextGwLabelVi}`,
        actionTitleEn: `Switched gateway ${pendingAction.gatewayCode} → ${nextGwLabelEn}`,
        targetEntity: `${pendingAction.gatewayCode} · ${pendingAction.bankName}`,
        detailVi: `Trạng thái cổng mẫu: ${nextGwLabelVi} (Không kết nối ngân hàng thực)`,
        detailEn: `Synthetic gateway state: ${nextGwLabelEn} (Zero real bank connection)`,
        reason: trimmedReason,
        operator: CURRENT_ADMIN_OPERATOR,
      });

      setLatestReceipt({
        referenceCode: refCode,
        timestamp: nowStamp,
        domainLabelVi: 'Giám sát Cổng Ngân hàng (DEMO)',
        domainLabelEn: 'Bank Gateway Monitoring (DEMO)',
        titleVi: `Đã chuyển cổng ${pendingAction.gatewayCode} sang "${nextGwLabelVi}"`,
        titleEn: `Switched gateway ${pendingAction.gatewayCode} to "${nextGwLabelEn}"`,
        summaryVi: `Cổng "${pendingAction.bankName}" đã được cập nhật trạng thái mô phỏng DEMO và lưu vào Nhật ký Kiểm toán.`,
        summaryEn: `Gateway "${pendingAction.bankName}" synthetic state updated and recorded in the Audit Trail.`,
        reason: trimmedReason,
        demoSafetyNoticeVi:
          'Chế độ DEMO: Không tác động đường truyền Napas 247 hay cổng thanh toán ngân hàng thực.',
        demoSafetyNoticeEn:
          'DEMO Mode: Does not affect real Napas 247 or external banking payment rails.',
      });

      closeModal();
      return;
    }

    // verify_system_config
    onUpdateConfigParams((prev) =>
      prev.map((c) => ({
        ...c,
        lastVerifiedAt: nowStamp,
      }))
    );

    onAppendAuditEvent({
      id: `gaud_${Date.now()}`,
      referenceCode: refCode,
      timestamp: nowStamp,
      module: 'system',
      moduleLabelVi: 'Cấu hình Hệ thống',
      moduleLabelEn: 'System Config',
      actionTitleVi: `Xác nhận kiểm tra ${pendingAction.paramCount} tham số cấu hình & bảo mật khóa`,
      actionTitleEn: `Verified ${pendingAction.paramCount} system config & secret hygiene parameters`,
      targetEntity: 'SYSTEM_CONFIG_V2 · Redacted Secrets Verified',
      detailVi: 'Toàn bộ khóa tích hợp được ẩn an toàn ([REDACTED_SECRET])',
      detailEn: 'All integration secrets strictly redacted ([REDACTED_SECRET])',
      reason: trimmedReason,
      operator: CURRENT_ADMIN_OPERATOR,
    });

    setLatestReceipt({
      referenceCode: refCode,
      timestamp: nowStamp,
      domainLabelVi: 'Cấu hình Hệ thống (DEMO)',
      domainLabelEn: 'System Configuration (DEMO)',
      titleVi: 'Đã xác nhận kiểm tra Cấu hình Hệ thống & Bảo mật Khóa',
      titleEn: 'Verified System Configuration & Secret Hygiene',
      summaryVi: `Đã cập nhật mốc kiểm tra (${nowStamp}) cho ${pendingAction.paramCount} tham số hệ thống.`,
      summaryEn: `Updated verification timestamp (${nowStamp}) across ${pendingAction.paramCount} system parameters.`,
      reason: trimmedReason,
      demoSafetyNoticeVi:
        'Chính sách Zero-Secret: Không hiển thị bất kỳ khóa bí mật hay API key nào.',
      demoSafetyNoticeEn:
        'Zero-Secret Policy: No API keys or signing secrets are ever exposed.',
    });

    closeModal();
  };

  const showBroadcast =
    activeSubTab === 'all' || activeSubTab === 'broadcast';
  const showAiOps = activeSubTab === 'all' || activeSubTab === 'ai_ops';
  const showBankGateways =
    activeSubTab === 'all' || activeSubTab === 'bank_gateways';
  const showConfigAudit =
    activeSubTab === 'all' || activeSubTab === 'config_audit';

  return (
    <div
      data-testid="sw-admin-screen-system"
      className="space-y-6 min-w-0"
    >
      {/* Compact Sub-navigation & Secondary Icon Quick-Action Bar */}
      <div className="sw-card p-3 sm:p-3.5 min-w-0 max-w-full flex items-center justify-between gap-2.5">
        <div
          role="tablist"
          aria-label={tr(
            'Phân hệ Thông báo, Giám sát AI, Ngân hàng và Nhật ký quản trị',
            'Notifications, AI Operations, Bank Gateways, and Admin Log tabs'
          )}
          data-testid="sw-admin-system-subtabs"
          className="flex flex-wrap items-center gap-1.5 min-w-0 flex-1 overflow-x-auto max-w-full"
        >
          <button
            type="button"
            role="tab"
            aria-selected={activeSubTab === 'broadcast'}
            data-testid="sw-admin-system-tab-broadcast"
            onClick={() => setActiveSubTab('broadcast')}
            className={`sw-focus-ring flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-xl px-2.5 py-1 text-left text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
              activeSubTab === 'broadcast'
                ? 'bg-[var(--ink)] text-[var(--bg)] shadow-xs'
                : 'bg-[var(--sw-surface-muted)] text-[var(--ink2)] hover:text-[var(--ink)]'
            }`}
          >
            <Megaphone aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
            <span>{tr('Thông báo', 'Notifications')}</span>
            <span className="text-[0.72rem] font-extrabold tabular-nums opacity-85">
              {broadcasts.length}
            </span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeSubTab === 'ai_ops'}
            data-testid="sw-admin-system-tab-ai"
            onClick={() => setActiveSubTab('ai_ops')}
            className={`sw-focus-ring flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-xl px-2.5 py-1 text-left text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
              activeSubTab === 'ai_ops'
                ? 'bg-[var(--ink)] text-[var(--bg)] shadow-xs'
                : 'bg-[var(--sw-surface-muted)] text-[var(--ink2)] hover:text-[var(--ink)]'
            }`}
          >
            <Cpu aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
            <span>{tr('Giám sát AI', 'AI Monitoring')}</span>
            <span className="text-[0.72rem] font-extrabold tabular-nums opacity-85">
              {aiPipelines.length}
            </span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeSubTab === 'bank_gateways'}
            data-testid="sw-admin-system-tab-bank"
            onClick={() => setActiveSubTab('bank_gateways')}
            className={`sw-focus-ring flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-xl px-2.5 py-1 text-left text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
              activeSubTab === 'bank_gateways'
                ? 'bg-[var(--ink)] text-[var(--bg)] shadow-xs'
                : 'bg-[var(--sw-surface-muted)] text-[var(--ink2)] hover:text-[var(--ink)]'
            }`}
          >
            <Building2 aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
            <span>{tr('Ngân hàng', 'Bank Gateways')}</span>
            <span className="text-[0.72rem] font-extrabold tabular-nums opacity-85">
              {bankGateways.length}
            </span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeSubTab === 'config_audit'}
            data-testid="sw-admin-system-tab-config-audit"
            onClick={() => setActiveSubTab('config_audit')}
            className={`sw-focus-ring flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-xl px-2.5 py-1 text-left text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
              activeSubTab === 'config_audit'
                ? 'bg-[var(--ink)] text-[var(--bg)] shadow-xs'
                : 'bg-[var(--sw-surface-muted)] text-[var(--ink2)] hover:text-[var(--ink)]'
            }`}
          >
            <Settings aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
            <span>{tr('Nhật ký quản trị', 'Admin Audit & Config')}</span>
            <span className="text-[0.72rem] font-extrabold tabular-nums opacity-85">
              {auditEvents.length}
            </span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeSubTab === 'all'}
            data-testid="sw-admin-system-tab-all"
            onClick={() => setActiveSubTab('all')}
            className={`sw-focus-ring flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-xl px-2.5 py-1 text-left text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
              activeSubTab === 'all'
                ? 'bg-[var(--ink)] text-[var(--bg)] shadow-xs'
                : 'bg-[var(--sw-surface-muted)] text-[var(--ink2)] hover:text-[var(--ink)]'
            }`}
          >
            <span>{tr('Xem tất cả', 'View All')}</span>
          </button>
        </div>

        <div className="relative group/syssim shrink-0">
          <button
            type="button"
            data-testid="sw-admin-system-simulate-error-btn"
            aria-pressed={simulatedSectionError}
            aria-label={
              simulatedSectionError
                ? tr('Khôi phục bảng hệ thống', 'Restore system console')
                : tr('Mô phỏng lỗi giám sát', 'Simulate telemetry error')
            }
            title={
              simulatedSectionError
                ? tr('Khôi phục bảng hệ thống', 'Restore system console')
                : tr('Mô phỏng lỗi giám sát', 'Simulate telemetry error')
            }
            onClick={() => setSimulatedSectionError((prev) => !prev)}
            className="sw-focus-ring sw-btn ghost sm !h-9 !w-9 !min-h-[36px] !min-w-[36px] !p-0 justify-center cursor-pointer"
          >
            <AlertTriangle aria-hidden="true" className="h-4 w-4 shrink-0" />
            <span className="sr-only">
              {simulatedSectionError
                ? tr('Khôi phục bảng hệ thống', 'Restore system console')
                : tr('Mô phỏng lỗi giám sát', 'Simulate telemetry error')}
            </span>
          </button>
          <span
            role="tooltip"
            className="pointer-events-none invisible opacity-0 group-hover/syssim:visible group-hover/syssim:opacity-100 group-focus-within/syssim:visible group-focus-within/syssim:opacity-100 transition-opacity duration-150 absolute right-0 top-full mt-1.5 z-40 whitespace-nowrap rounded-lg border border-[var(--stroke)] bg-[var(--ink)] px-2.5 py-1 text-[0.72rem] font-bold text-[var(--bg)] shadow-lg"
          >
            {simulatedSectionError
              ? tr('Khôi phục bảng hệ thống', 'Restore system console')
              : tr('Mô phỏng lỗi giám sát', 'Simulate telemetry error')}
          </span>
        </div>
      </div>

      {/* Explicit DEMO Action Receipt Banner */}
      {latestReceipt && (
        <div
          role="status"
          aria-live="polite"
          data-testid="sw-admin-a5-action-result"
          className="sw-card p-4 sm:p-5 border border-[var(--sw-em)] bg-[var(--sw-em-s)]/60 space-y-2"
        >
          <div className="flex items-start justify-between gap-3">
            <div className="flex items-center gap-2 text-[0.8125rem] font-extrabold text-[var(--sw-em-d)]">
              <FileCheck2 aria-hidden="true" className="h-4 w-4 shrink-0" />
              <span>
                {tr(latestReceipt.domainLabelVi, latestReceipt.domainLabelEn)} ·{' '}
                <span className="tabular-nums">{latestReceipt.referenceCode}</span>
              </span>
            </div>
            <button
              type="button"
              data-testid="sw-admin-dismiss-a5-result-btn"
              onClick={() => setLatestReceipt(null)}
              aria-label={tr('Đóng biên nhận kết quả', 'Dismiss receipt')}
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
              {tr('Lý do vận hành:', 'Operational reason:')}
            </span>{' '}
            {latestReceipt.reason}
          </div>
          <div className="pt-1 text-[0.75rem] font-semibold text-[var(--sw-em-d)]">
            {tr(
              latestReceipt.demoSafetyNoticeVi,
              latestReceipt.demoSafetyNoticeEn
            )}
          </div>
        </div>
      )}

      {simulatedSectionError ? (
        <div
          role="alert"
          data-testid="sw-admin-system-error-state"
          className="sw-card p-6 text-center space-y-3"
        >
          <div className="mx-auto flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--rose-s)] text-[var(--rose)]">
            <AlertTriangle aria-hidden="true" className="h-5 w-5" />
          </div>
          <h3 className="font-display text-[1rem] font-extrabold text-[var(--ink)]">
            {tr(
              'Không thể tải luồng giám sát Hệ thống, AI & Cổng Ngân hàng (Mô phỏng lỗi)',
              'Unable to load System, AI & Bank Gateway telemetry (Simulated error)'
            )}
          </h3>
          <p className="text-[0.84375rem] text-[var(--ink2)] max-w-md mx-auto">
            {tr(
              'Bộ thu thập trạng thái mẫu đang tạm ngưng để kiểm thử giao diện chịu lỗi.',
              'Synthetic telemetry reader is paused to verify error state resilience.'
            )}
          </p>
          <div>
            <button
              type="button"
              data-testid="sw-admin-system-error-retry-btn"
              onClick={() => setSimulatedSectionError(false)}
              className="sw-focus-ring sw-btn sw-btn-pri sm"
            >
              <RefreshCw aria-hidden="true" className="h-4 w-4" />
              <span>{tr('Thử tải lại', 'Retry loading')}</span>
            </button>
          </div>
        </div>
      ) : (
        <div className="space-y-8">
          {/* =================================================================
              MODULE 1: BROADCAST AUTHORING (DRAFT / PREVIEW / AUDIENCE / CONFIRM)
              ================================================================= */}
          {showBroadcast && (
            <section
              aria-labelledby="sw-admin-broadcast-heading"
              data-testid="sw-admin-broadcast-section"
              className="space-y-4 min-w-0"
            >
              <div className="grid grid-cols-1 xl:grid-cols-12 gap-6 items-start min-w-0">
                {/* Left 7 cols: Broadcast Authoring Studio */}
                <div
                  data-testid="sw-admin-broadcast-author-form"
                  className="xl:col-span-7 min-w-0 sw-card p-5 space-y-4"
                >
                  <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 border-b border-[var(--line)] pb-3">
                    <div>
                      <div className="flex items-center gap-2 text-[0.75rem] font-bold text-[var(--sw-em-d)]">
                        <Megaphone aria-hidden="true" className="h-4 w-4" />
                        <span>
                          {tr(
                            'Soạn thảo Thông báo Hệ thống (DEMO)',
                            'System Notification Authoring (DEMO)'
                          )}
                        </span>
                      </div>
                      <h3
                        id="sw-admin-broadcast-heading"
                        className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]"
                      >
                        {tr(
                          'Soạn Bản nháp, Chọn Đối tượng & Xác nhận Phát hành',
                          'Draft, Select Audience & Confirm DEMO Notification'
                        )}
                      </h3>
                    </div>
                    <span className="text-[0.75rem] font-semibold text-[var(--ink2)]">
                      {tr(
                        'Không gửi Push/SMS/Email thực',
                        'No real Push/SMS/Email sent'
                      )}
                    </span>
                  </div>

                  {/* Quick Authoring Templates */}
                  <div className="space-y-1.5">
                    <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                      {tr(
                        'Chèn mẫu thông báo vận hành nhanh:',
                        'Quick broadcast templates:'
                      )}
                    </div>
                    <div className="flex flex-wrap gap-1.5">
                      {ADMIN_BROADCAST_TEMPLATES.map((tpl) => (
                        <button
                          key={tpl.id}
                          type="button"
                          data-testid={`sw-admin-broadcast-template-${tpl.id}`}
                          onClick={() => {
                            setDraftTitle(
                              language === 'en' ? tpl.titleEn : tpl.titleVi
                            );
                            setDraftBody(
                              language === 'en' ? tpl.bodyEn : tpl.bodyVi
                            );
                            setDraftCategory(tpl.category);
                            setDraftAudienceId(tpl.audienceId);
                            setAuthorFormError(null);
                          }}
                          className="sw-focus-ring rounded-lg border border-[var(--line)] bg-[var(--sw-surface-muted)] px-2.5 py-1.5 text-left text-[0.75rem] font-semibold text-[var(--ink2)] hover:text-[var(--ink)] cursor-pointer"
                        >
                          {tr(tpl.labelVi, tpl.labelEn)}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Form Fields */}
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <div>
                      <label
                        htmlFor="sw-admin-broadcast-category"
                        className="block text-[0.75rem] font-bold text-[var(--ink2)] mb-1"
                      >
                        {tr('Phân loại thông báo', 'Broadcast Category')}
                      </label>
                      <select
                        id="sw-admin-broadcast-category"
                        data-testid="sw-admin-broadcast-category-select"
                        value={draftCategory}
                        onChange={(e) =>
                          setDraftCategory(
                            e.target.value as AdminBroadcastCategory
                          )
                        }
                        className="sw-in w-full !py-2 text-[0.8125rem]"
                      >
                        <option value="system_maintenance">
                          {tr('Bảo trì & Vận hành', 'Maintenance & Ops')}
                        </option>
                        <option value="security_advisory">
                          {tr('Cảnh báo Bảo mật', 'Security Advisory')}
                        </option>
                        <option value="fund_cycle_reminder">
                          {tr('Nhắc lịch Quỹ nhóm', 'Group Fund Reminder')}
                        </option>
                        <option value="feature_update">
                          {tr('Cập nhật Tính năng', 'Feature Update')}
                        </option>
                      </select>
                    </div>

                    <div>
                      <label
                        htmlFor="sw-admin-broadcast-audience"
                        className="block text-[0.75rem] font-bold text-[var(--ink2)] mb-1"
                      >
                        {tr('Tệp đối tượng nhận (Audience)', 'Target Audience')}
                      </label>
                      <select
                        id="sw-admin-broadcast-audience"
                        data-testid="sw-admin-broadcast-audience-select"
                        value={draftAudienceId}
                        onChange={(e) =>
                          setDraftAudienceId(
                            e.target.value as AdminBroadcastAudienceId
                          )
                        }
                        className="sw-in w-full !py-2 text-[0.8125rem]"
                      >
                        {ADMIN_BROADCAST_AUDIENCE_OPTIONS.map((opt) => (
                          <option key={opt.id} value={opt.id}>
                            {tr(opt.labelVi, opt.labelEn)} (
                            {opt.estimatedRecipients.toLocaleString(
                              language === 'en' ? 'en-US' : 'vi-VN'
                            )}
                            )
                          </option>
                        ))}
                      </select>
                    </div>
                  </div>

                  {/* Audience description & channel */}
                  <div
                    data-testid="sw-admin-broadcast-audience-summary"
                    className="rounded-xl bg-[var(--sw-surface-muted)]/60 p-3 text-[0.75rem] flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2"
                  >
                    <div className="text-[var(--ink2)]">
                      <strong className="font-bold text-[var(--ink)]">
                        {tr('Phạm vi đối tượng:', 'Audience scope:')}
                      </strong>{' '}
                      {tr(
                        selectedAudienceOption.descriptionVi,
                        selectedAudienceOption.descriptionEn
                      )}
                    </div>
                    <div className="font-extrabold text-[var(--sw-em-d)] tabular-nums shrink-0">
                      ~
                      {selectedAudienceOption.estimatedRecipients.toLocaleString(
                        language === 'en' ? 'en-US' : 'vi-VN'
                      )}{' '}
                      {tr('người nhận (DEMO)', 'recipients (DEMO)')}
                    </div>
                  </div>

                  <div className="space-y-1">
                    <label
                      htmlFor="sw-admin-broadcast-title"
                      className="block text-[0.8125rem] font-bold text-[var(--ink)]"
                    >
                      {tr('Tiêu đề thông báo *', 'Broadcast Title *')}
                    </label>
                    <input
                      id="sw-admin-broadcast-title"
                      type="text"
                      data-testid="sw-admin-broadcast-title-input"
                      value={draftTitle}
                      onChange={(e) => {
                        setDraftTitle(e.target.value);
                        if (authorFormError) setAuthorFormError(null);
                      }}
                      placeholder={tr(
                        'Nhập tiêu đề thông báo ngắn gọn, rõ ràng...',
                        'Enter a concise broadcast title...'
                      )}
                      className="sw-in w-full text-[0.84375rem]"
                    />
                  </div>

                  <div className="space-y-1">
                    <label
                      htmlFor="sw-admin-broadcast-body"
                      className="block text-[0.8125rem] font-bold text-[var(--ink)]"
                    >
                      {tr('Nội dung chi tiết *', 'Broadcast Message Body *')}
                    </label>
                    <textarea
                      id="sw-admin-broadcast-body"
                      data-testid="sw-admin-broadcast-body-input"
                      rows={3}
                      value={draftBody}
                      onChange={(e) => {
                        setDraftBody(e.target.value);
                        if (authorFormError) setAuthorFormError(null);
                      }}
                      placeholder={tr(
                        'Nhập nội dung thông báo hiển thị cho người dùng...',
                        'Enter the broadcast message body...'
                      )}
                      className="sw-in w-full text-[0.84375rem]"
                    />
                  </div>

                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-3 text-[0.75rem]">
                      <span className="font-bold text-[var(--ink2)]">
                        {tr('Vị trí hiển thị mẫu:', 'Display surface:')}
                      </span>
                      <label className="inline-flex items-center gap-1.5 cursor-pointer">
                        <input
                          type="radio"
                          name="broadcast-channel"
                          checked={draftChannel === 'in_app_banner'}
                          onChange={() => setDraftChannel('in_app_banner')}
                          className="accent-[#059669]"
                        />
                        <span>
                          {tr('Biểu ngữ + Hộp thư', 'Banner + Inbox')}
                        </span>
                      </label>
                      <label className="inline-flex items-center gap-1.5 cursor-pointer">
                        <input
                          type="radio"
                          name="broadcast-channel"
                          checked={draftChannel === 'inbox_only'}
                          onChange={() => setDraftChannel('inbox_only')}
                          className="accent-[#059669]"
                        />
                        <span>{tr('Chỉ Hộp thư', 'Inbox Only')}</span>
                      </label>
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                      <button
                        type="button"
                        data-testid="sw-admin-broadcast-save-draft-btn"
                        onClick={handleSaveBroadcastDraft}
                        className="sw-focus-ring sw-btn ghost sm whitespace-nowrap"
                      >
                        <Save aria-hidden="true" className="h-4 w-4" />
                        <span>
                          {tr('Lưu bản nháp (Draft)', 'Save as Draft')}
                        </span>
                      </button>

                      <button
                        type="button"
                        data-testid="sw-admin-broadcast-publish-btn"
                        onClick={handleInitiatePublishAuthoredBroadcast}
                        className="sw-focus-ring sw-btn sw-btn-pri sm whitespace-nowrap"
                      >
                        <Send aria-hidden="true" className="h-4 w-4" />
                        <span>
                          {tr(
                            'Xác nhận phát hành (DEMO)',
                            'Confirm & Publish (DEMO)'
                          )}
                        </span>
                      </button>
                    </div>
                  </div>

                  {authorFormError && (
                    <div
                      role="alert"
                      data-testid="sw-admin-broadcast-author-error"
                      className="rounded-xl bg-[var(--rose-s)] px-3 py-2 text-[0.8125rem] font-bold text-[var(--rose)]"
                    >
                      {authorFormError}
                    </div>
                  )}
                </div>

                {/* Right 5 cols: Live Broadcast Preview Box */}
                <div
                  data-testid="sw-admin-broadcast-preview-box"
                  className="xl:col-span-5 min-w-0 sw-card p-5 space-y-4"
                >
                  <div className="flex items-center justify-between gap-2 border-b border-[var(--line)] pb-3">
                    <div className="flex items-center gap-2 text-[0.75rem] font-bold text-[var(--sw-em-d)]">
                      <Eye aria-hidden="true" className="h-4 w-4" />
                      <span>
                        {tr(
                          'Xem trước Hiển thị trên Ứng dụng (Live Preview)',
                          'Live In-App Mobile Preview'
                        )}
                      </span>
                    </div>
                    <span className="text-[0.6875rem] font-bold text-[var(--ink3)]">
                      {tr('Chế độ xem trước DEMO', 'DEMO Preview Only')}
                    </span>
                  </div>

                  {/* Simulated Mobile Notification Card */}
                  <div className="rounded-2xl border border-[var(--sw-em)]/40 bg-[var(--sw-surface-muted)]/75 p-4 space-y-2.5">
                    <div className="flex items-center justify-between gap-2 text-[0.75rem]">
                      <span
                        className={`font-extrabold ${
                          getBroadcastCategoryMeta(draftCategory, tr).textClass
                        }`}
                      >
                        {tr(
                          getBroadcastCategoryMeta(draftCategory, tr).labelVi,
                          getBroadcastCategoryMeta(draftCategory, tr).labelEn
                        )}
                      </span>
                      <span className="text-[var(--ink3)] tabular-nums">
                        {tr('Vừa xong · SpendWise', 'Just now · SpendWise')}
                      </span>
                    </div>

                    <div
                      data-testid="sw-admin-broadcast-preview-title"
                      className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]"
                    >
                      {draftTitle.trim() ||
                        tr(
                          '(Chưa nhập tiêu đề thông báo)',
                          '(Untitled broadcast)'
                        )}
                    </div>

                    <p
                      data-testid="sw-admin-broadcast-preview-body"
                      className="text-[0.8125rem] text-[var(--ink2)] leading-relaxed"
                    >
                      {draftBody.trim() ||
                        tr(
                          '(Chưa nhập nội dung thông báo)',
                          '(No message body entered)'
                        )}
                    </p>

                    <div className="pt-2 border-t border-[var(--line)] flex flex-wrap items-center justify-between gap-2 text-[0.6875rem] text-[var(--ink3)]">
                      <span>
                        {tr('Đối tượng:', 'Audience:')}{' '}
                        <strong className="text-[var(--ink2)]">
                          {tr(
                            selectedAudienceOption.labelVi,
                            selectedAudienceOption.labelEn
                          )}
                        </strong>
                      </span>
                      <span className="tabular-nums font-bold text-[var(--sw-em-d)]">
                        {selectedAudienceOption.estimatedRecipients.toLocaleString(
                          language === 'en' ? 'en-US' : 'vi-VN'
                        )}{' '}
                        {tr('tài khoản mẫu', 'synthetic users')}
                      </span>
                    </div>
                  </div>

                  <div className="space-y-1.5 text-[0.75rem] text-[var(--ink2)]">
                    <div className="sw-kv">
                      <span>{tr('Kênh phân phối', 'Delivery Channel')}</span>
                      <span className="font-bold text-[var(--ink)]">
                        {draftChannel === 'in_app_banner'
                          ? tr(
                              'Biểu ngữ In-App + Hộp thư (DEMO)',
                              'In-App Banner + Inbox (DEMO)'
                            )
                          : tr(
                              'Chỉ Hộp thư In-App (DEMO)',
                              'In-App Inbox Only (DEMO)'
                            )}
                      </span>
                    </div>
                    <div className="sw-kv">
                      <span>
                        {tr('Kết nối dịch vụ gửi tin', 'External Push/SMS API')}
                      </span>
                      <span className="font-bold text-[var(--sw-em-d)]">
                        {tr(
                          'Đã ngắt (100% Giả lập cục bộ)',
                          'Disabled (100% Local Simulation)'
                        )}
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              {/* Broadcast History & Queue Table */}
              <div
                data-testid="sw-admin-broadcast-list"
                className="sw-card p-4 sm:p-5 space-y-4 min-w-0 overflow-hidden"
              >
                <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
                  <div>
                    <h4 className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                      {tr(
                        `Danh sách Thông báo Hệ thống & Bản nháp (${filteredBroadcasts.length})`,
                        `Broadcast History & Drafts (${filteredBroadcasts.length})`
                      )}
                    </h4>
                    <p className="text-[0.75rem] text-[var(--ink2)]">
                      {tr(
                        'Quản lý các bản nháp, lịch phát hành và thông báo đang hiển thị trên môi trường DEMO.',
                        'Manage drafts, scheduled notices, and active DEMO broadcasts.'
                      )}
                    </p>
                  </div>

                  <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-2.5">
                    <div className="relative">
                      <Search
                        aria-hidden="true"
                        className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-[var(--ink3)]"
                      />
                      <input
                        type="search"
                        data-testid="sw-admin-broadcast-search"
                        value={broadcastSearchQuery}
                        onChange={(e) => setBroadcastSearchQuery(e.target.value)}
                        placeholder={tr(
                          'Tìm mã BRC-..., tiêu đề, đối tượng...',
                          'Search BRC-... code, title, audience...'
                        )}
                        aria-label={tr(
                          'Tìm kiếm thông báo broadcast',
                          'Search broadcasts'
                        )}
                        className="sw-in !pl-9 !py-2 text-[0.8125rem]"
                      />
                    </div>

                    <select
                      data-testid="sw-admin-broadcast-status-filter"
                      value={broadcastStatusFilter}
                      onChange={(e) =>
                        setBroadcastStatusFilter(
                          e.target.value as 'all' | AdminBroadcastStatus
                        )
                      }
                      aria-label={tr(
                        'Lọc theo trạng thái thông báo',
                        'Filter broadcast status'
                      )}
                      className="sw-in !py-2 text-[0.8125rem]"
                    >
                      <option value="all">
                        {tr('Tất cả trạng thái', 'All statuses')}
                      </option>
                      <option value="published_demo">
                        {tr('Đã phát hành (DEMO)', 'Published (DEMO)')}
                      </option>
                      <option value="scheduled_demo">
                        {tr('Đang lên lịch (DEMO)', 'Scheduled (DEMO)')}
                      </option>
                      <option value="draft">
                        {tr('Bản nháp (Draft)', 'Draft')}
                      </option>
                      <option value="cancelled_demo">
                        {tr('Đã thu hồi (DEMO)', 'Cancelled (DEMO)')}
                      </option>
                    </select>
                  </div>
                </div>

                <div className="overflow-x-auto border-t border-[var(--line)] pt-2">
                  <table
                    aria-label={tr(
                      'Bảng danh sách thông báo Broadcast',
                      'System broadcasts table'
                    )}
                    className="w-full text-left border-collapse"
                  >
                    <thead>
                      <tr className="border-b border-[var(--line)] text-[0.75rem] font-bold text-[var(--ink3)]">
                        <th scope="col" className="py-3 pl-2 pr-3">
                          {tr('Mã & Phân loại', 'Code & Category')}
                        </th>
                        <th scope="col" className="py-3 px-3">
                          {tr('Tiêu đề & Nội dung', 'Title & Body')}
                        </th>
                        <th scope="col" className="py-3 px-3">
                          {tr('Đối tượng nhận (Audience)', 'Audience Segment')}
                        </th>
                        <th scope="col" className="py-3 px-3">
                          {tr('Trạng thái DEMO', 'DEMO Status')}
                        </th>
                        <th scope="col" className="py-3 pl-3 pr-2 text-right">
                          {tr('Thao tác', 'Actions')}
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[var(--line)] text-[0.8125rem]">
                      {filteredBroadcasts.map((b) => {
                        const statusMeta = getBroadcastStatusMeta(b.status, tr);

                        return (
                          <tr
                            key={b.id}
                            data-testid={`sw-admin-broadcast-row-${b.broadcastCode}`}
                            className="hover:bg-[var(--sw-surface-muted)]/50 transition-colors"
                          >
                            <td className="py-3.5 pl-2 pr-3 align-top whitespace-nowrap">
                              <div className="font-bold text-[var(--ink)] tabular-nums">
                                {b.broadcastCode}
                              </div>
                              <div className="text-[0.75rem] font-semibold text-[var(--ink2)]">
                                {tr(b.categoryLabelVi, b.categoryLabelEn)}
                              </div>
                              <div className="text-[0.6875rem] text-[var(--ink3)] tabular-nums">
                                {b.publishedOrUpdatedAt}
                              </div>
                            </td>

                            <td className="py-3.5 px-3 align-top max-w-md">
                              <div className="font-bold text-[var(--ink)]">
                                {tr(b.titleVi, b.titleEn)}
                              </div>
                              <p className="text-[0.75rem] text-[var(--ink2)] mt-0.5 line-clamp-2">
                                {tr(b.bodyVi, b.bodyEn)}
                              </p>
                              <div className="text-[0.6875rem] text-[var(--sw-em-d)] font-semibold mt-0.5">
                                {tr(b.deliveryNoticeVi, b.deliveryNoticeEn)}
                              </div>
                            </td>

                            <td className="py-3.5 px-3 align-top">
                              <div className="font-semibold text-[var(--ink)]">
                                {tr(b.audienceLabelVi, b.audienceLabelEn)}
                              </div>
                              <div className="text-[0.75rem] text-[var(--ink2)] tabular-nums">
                                ~
                                {b.estimatedRecipients.toLocaleString(
                                  language === 'en' ? 'en-US' : 'vi-VN'
                                )}{' '}
                                {tr('người nhận mẫu', 'synthetic users')}
                              </div>
                            </td>

                            <td className="py-3.5 px-3 align-top whitespace-nowrap">
                              <div
                                data-testid={`sw-admin-broadcast-status-${b.broadcastCode}`}
                                className={`font-extrabold ${statusMeta.textClass}`}
                              >
                                {statusMeta.label}
                              </div>
                              <div className="text-[0.6875rem] text-[var(--ink3)]">
                                {b.authorOperator}
                              </div>
                            </td>

                            <td className="py-3.5 pl-3 pr-2 align-top text-right whitespace-nowrap">
                              <div className="flex flex-wrap items-center justify-end gap-1.5">
                                <button
                                  type="button"
                                  data-testid={`sw-admin-broadcast-load-draft-${b.broadcastCode}`}
                                  onClick={() => {
                                    setDraftTitle(
                                      language === 'en' ? b.titleEn : b.titleVi
                                    );
                                    setDraftBody(
                                      language === 'en' ? b.bodyEn : b.bodyVi
                                    );
                                    setDraftCategory(b.category);
                                    setDraftAudienceId(b.audienceId);
                                  }}
                                  className="sw-focus-ring sw-btn ghost sm"
                                >
                                  <span>
                                    {tr('Nạp vào khung soạn', 'Load to Editor')}
                                  </span>
                                </button>

                                {(b.status === 'draft' ||
                                  b.status === 'scheduled_demo') && (
                                  <button
                                    type="button"
                                    data-testid={`sw-admin-broadcast-publish-row-${b.broadcastCode}`}
                                    onClick={() =>
                                      openModal({
                                        kind: 'publish_existing_broadcast',
                                        broadcastId: b.id,
                                        broadcastCode: b.broadcastCode,
                                        titleVi: b.titleVi,
                                        titleEn: b.titleEn,
                                        audienceLabelVi: b.audienceLabelVi,
                                        audienceLabelEn: b.audienceLabelEn,
                                        estimatedRecipients:
                                          b.estimatedRecipients,
                                      })
                                    }
                                    className="sw-focus-ring sw-btn sw-btn-pri sm"
                                  >
                                    <Send
                                      aria-hidden="true"
                                      className="h-3.5 w-3.5"
                                    />
                                    <span>
                                      {tr('Phát hành (DEMO)', 'Publish (DEMO)')}
                                    </span>
                                  </button>
                                )}

                                {b.status === 'published_demo' && (
                                  <button
                                    type="button"
                                    data-testid={`sw-admin-broadcast-cancel-row-${b.broadcastCode}`}
                                    onClick={() =>
                                      openModal({
                                        kind: 'cancel_existing_broadcast',
                                        broadcastId: b.id,
                                        broadcastCode: b.broadcastCode,
                                        titleVi: b.titleVi,
                                        titleEn: b.titleEn,
                                      })
                                    }
                                    className="sw-focus-ring sw-btn ghost sm"
                                  >
                                    <span>
                                      {tr('Thu hồi (DEMO)', 'Cancel (DEMO)')}
                                    </span>
                                  </button>
                                )}
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            </section>
          )}

          {/* =================================================================
              MODULE 2: AI OPERATIONS MONITORING (SYNTHETIC DEMO)
              ================================================================= */}
          {showAiOps && (
            <section
              aria-labelledby="sw-admin-ai-ops-heading"
              data-testid="sw-admin-ai-ops-section"
              className="sw-card p-4 sm:p-5 space-y-4 min-w-0 overflow-hidden"
            >
              <div className="space-y-3 border-b border-[var(--line)] pb-4 min-w-0">
                <div className="space-y-1 min-w-0">
                  <h3
                    id="sw-admin-ai-ops-heading"
                    className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]"
                  >
                    {tr(
                      'Trạng thái Pipeline Phân loại, Dự báo Ngân sách & Bộ chắn An toàn',
                      'Categorization, Budget Pacing & Safety Guardrail Pipelines'
                    )}
                  </h3>
                  <p className="text-[0.8125rem] text-[var(--ink2)]">
                    {tr(
                      'Toàn bộ chỉ số độ trễ, tỉ lệ chặn lọc và kết quả mẫu dưới đây là dữ liệu giả lập — không gọi API mô hình AI hoặc gửi dữ liệu người dùng ra ngoài.',
                      'All latency metrics, guardrail pass rates, and sample outputs below are synthetic fixtures — zero external AI model API calls.'
                    )}
                  </p>
                </div>

                {/* AI KPI Strip — flat divided strip (no nested card borders) */}
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 pt-2 border-t border-[var(--line)]">
                  <div className="min-w-0">
                    <div className="text-[0.72rem] font-bold text-[var(--ink2)] truncate">
                      {tr('Lượt xử lý mẫu (24h)', 'Synthetic Req (24h)')}
                    </div>
                    <div className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)] tabular-nums truncate">
                      {aiSummaryMetrics.totalReq.toLocaleString(
                        language === 'en' ? 'en-US' : 'vi-VN'
                      )}
                    </div>
                  </div>
                  <div className="min-w-0">
                    <div className="text-[0.72rem] font-bold text-[var(--ink2)] truncate">
                      {tr('Tỉ lệ đạt kiểm duyệt an toàn', 'Guardrail Pass')}
                    </div>
                    <div className="font-display text-[1.0625rem] font-extrabold text-[var(--sw-em-d)] tabular-nums truncate">
                      {aiSummaryMetrics.avgGuardrailPct}%
                    </div>
                  </div>
                  <div className="min-w-0">
                    <div className="text-[0.72rem] font-bold text-[var(--ink2)] truncate">
                      {tr('Bảo vệ dữ liệu cá nhân', 'PII Redaction')}
                    </div>
                    <div className="font-display text-[1.0625rem] font-extrabold text-[var(--sw-em-d)] truncate">
                      {tr('Đã ẩn danh 100%', '100% Masked')}
                    </div>
                  </div>
                </div>
              </div>

              {/* AI Filters */}
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <div className="relative">
                  <Search
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-[var(--ink3)]"
                  />
                  <input
                    type="search"
                    data-testid="sw-admin-ai-search"
                    value={aiSearchQuery}
                    onChange={(e) => setAiSearchQuery(e.target.value)}
                    placeholder={tr(
                      'Tìm mã AI-PIPE-..., tên bộ phân tích...',
                      'Search AI-PIPE-... code, pipeline name...'
                    )}
                    aria-label={tr(
                      'Tìm kiếm pipeline vận hành AI',
                      'Search AI operations pipelines'
                    )}
                    className="sw-in w-full !pl-9 !py-2 text-[0.8125rem]"
                  />
                </div>

                <select
                  data-testid="sw-admin-ai-domain-filter"
                  value={aiDomainFilter}
                  onChange={(e) =>
                    setAiDomainFilter(
                      e.target.value as 'all' | AdminAiPipelineDomain
                    )
                  }
                  aria-label={tr(
                    'Lọc theo nghiệp vụ AI',
                    'Filter by AI domain'
                  )}
                  className="sw-in w-full !py-2 text-[0.8125rem]"
                >
                  <option value="all">
                    {tr('Tất cả nghiệp vụ AI', 'All AI domains')}
                  </option>
                  <option value="tx_categorization">
                    {tr('Phân loại Giao dịch', 'Transaction Categorization')}
                  </option>
                  <option value="budget_forecasting">
                    {tr('Dự báo Ngân sách', 'Budget Forecasting')}
                  </option>
                  <option value="fund_cashflow_summary">
                    {tr('Tóm tắt Quỹ nhóm', 'Group Fund Summary')}
                  </option>
                  <option value="anomaly_guardrail">
                    {tr('Kiểm soát An toàn AI (Guardrail)', 'Safety & Guardrails')}
                  </option>
                </select>

                <select
                  data-testid="sw-admin-ai-state-filter"
                  value={aiStateFilter}
                  onChange={(e) =>
                    setAiStateFilter(
                      e.target.value as 'all' | AdminAiPipelineState
                    )
                  }
                  aria-label={tr(
                    'Lọc theo trạng thái Pipeline AI',
                    'Filter by AI pipeline state'
                  )}
                  className="sw-in w-full !py-2 text-[0.8125rem]"
                >
                  <option value="all">
                    {tr('Tất cả trạng thái AI (DEMO)', 'All AI states (DEMO)')}
                  </option>
                  <option value="healthy_demo">
                    {tr('Hoạt động tốt (DEMO)', 'Healthy (DEMO)')}
                  </option>
                  <option value="degraded_latency_demo">
                    {tr('Độ trễ cao (DEMO)', 'Elevated Latency (DEMO)')}
                  </option>
                  <option value="guardrail_blocked_demo">
                    {tr('Đang chặn lọc chính sách (DEMO)', 'Guardrail Active (DEMO)')}
                  </option>
                  <option value="paused_demo">
                    {tr('Đang tạm dừng (DEMO)', 'Paused (DEMO)')}
                  </option>
                </select>
              </div>

              {/* AI Pipelines Table */}
              <div className="overflow-x-auto border-t border-[var(--line)] pt-2">
                <table
                  data-testid="sw-admin-ai-ops-table"
                  aria-label={tr(
                    'Bảng giám sát vận hành AI mẫu',
                    'Synthetic AI operations monitoring table'
                  )}
                  className="w-full text-left border-collapse"
                >
                  <thead>
                    <tr className="border-b border-[var(--line)] text-[0.75rem] font-bold text-[var(--ink3)]">
                      <th scope="col" className="py-3 pl-2 pr-3">
                        {tr('Pipeline & Mô hình mẫu', 'Pipeline & Synthetic Model')}
                      </th>
                      <th scope="col" className="py-3 px-3 text-right">
                        {tr('Lượt 24h / Độ trễ P95', '24h Vol / P95 Latency')}
                      </th>
                      <th scope="col" className="py-3 px-3">
                        {tr('Kết quả Mẫu & Bảo vệ PII', 'Sample Output & PII Hygiene')}
                      </th>
                      <th scope="col" className="py-3 px-3">
                        {tr('Trạng thái (DEMO)', 'State (DEMO)')}
                      </th>
                      <th scope="col" className="py-3 pl-3 pr-2 text-right">
                        {tr('Điều khiển (DEMO)', 'Control (DEMO)')}
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--line)] text-[0.8125rem]">
                    {filteredAiPipelines.map((p) => {
                      const stateMeta = getAiPipelineStateMeta(p.state, tr);
                      const isPaused = p.state === 'paused_demo';

                      return (
                        <tr
                          key={p.id}
                          data-testid={`sw-admin-ai-pipeline-row-${p.pipelineCode}`}
                          className="hover:bg-[var(--sw-surface-muted)]/50 transition-colors"
                        >
                          <td className="py-3.5 pl-2 pr-3 align-top">
                            <div className="font-bold text-[var(--ink)] tabular-nums">
                              {p.pipelineCode}
                            </div>
                            <div className="font-bold text-[var(--ink)]">
                              {tr(p.nameVi, p.nameEn)}
                            </div>
                            <code className="text-[0.6875rem] font-mono text-[var(--ink3)]">
                              {p.modelAliasDisplay}
                            </code>
                          </td>

                          <td className="py-3.5 px-3 align-top text-right whitespace-nowrap tabular-nums">
                            <div className="font-bold text-[var(--ink)]">
                              {p.syntheticRequests24h.toLocaleString(
                                language === 'en' ? 'en-US' : 'vi-VN'
                              )}{' '}
                              {tr('lượt', 'reqs')}
                            </div>
                            <div className="text-[0.75rem] text-[var(--ink2)]">
                              P95: {p.p95LatencyMs} ms · Guardrail:{' '}
                              {p.guardrailPassRatePct}%
                            </div>
                          </td>

                          <td className="py-3.5 px-3 align-top max-w-md">
                            <p className="text-[0.75rem] text-[var(--ink)] leading-snug">
                              {tr(p.lastSampleOutputVi, p.lastSampleOutputEn)}
                            </p>
                            <div className="text-[0.6875rem] text-[var(--sw-em-d)] font-semibold mt-0.5">
                              {tr(p.piiMaskingStatusVi, p.piiMaskingStatusEn)}
                            </div>
                          </td>

                          <td className="py-3.5 px-3 align-top">
                            <div
                              data-testid={`sw-admin-ai-state-${p.pipelineCode}`}
                              className={`font-extrabold ${stateMeta.textClass}`}
                            >
                              {stateMeta.label}
                            </div>
                            <div className="text-[0.6875rem] text-[var(--ink3)] mt-0.5">
                              {tr(p.operatorNoteVi, p.operatorNoteEn)}
                            </div>
                          </td>

                          <td className="py-3.5 pl-3 pr-2 align-top text-right whitespace-nowrap">
                            <button
                              type="button"
                              data-testid={`sw-admin-ai-toggle-state-${p.pipelineCode}`}
                              onClick={() =>
                                openModal({
                                  kind: 'toggle_ai_pipeline_state',
                                  pipelineId: p.id,
                                  pipelineCode: p.pipelineCode,
                                  nameVi: p.nameVi,
                                  nameEn: p.nameEn,
                                  currentState: p.state,
                                  targetState: isPaused
                                    ? 'healthy_demo'
                                    : 'paused_demo',
                                })
                              }
                              className={`sw-focus-ring sw-btn sm ${
                                isPaused ? 'sw-btn-pri' : 'ghost'
                              }`}
                            >
                              {isPaused ? (
                                <>
                                  <PlayCircle
                                    aria-hidden="true"
                                    className="h-3.5 w-3.5"
                                  />
                                  <span>
                                    {tr('Khôi phục (DEMO)', 'Resume (DEMO)')}
                                  </span>
                                </>
                              ) : (
                                <>
                                  <PauseCircle
                                    aria-hidden="true"
                                    className="h-3.5 w-3.5"
                                  />
                                  <span>
                                    {tr('Tạm dừng (DEMO)', 'Pause (DEMO)')}
                                  </span>
                                </>
                              )}
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </section>
          )}

          {/* =================================================================
              MODULE 3: BANK GATEWAY MONITORING (SYNTHETIC NAPAS 247 & DIRECT LINK)
              ================================================================= */}
          {showBankGateways && (
            <section
              aria-labelledby="sw-admin-bank-gateway-heading"
              data-testid="sw-admin-bank-gateway-section"
              className="sw-card p-4 sm:p-5 space-y-4 min-w-0 overflow-hidden"
            >
              <div className="space-y-3 border-b border-[var(--line)] pb-4 min-w-0">
                <div className="space-y-1 min-w-0">
                  <h3
                    id="sw-admin-bank-gateway-heading"
                    className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]"
                  >
                    {tr(
                      'Trạng thái Kết nối Cổng Ngân hàng, Độ trễ & Chế độ Bảo trì Mẫu',
                      'Bank Rail Status, Webhook Latency & Simulated Maintenance Routing'
                    )}
                  </h3>
                  <p className="text-[0.8125rem] text-[var(--ink2)]">
                    {tr(
                      'Các chỉ số cổng Vietcombank, BIDV, Techcombank, MB Bank, ACB, VPBank chỉ mô phỏng trạng thái vận hành DEMO — không gọi API ngân hàng thực.',
                      'Gateway metrics for Vietcombank, BIDV, Techcombank, MB Bank, ACB, and VPBank simulate operational states in DEMO — zero real bank provider calls.'
                    )}
                  </p>
                </div>

                {/* Bank KPI Strip — flat divided strip (no nested card borders) */}
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 pt-2 border-t border-[var(--line)]">
                  <div className="min-w-0">
                    <div className="text-[0.72rem] font-bold text-[var(--ink2)] truncate">
                      {tr('Cổng ổn định', 'Operational')}
                    </div>
                    <div className="font-display text-[1.0625rem] font-extrabold text-[var(--sw-em-d)] tabular-nums truncate">
                      {bankSummaryMetrics.operationalCount} / {bankGateways.length}
                    </div>
                  </div>
                  <div className="min-w-0">
                    <div className="text-[0.72rem] font-bold text-[var(--ink2)] truncate">
                      {tr('Độ trễ / Bảo trì', 'Degraded / Maint')}
                    </div>
                    <div className="font-display text-[1.0625rem] font-extrabold text-[var(--amber)] tabular-nums truncate">
                      {bankSummaryMetrics.degradedCount +
                        bankSummaryMetrics.maintenanceCount}
                    </div>
                  </div>
                  <div className="min-w-0">
                    <div className="text-[0.72rem] font-bold text-[var(--ink2)] truncate">
                      {tr('Chờ đối soát mã', 'Pending Match')}
                    </div>
                    <div className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)] tabular-nums truncate">
                      {bankSummaryMetrics.pendingReconcileTotal}
                    </div>
                  </div>
                </div>
              </div>

              {/* Bank Gateway Filters */}
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <div className="relative">
                  <Search
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-[var(--ink3)]"
                  />
                  <input
                    type="search"
                    data-testid="sw-admin-bank-search"
                    value={bankSearchQuery}
                    onChange={(e) => setBankSearchQuery(e.target.value)}
                    placeholder={tr(
                      'Tìm mã GW-..., tên ngân hàng (VCB, BIDV, TCB...)',
                      'Search GW-... code, bank name (VCB, BIDV, TCB...)'
                    )}
                    aria-label={tr(
                      'Tìm kiếm cổng ngân hàng',
                      'Search bank gateways'
                    )}
                    className="sw-in w-full !pl-9 !py-2 text-[0.8125rem]"
                  />
                </div>

                <select
                  data-testid="sw-admin-bank-state-filter"
                  value={bankStateFilter}
                  onChange={(e) =>
                    setBankStateFilter(
                      e.target.value as 'all' | AdminBankGatewayState
                    )
                  }
                  aria-label={tr(
                    'Lọc theo trạng thái cổng ngân hàng',
                    'Filter by bank gateway state'
                  )}
                  className="sw-in w-full !py-2 text-[0.8125rem]"
                >
                  <option value="all">
                    {tr('Tất cả trạng thái cổng (DEMO)', 'All gateway states (DEMO)')}
                  </option>
                  <option value="operational_demo">
                    {tr('Hoạt động ổn định (DEMO)', 'Operational (DEMO)')}
                  </option>
                  <option value="degraded_latency_demo">
                    {tr('Độ trễ cao · Cảnh báo (DEMO)', 'Degraded Latency (DEMO)')}
                  </option>
                  <option value="maintenance_demo">
                    {tr('Đang bảo trì (DEMO)', 'Maintenance Mode (DEMO)')}
                  </option>
                </select>

                <select
                  data-testid="sw-admin-bank-rail-filter"
                  value={bankRailFilter}
                  onChange={(e) =>
                    setBankRailFilter(
                      e.target.value as 'all' | AdminBankRailType
                    )
                  }
                  aria-label={tr(
                    'Lọc theo kênh kết nối ngân hàng',
                    'Filter by banking rail type'
                  )}
                  className="sw-in w-full !py-2 text-[0.8125rem]"
                >
                  <option value="all">
                    {tr('Mọi phương thức kết nối', 'All connection rails')}
                  </option>
                  <option value="napas_247">
                    {tr('Chuyển nhanh Napas 247', 'Napas 247 Fast Transfer')}
                  </option>
                  <option value="direct_link">
                    {tr('Liên kết Ví trực tiếp (Direct Link)', 'Direct Wallet Link')}
                  </option>
                  <option value="vietqr_webhook">
                    {tr('VietQR Động & Báo Có', 'Dynamic VietQR Webhook')}
                  </option>
                </select>
              </div>

              {/* Bank Gateways Table */}
              <div className="overflow-x-auto border-t border-[var(--line)] pt-2">
                <table
                  data-testid="sw-admin-bank-gateway-table"
                  aria-label={tr(
                    'Bảng giám sát cổng ngân hàng mẫu',
                    'Synthetic bank gateway monitoring table'
                  )}
                  className="w-full text-left border-collapse"
                >
                  <thead>
                    <tr className="border-b border-[var(--line)] text-[0.75rem] font-bold text-[var(--ink3)]">
                      <th scope="col" className="py-3 pl-2 pr-3">
                        {tr('Mã Cổng & Ngân hàng', 'Gateway Code & Bank')}
                      </th>
                      <th scope="col" className="py-3 px-3 text-right">
                        {tr('Sản lượng 24h / Webhook', '24h Vol / Webhook')}
                      </th>
                      <th scope="col" className="py-3 px-3 text-right">
                        {tr('Độ trễ P95 / Chờ khớp', 'P95 Latency / Pending')}
                      </th>
                      <th scope="col" className="py-3 px-3">
                        {tr('Trạng thái Vận hành (DEMO)', 'Operational Status (DEMO)')}
                      </th>
                      <th scope="col" className="py-3 pl-3 pr-2 text-right">
                        {tr('Chuyển chế độ (DEMO)', 'Switch Mode (DEMO)')}
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--line)] text-[0.8125rem]">
                    {filteredBankGateways.map((gw) => {
                      const gwStateMeta = getBankGatewayStateMeta(gw.state, tr);
                      const isOperational = gw.state === 'operational_demo';

                      return (
                        <tr
                          key={gw.id}
                          data-testid={`sw-admin-bank-gateway-row-${gw.gatewayCode}`}
                          className="hover:bg-[var(--sw-surface-muted)]/50 transition-colors"
                        >
                          <td className="py-3.5 pl-2 pr-3 align-top">
                            <div className="font-bold text-[var(--ink)] tabular-nums">
                              {gw.gatewayCode}
                            </div>
                            <div className="font-bold text-[var(--ink)]">
                              {gw.bankName}
                            </div>
                            <div className="text-[0.75rem] text-[var(--ink2)]">
                              {tr(gw.railLabelVi, gw.railLabelEn)}
                            </div>
                          </td>

                          <td className="py-3.5 px-3 align-top text-right whitespace-nowrap tabular-nums">
                            <div className="font-bold text-[var(--ink)]">
                              {gw.syntheticTxVolume24h.toLocaleString(
                                language === 'en' ? 'en-US' : 'vi-VN'
                              )}{' '}
                              {tr('lệnh mẫu', 'txs')}
                            </div>
                            <div className="text-[0.75rem] text-[var(--sw-em-d)] font-semibold">
                              Webhook: {gw.webhookSuccessRatePct}%
                            </div>
                          </td>

                          <td className="py-3.5 px-3 align-top text-right whitespace-nowrap tabular-nums">
                            <div
                              className={`font-bold ${
                                gw.p95RoundtripMs > 1000
                                  ? 'text-[var(--amber)]'
                                  : 'text-[var(--ink)]'
                              }`}
                            >
                              {gw.p95RoundtripMs} ms
                            </div>
                            <div className="text-[0.75rem] text-[var(--ink2)]">
                              {tr('Chờ đối soát:', 'Pending:')}{' '}
                              {gw.pendingReconcileCount}
                            </div>
                          </td>

                          <td className="py-3.5 px-3 align-top max-w-md">
                            <div
                              data-testid={`sw-admin-bank-state-${gw.gatewayCode}`}
                              className={`font-extrabold ${gwStateMeta.textClass}`}
                            >
                              {gwStateMeta.label}
                            </div>
                            <p className="text-[0.75rem] text-[var(--ink2)] mt-0.5 leading-snug">
                              {tr(gw.statusSummaryVi, gw.statusSummaryEn)}
                            </p>
                          </td>

                          <td className="py-3.5 pl-3 pr-2 align-top text-right whitespace-nowrap">
                            <button
                              type="button"
                              data-testid={`sw-admin-bank-toggle-mode-${gw.gatewayCode}`}
                              onClick={() =>
                                openModal({
                                  kind: 'switch_bank_gateway_state',
                                  gatewayId: gw.id,
                                  gatewayCode: gw.gatewayCode,
                                  bankName: gw.bankName,
                                  currentState: gw.state,
                                  targetState: isOperational
                                    ? 'maintenance_demo'
                                    : 'operational_demo',
                                })
                              }
                              className={`sw-focus-ring sw-btn sm ${
                                isOperational ? 'ghost' : 'sw-btn-pri'
                              }`}
                            >
                              {isOperational ? (
                                <>
                                  <Wrench
                                    aria-hidden="true"
                                    className="h-3.5 w-3.5"
                                  />
                                  <span>
                                    {tr(
                                      'Bật Bảo trì (DEMO)',
                                      'Set Maintenance (DEMO)'
                                    )}
                                  </span>
                                </>
                              ) : (
                                <>
                                  <CheckCircle2
                                    aria-hidden="true"
                                    className="h-3.5 w-3.5"
                                  />
                                  <span>
                                    {tr(
                                      'Khôi phục cổng (DEMO)',
                                      'Restore Gateway (DEMO)'
                                    )}
                                  </span>
                                </>
                              )}
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </section>
          )}

          {/* =================================================================
              MODULE 4: SYSTEM CONFIGURATION & SESSION AUDIT LOG
              ================================================================= */}
          {showConfigAudit && (
            <section
              aria-label={tr(
                'Cấu hình hệ thống và Nhật ký kiểm toán',
                'System configuration and Audit log'
              )}
              data-testid="sw-admin-system-config-audit-section"
              className="grid grid-cols-1 xl:grid-cols-12 gap-6 items-start min-w-0"
            >
              {/* Left 6 cols: System Config & Redacted Secrets */}
              <div
                data-testid="sw-admin-system-config-view"
                className="xl:col-span-6 min-w-0 sw-card p-5 space-y-4"
              >
                <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--line)] pb-3">
                  <div>
                    <div className="text-[0.75rem] font-bold text-[var(--sw-em-d)]">
                      {tr(
                        'Cấu hình Bất biến & Bảo mật Khóa ([REDACTED_SECRET])',
                        'System Invariants & Secret Hygiene ([REDACTED_SECRET])'
                      )}
                    </div>
                    <h4 className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                      {tr(
                        'Tham số Hệ thống & Chính sách Zero-Secret',
                        'System Parameters & Zero-Secret Policy'
                      )}
                    </h4>
                  </div>

                  <button
                    type="button"
                    data-testid="sw-admin-verify-config-btn"
                    onClick={() =>
                      openModal({
                        kind: 'verify_system_config',
                        paramCount: configParams.length,
                      })
                    }
                    className="sw-focus-ring sw-btn sw-btn-pri sm whitespace-nowrap"
                  >
                    <ShieldCheck aria-hidden="true" className="h-4 w-4" />
                    <span>
                      {tr('Xác nhận kiểm tra ca trực', 'Verify Shift Config')}
                    </span>
                  </button>
                </div>

                <div className="divide-y divide-[var(--line)] text-[0.8125rem]">
                  {configParams.map((param) => (
                    <div key={param.id} className="py-3 space-y-1">
                      <div className="flex items-center justify-between gap-2">
                        <span className="font-bold text-[var(--ink)]">
                          {tr(param.labelVi, param.labelEn)}
                        </span>
                        <span
                          className={`inline-flex items-center gap-1 font-mono text-[0.75rem] font-bold ${
                            param.isRedactedSecret
                              ? 'text-[var(--amber)]'
                              : 'text-[var(--sw-em-d)]'
                          }`}
                        >
                          {param.isRedactedSecret && (
                            <Lock
                              aria-hidden="true"
                              className="h-3.5 w-3.5 shrink-0"
                            />
                          )}
                          <span>{param.valueDisplay}</span>
                        </span>
                      </div>
                      <div className="text-[0.75rem] text-[var(--ink2)]">
                        {tr(param.descriptionVi, param.descriptionEn)}
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Right 6 cols: Global Audit Log */}
              <div
                data-testid="sw-admin-audit-log-view"
                className="xl:col-span-6 min-w-0 sw-card p-5 space-y-4"
              >
                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 border-b border-[var(--line)] pb-3">
                  <div>
                    <div className="flex items-center gap-1.5 text-[0.75rem] font-bold text-[var(--sw-em-d)]">
                      <ClipboardList aria-hidden="true" className="h-4 w-4" />
                      <span>
                        {tr(
                          `Nhật ký Kiểm toán Toàn phiên (${filteredAuditEvents.length})`,
                          `Session-Wide Audit Log (${filteredAuditEvents.length})`
                        )}
                      </span>
                    </div>
                    <h4 className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                      {tr(
                        'Lịch sử Thao tác Vận hành DEMO',
                        'DEMO Operational Audit Trail'
                      )}
                    </h4>
                  </div>

                  <div className="flex items-center gap-2">
                    <input
                      type="search"
                      data-testid="sw-admin-audit-search"
                      value={auditSearchQuery}
                      onChange={(e) => setAuditSearchQuery(e.target.value)}
                      placeholder={tr('Tìm mã AUD-...', 'Search AUD-...')}
                      aria-label={tr('Tìm kiếm kiểm toán', 'Search audit log')}
                      className="sw-in !py-1.5 !px-2.5 text-[0.75rem] w-36"
                    />
                    <select
                      data-testid="sw-admin-audit-module-filter"
                      value={auditModuleFilter}
                      onChange={(e) =>
                        setAuditModuleFilter(
                          e.target.value as
                            | 'all'
                            | AdminGlobalAuditEvent['module']
                        )
                      }
                      aria-label={tr(
                        'Lọc phân hệ kiểm toán',
                        'Filter audit module'
                      )}
                      className="sw-in !py-1.5 text-[0.75rem]"
                    >
                      <option value="all">{tr('Tất cả', 'All')}</option>
                      <option value="system">
                        {tr('Hệ thống', 'System')}
                      </option>
                      <option value="support">
                        {tr('Hỗ trợ', 'Support')}
                      </option>
                      <option value="ledger">
                        {tr('Sổ cái', 'Ledger')}
                      </option>
                      <option value="users">
                        {tr('Người dùng', 'Users')}
                      </option>
                    </select>
                  </div>
                </div>

                <div className="divide-y divide-[var(--line)] max-h-[460px] overflow-y-auto pr-1 text-[0.8125rem] sw-sheet-body">
                  {filteredAuditEvents.map((ev) => (
                    <div
                      key={ev.id}
                      data-testid={`sw-admin-audit-item-${ev.referenceCode}`}
                      className="py-3 space-y-1"
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2 text-[0.75rem]">
                        <span className="font-bold text-[var(--sw-em-d)] tabular-nums">
                          {ev.referenceCode} ·{' '}
                          {tr(ev.moduleLabelVi, ev.moduleLabelEn)}
                        </span>
                        <span className="text-[var(--ink3)] tabular-nums">
                          {ev.timestamp}
                        </span>
                      </div>
                      <div className="font-bold text-[var(--ink)]">
                        {tr(ev.actionTitleVi, ev.actionTitleEn)}
                      </div>
                      <div className="text-[0.75rem] text-[var(--ink2)]">
                        {ev.targetEntity} — {tr(ev.detailVi, ev.detailEn)}
                      </div>
                      <div className="text-[0.75rem] text-[var(--ink)]">
                        <span className="font-bold text-[var(--ink2)]">
                          {tr('Lý do:', 'Reason:')}
                        </span>{' '}
                        {ev.reason}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </section>
          )}
        </div>
      )}

      {/* =====================================================================
          DELIBERATE CONFIRMATION MODAL FOR ADMIN SYSTEM ACTIONS
          ===================================================================== */}
      {pendingAction && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="sw-admin-a5-confirm-title"
          data-testid="sw-admin-a5-confirm-modal"
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
                    id="sw-admin-a5-confirm-title"
                    className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]"
                  >
                    {pendingAction.kind === 'publish_authored_broadcast' ||
                    pendingAction.kind === 'publish_existing_broadcast'
                      ? tr(
                          'Xác nhận Phát hành Thông báo Broadcast (DEMO)',
                          'Confirm Publishing System Broadcast (DEMO)'
                        )
                      : pendingAction.kind === 'cancel_existing_broadcast'
                      ? tr(
                          `Xác nhận Thu hồi Thông báo ${pendingAction.broadcastCode}`,
                          `Confirm Cancelling Broadcast ${pendingAction.broadcastCode}`
                        )
                      : pendingAction.kind === 'toggle_ai_pipeline_state'
                      ? tr(
                          `Xác nhận Đổi Trạng thái Pipeline AI (${pendingAction.pipelineCode})`,
                          `Confirm AI Pipeline State Change (${pendingAction.pipelineCode})`
                        )
                      : pendingAction.kind === 'switch_bank_gateway_state'
                      ? tr(
                          `Xác nhận Chuyển Chế độ Cổng Ngân hàng (${pendingAction.gatewayCode})`,
                          `Confirm Switching Bank Gateway Mode (${pendingAction.gatewayCode})`
                        )
                      : tr(
                          'Xác nhận Kiểm tra Cấu hình Hệ thống Ca trực',
                          'Confirm Shift System Configuration Verification'
                        )}
                  </h3>
                  <div className="text-[0.75rem] text-[var(--ink2)]">
                    {tr(
                      'Thao tác chỉ cập nhật dữ liệu mẫu DEMO và lưu vào Nhật ký Kiểm toán',
                      'Updates synthetic DEMO state only and records an Audit Log entry'
                    )}
                  </div>
                </div>
              </div>

              <button
                type="button"
                onClick={closeModal}
                aria-label={tr('Đóng hộp thoại', 'Close modal')}
                className="sw-focus-ring sw-ib !h-9 !w-9 !min-h-[36px] !min-w-[36px] shrink-0"
              >
                <X aria-hidden="true" className="h-4 w-4" />
              </button>
            </div>

            <div className="flex-1 min-h-0 overflow-y-auto overscroll-contain p-5 sm:px-6 py-4 space-y-4 sw-sheet-body">
              {/* Action Target Summary */}
              <div className="rounded-xl border border-[var(--line)] bg-[var(--sw-surface-muted)]/60 p-3.5 text-[0.8125rem] space-y-1">
                {pendingAction.kind === 'publish_authored_broadcast' && (
                  <>
                    <div>
                      <span className="font-bold text-[var(--ink2)]">
                        {tr('Tiêu đề:', 'Title:')}
                      </span>{' '}
                      <span className="font-bold text-[var(--ink)]">
                        {pendingAction.titleVi}
                      </span>
                    </div>
                    <div>
                      <span className="font-bold text-[var(--ink2)]">
                        {tr('Đối tượng nhận:', 'Target Audience:')}
                      </span>{' '}
                      <span className="font-bold text-[var(--sw-em-d)]">
                        {tr(
                          selectedAudienceOption.labelVi,
                          selectedAudienceOption.labelEn
                        )}{' '}
                        (~
                        {selectedAudienceOption.estimatedRecipients.toLocaleString(
                          'vi-VN'
                        )}{' '}
                        {tr('tài khoản mẫu', 'synthetic users')})
                      </span>
                    </div>
                  </>
                )}
                {pendingAction.kind === 'publish_existing_broadcast' && (
                  <div>
                    <span className="font-bold text-[var(--ink)]">
                      {pendingAction.broadcastCode} — {pendingAction.titleVi}
                    </span>{' '}
                    ({pendingAction.audienceLabelVi})
                  </div>
                )}
                {pendingAction.kind === 'cancel_existing_broadcast' && (
                  <div>
                    <span className="font-bold text-[var(--ink)]">
                      {pendingAction.broadcastCode} — {pendingAction.titleVi}
                    </span>
                  </div>
                )}
                {pendingAction.kind === 'toggle_ai_pipeline_state' && (
                  <div>
                    <span className="font-bold text-[var(--ink)]">
                      {pendingAction.pipelineCode} ·{' '}
                      {tr(pendingAction.nameVi, pendingAction.nameEn)}
                    </span>
                  </div>
                )}
                {pendingAction.kind === 'switch_bank_gateway_state' && (
                  <div>
                    <span className="font-bold text-[var(--ink)]">
                      {pendingAction.gatewayCode} · {pendingAction.bankName}
                    </span>
                  </div>
                )}
                <div className="text-[0.75rem] text-[var(--sw-em-d)] font-semibold pt-1">
                  {tr(
                    'Cam kết DEMO: Không gửi thông báo đẩy/SMS/Email, không gọi nhà cung cấp AI và không kết nối ngân hàng thực.',
                    'DEMO Guarantee: Does not send external notifications, invoke AI providers, or call real banking APIs.'
                  )}
                </div>
              </div>

              {/* Preset reasons */}
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
                        data-testid={`sw-admin-a5-preset-reason-${idx}`}
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
                  htmlFor="sw-admin-a5-modal-reason"
                  className="block text-[0.8125rem] font-bold text-[var(--ink)]"
                >
                  {tr(
                    'Lý do vận hành & căn cứ phê duyệt (Bắt buộc) *',
                    'Operational reason & justification (Required) *'
                  )}
                </label>
                <textarea
                  id="sw-admin-a5-modal-reason"
                  data-testid="sw-admin-a5-modal-reason-input"
                  rows={3}
                  value={modalReasonInput}
                  onChange={(e) => {
                    setModalReasonInput(e.target.value);
                    if (modalError) setModalError(null);
                  }}
                  placeholder={tr(
                    'Nhập lý do vận hành cụ thể (tối thiểu 6 ký tự)...',
                    'Enter specific operational reason (min 6 chars)...'
                  )}
                  className="sw-in w-full text-[0.84375rem]"
                />
              </div>

              {/* Deliberate confirmation checkbox */}
              <label className="flex items-start gap-2.5 text-[0.8125rem] text-[var(--ink)] cursor-pointer select-none">
                <input
                  type="checkbox"
                  data-testid="sw-admin-a5-modal-confirm-checkbox"
                  checked={isModalConfirmed}
                  onChange={(e) => {
                    setIsModalConfirmed(e.target.checked);
                    if (modalError) setModalError(null);
                  }}
                  className="mt-1 h-4 w-4 rounded border-[var(--line)] accent-[#059669] shrink-0"
                />
                <span>
                  {tr(
                    'Tôi xác nhận thực hiện thao tác DEMO này (không gửi thông báo thực hay gọi nhà cung cấp bên ngoài).',
                    'I deliberately confirm this DEMO action (no real notification sending or external provider calls).'
                  )}
                </span>
              </label>

              {modalError && (
                <div
                  role="alert"
                  data-testid="sw-admin-a5-modal-error"
                  className="rounded-xl bg-[var(--rose-s)] px-3 py-2 text-[0.8125rem] font-bold text-[var(--rose)]"
                >
                  {modalError}
                </div>
              )}
            </div>

            <div className="shrink-0 flex items-center justify-end gap-2 px-5 sm:px-6 py-3.5 border-t border-[var(--line)] bg-[var(--sw-sheet-bg)]">
              <button
                type="button"
                data-testid="sw-admin-a5-modal-cancel-btn"
                onClick={closeModal}
                className="sw-focus-ring sw-btn ghost sm"
              >
                <span>{tr('Hủy bỏ', 'Cancel')}</span>
              </button>
              <button
                type="submit"
                data-testid="sw-admin-a5-modal-submit-btn"
                className="sw-focus-ring sw-btn sw-btn-pri sm"
              >
                <Check aria-hidden="true" className="h-4 w-4" />
                <span>
                  {tr('Xác nhận & Ghi Kiểm toán', 'Confirm & Record Audit')}
                </span>
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
};
