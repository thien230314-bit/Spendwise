import { useState, useEffect, useCallback, useRef } from 'react';
import {
  SupportCase,
  DisputeRecord,
  SupportCaseCategory,
  SupportCaseStatus,
  DisputeReason,
  DisputeStatus,
  AttachmentMetadata,
  SupportHistoryItem,
  User,
} from '../types';
import {
  casesApiService,
  SupportCaseDetailDto,
  DisputeDetailDto,
  CaseHistoryDto,
  CaseAttachmentDto,
  CaseAttachmentUpload,
} from './casesApiService';
import { ApiError } from '../lib/apiError';

export function mapCaseAttachmentDto(dto: CaseAttachmentDto): AttachmentMetadata {
  return {
    id: dto.id,
    name: dto.fileName,
    size: dto.length,
    type: dto.contentType,
    uploadedAt: dto.createdAt,
  };
}

export function mapCaseHistoryDto(dto: CaseHistoryDto): SupportHistoryItem {
  return {
    id: dto.id,
    actorId: dto.actorId,
    actorName: dto.actorName,
    actorRole: (dto.actorRole as 'User' | 'SystemAdmin') || 'User',
    action: (dto.action as any) || 'reply',
    message: dto.message,
    oldStatus: dto.oldStatus || undefined,
    newStatus: dto.newStatus || undefined,
    timestamp: dto.createdAt,
    attachments: (dto.attachments || []).map(mapCaseAttachmentDto),
  };
}

export function mapSupportCaseDetailDto(dto: SupportCaseDetailDto): SupportCase {
  const history = (dto.history || []).map(mapCaseHistoryDto);
  const firstItem = history[0];
  return {
    id: dto.id,
    ticketNumber: dto.ticketNumber,
    userId: dto.userId,
    subject: dto.subject,
    title: dto.subject,
    category: (dto.category as SupportCaseCategory) || 'general',
    message: dto.description || firstItem?.message || '',
    description: dto.description,
    linkedTransactionId: dto.linkedTransactionId || undefined,
    status: (dto.status as SupportCaseStatus) || 'pending',
    priority: (dto.priority as any) || 'normal',
    createdAt: dto.createdAt,
    updatedAt: dto.updatedAt || dto.createdAt,
    latestReply: dto.latestReplyAt || undefined,
    correctionGranted: dto.correctionGranted,
    history,
  };
}

export function mapDisputeDetailDto(dto: DisputeDetailDto): DisputeRecord {
  const history = (dto.history || []).map(mapCaseHistoryDto);
  return {
    id: dto.id,
    disputeCode: `#${dto.reference}`,
    userId: dto.userId,
    transactionId: dto.linkedTransactionId,
    transactionRef: dto.reference,
    transactionTitle: dto.title,
    transactionAmount: Number(dto.amount) || 0,
    transactionCounterparty: dto.counterparty || undefined,
    transactionDate: dto.occurredAt,
    reason: (dto.reason as DisputeReason) || 'other',
    description: dto.description,
    status: (dto.status as DisputeStatus) || 'pending',
    resolutionNote: dto.resolution || undefined,
    createdAt: dto.createdAt,
    updatedAt: dto.updatedAt || dto.createdAt,
    history,
  };
}

export function validateAndConvertAttachments(attachments?: AttachmentMetadata[]): {
  valid: boolean;
  uploads: CaseAttachmentUpload[];
  error?: string;
} {
  if (!attachments || attachments.length === 0) {
    return { valid: true, uploads: [] };
  }

  if (attachments.length > 5) {
    return {
      valid: false,
      uploads: [],
      error: 'Chỉ được đính kèm tối đa 5 tệp tin.',
    };
  }

  const maxBytes = 5 * 1024 * 1024; // 5MB
  const uploads: CaseAttachmentUpload[] = [];

  for (const att of attachments) {
    if (att.size && att.size > maxBytes) {
      return {
        valid: false,
        uploads: [],
        error: `Tệp "${att.name}" vượt quá dung lượng tối đa cho phép (5MB).`,
      };
    }

    if (!att.dataUrl) {
      return {
        valid: false,
        uploads: [],
        error: `Không thể đọc dữ liệu của tệp "${att.name}". Vui lòng chọn lại tệp.`,
      };
    }

    const parts = att.dataUrl.split(',');
    if (parts.length < 2 || !parts[1]) {
      return {
        valid: false,
        uploads: [],
        error: `Định dạng tệp "${att.name}" không hợp lệ để gửi lên hệ thống.`,
      };
    }

    uploads.push({
      fileName: att.name,
      contentType: att.type || 'application/octet-stream',
      content: parts[1],
    });
  }

  return { valid: true, uploads };
}

export function attachmentMetadataToUpload(att: AttachmentMetadata): CaseAttachmentUpload | null {
  if (!att.dataUrl) return null;
  const parts = att.dataUrl.split(',');
  if (parts.length < 2 || !parts[1]) return null;
  return {
    fileName: att.name,
    contentType: att.type || 'application/octet-stream',
    content: parts[1], // base64 payload
  };
}


export const SUPPORT_DISPUTES_UPDATED_EVENT = 'spendwise_support_disputes_updated';


export function useSupportDisputes(currentUser: User | null) {
  const [cases, setCases] = useState<SupportCase[]>([]);
  const [disputes, setDisputes] = useState<DisputeRecord[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  const fetchSeqRef = useRef<number>(0);
  const currentUserIdRef = useRef<string | null>(currentUser?.id || null);

  // Immediately clear records if user switches or logs out
  if (currentUserIdRef.current !== (currentUser?.id || null)) {
    currentUserIdRef.current = currentUser?.id || null;
    fetchSeqRef.current++;
    setCases([]);
    setDisputes([]);
    setLoadError(null);
  }

  const refresh = useCallback(async () => {
    const seq = ++fetchSeqRef.current;
    const userId = currentUser?.id;

    if (!userId) {
      setCases([]);
      setDisputes([]);
      setIsLoading(false);
      setLoadError(null);
      return;
    }

    setIsLoading(true);
    setLoadError(null);

    try {
      if (currentUser.role === 'SystemAdmin') {
        const [backendCases, backendDisputes] = await Promise.all([
          casesApiService.getAdminSupportCases(),
          casesApiService.getAdminDisputes(),
        ]);

        if (seq !== fetchSeqRef.current) return;

        setCases((backendCases || []).map(mapSupportCaseDetailDto));
        setDisputes((backendDisputes || []).map(mapDisputeDetailDto));
      } else {
        const [backendCases, backendDisputes] = await Promise.all([
          casesApiService.getSupportCases(),
          casesApiService.getDisputes(),
        ]);

        if (seq !== fetchSeqRef.current) return;

        setCases((backendCases || []).map(mapSupportCaseDetailDto));
        setDisputes((backendDisputes || []).map(mapDisputeDetailDto));
      }
    } catch (err) {
      if (seq !== fetchSeqRef.current) return;
      console.error('Failed to load cases/disputes from backend API:', err);
      const apiErr = err as ApiError;
      const errorMsg =
        apiErr?.detail ||
        apiErr?.title ||
        'Không thể tải dữ liệu từ máy chủ. Vui lòng thử lại.';
      setCases([]);
      setDisputes([]);
      setLoadError(errorMsg);
    } finally {
      if (seq === fetchSeqRef.current) {
        setIsLoading(false);
      }
    }
  }, [currentUser?.id, currentUser?.role]);

  useEffect(() => {
    refresh();

    const handleUpdate = () => {
      refresh();
    };

    window.addEventListener(SUPPORT_DISPUTES_UPDATED_EVENT, handleUpdate);

    return () => {
      window.removeEventListener(SUPPORT_DISPUTES_UPDATED_EVENT, handleUpdate);
    };
  }, [refresh]);

  return {
    supportCases: cases,
    disputes,
    isLoading,
    loadError,
    refresh,
    loadAttachmentBlob: (att: AttachmentMetadata) => {
      if (currentUser?.role === 'SystemAdmin') {
        return casesApiService.getAdminAttachmentBlob(att.id);
      }
      return casesApiService.getAttachmentBlob(att.id);
    },
    createSupportCase: async (params: {
      category: SupportCaseCategory;
      title: string;
      description: string;
      attachments?: AttachmentMetadata[];
      linkedTransactionId?: string;
      linkedTransactionRef?: string;
    }): Promise<{
      success: boolean;
      caseId?: string;
      case?: SupportCase;
      attachmentError?: string;
      error?: string;
    }> => {
      if (!currentUser) return { success: false, error: 'Chưa đăng nhập' };

      // Validate attachments before calling creation endpoint
      const validation = validateAndConvertAttachments(params.attachments);
      if (!validation.valid) {
        return { success: false, error: validation.error };
      }

      let createdCaseId = '';
      try {
        const createdCase = await casesApiService.createSupportCase({
          category: params.category,
          priority: 'normal',
          linkedTransactionId: params.linkedTransactionId || null,
          subject: params.title.trim(),
          description: params.description.trim(),
        });
        createdCaseId = createdCase.id;
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi gửi yêu cầu hỗ trợ.',
        };
      }

      // If attachments exist, upload them via initial reply message
      if (validation.uploads.length > 0) {
        try {
          await casesApiService.replySupportCase(createdCaseId, {
            message: params.description.trim() || 'Tài liệu / Ảnh đính kèm',
            attachments: validation.uploads,
          });
        } catch (uploadErr) {
          console.warn('Failed to upload attachments for created support case:', uploadErr);
          const uploadApiErr = uploadErr as ApiError;
          await refresh();
          return {
            success: true,
            caseId: createdCaseId,
            attachmentError:
              uploadApiErr?.detail ||
              uploadApiErr?.title ||
              'Yêu cầu hỗ trợ đã được tạo nhưng tệp đính kèm tải lên thất bại. Vui lòng gửi lại tệp trong phần phản hồi của vé.',
          };
        }
      }

      await refresh();
      return { success: true, caseId: createdCaseId };
    },
    createDispute: async (params: {
      transaction: {
        id: string;
        userId?: string;
        referenceCode?: string;
        title: string;
        amount: number;
        date: string;
        recipient?: string;
        sender?: string;
        merchantName?: string;
      };
      reason: DisputeReason;
      description: string;
      attachments?: AttachmentMetadata[];
    }): Promise<{
      success: boolean;
      disputeId?: string;
      dispute?: DisputeRecord;
      attachmentError?: string;
      error?: string;
    }> => {
      if (!currentUser) return { success: false, error: 'Chưa đăng nhập' };

      // Validate attachments before calling creation endpoint
      const validation = validateAndConvertAttachments(params.attachments);
      if (!validation.valid) {
        return { success: false, error: validation.error };
      }

      let createdDisputeId = '';
      try {
        const createdDispute = await casesApiService.createDispute({
          linkedTransactionId: params.transaction.id,
          reason: params.reason,
          description: params.description.trim(),
        });
        createdDisputeId = createdDispute.id;
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi gửi khiếu nại giao dịch.',
        };
      }

      // If attachments exist, upload them via initial reply message
      if (validation.uploads.length > 0) {
        try {
          await casesApiService.replyDispute(createdDisputeId, {
            message: params.description.trim() || 'Biên lai / Tài liệu đối soát',
            attachments: validation.uploads,
          });
        } catch (uploadErr) {
          console.warn('Failed to upload attachments for created dispute:', uploadErr);
          const uploadApiErr = uploadErr as ApiError;
          await refresh();
          return {
            success: true,
            disputeId: createdDisputeId,
            attachmentError:
              uploadApiErr?.detail ||
              uploadApiErr?.title ||
              'Hồ sơ khiếu nại đã được tạo nhưng tệp đính kèm tải lên thất bại. Vui lòng gửi lại tài liệu trong phần trao đổi khiếu nại.',
          };
        }
      }

      await refresh();
      return { success: true, disputeId: createdDisputeId };
    },
    adminReplySupportCase: async (params: {
      caseId: string;
      message: string;
      attachments?: AttachmentMetadata[];
    }): Promise<{ success: boolean; case?: SupportCase; error?: string }> => {
      if (!currentUser || currentUser.role !== 'SystemAdmin') {
        return { success: false, error: 'Chỉ Quản trị viên mới có quyền thực hiện thao tác này.' };
      }

      const validation = validateAndConvertAttachments(params.attachments);
      if (!validation.valid) {
        return { success: false, error: validation.error };
      }

      try {
        const updatedDto = await casesApiService.adminReplySupportCase(params.caseId, {
          message: params.message.trim(),
          attachments: validation.uploads.length > 0 ? validation.uploads : null,
        });

        await refresh();
        return { success: true, case: mapSupportCaseDetailDto(updatedDto) };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi gửi phản hồi hỗ trợ.',
        };
      }
    },
    adminUpdateSupportStatus: async (params: {
      caseId: string;
      status: string;
      message: string;
      attachments?: AttachmentMetadata[];
    }): Promise<{ success: boolean; case?: SupportCase; error?: string }> => {
      if (!currentUser || currentUser.role !== 'SystemAdmin') {
        return { success: false, error: 'Chỉ Quản trị viên mới có quyền thực hiện thao tác này.' };
      }

      const validation = validateAndConvertAttachments(params.attachments);
      if (!validation.valid) {
        return { success: false, error: validation.error };
      }

      try {
        const updatedDto = await casesApiService.updateAdminSupportStatus(params.caseId, {
          status: params.status,
          message: params.message.trim(),
          attachments: validation.uploads.length > 0 ? validation.uploads : null,
        });

        await refresh();
        return { success: true, case: mapSupportCaseDetailDto(updatedDto) };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật trạng thái yêu cầu.',
        };
      }
    },
    grantTransactionCorrectionAllowance: async (params: {
      caseId: string;
      adminPin: string;
      reason: string;
    }): Promise<{ success: boolean; case?: SupportCase; error?: string }> => {
      if (!currentUser || currentUser.role !== 'SystemAdmin') {
        return { success: false, error: 'Chỉ Quản trị viên mới có quyền cấp thêm lượt sửa.' };
      }

      if (!params.adminPin || !params.adminPin.trim()) {
        return { success: false, error: 'Mã PIN quản trị viên không được để trống.' };
      }

      if (!params.reason || !params.reason.trim()) {
        return { success: false, error: 'Lý do cấp thêm lượt sửa không được để trống.' };
      }

      try {
        const updatedDto = await casesApiService.grantAdminCorrection(params.caseId, {
          adminPin: params.adminPin.trim(),
          reason: params.reason.trim(),
        });

        await refresh();
        return { success: true, case: mapSupportCaseDetailDto(updatedDto) };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cấp thêm lượt sửa giao dịch.',
        };
      }
    },
    adminReplyDispute: async (params: {
      disputeId: string;
      message: string;
      attachments?: AttachmentMetadata[];
    }): Promise<{ success: boolean; dispute?: DisputeRecord; error?: string }> => {
      if (!currentUser || currentUser.role !== 'SystemAdmin') {
        return { success: false, error: 'Chỉ Quản trị viên mới có quyền thực hiện thao tác này.' };
      }

      const validation = validateAndConvertAttachments(params.attachments);
      if (!validation.valid) {
        return { success: false, error: validation.error };
      }

      try {
        const updatedDto = await casesApiService.adminReplyDispute(params.disputeId, {
          message: params.message.trim(),
          attachments: validation.uploads.length > 0 ? validation.uploads : null,
        });

        await refresh();
        return { success: true, dispute: mapDisputeDetailDto(updatedDto) };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi gửi phản hồi khiếu nại.',
        };
      }
    },
    adminUpdateDisputeStatus: async (params: {
      disputeId: string;
      status: string;
      message: string;
      attachments?: AttachmentMetadata[];
    }): Promise<{ success: boolean; dispute?: DisputeRecord; error?: string }> => {
      if (!currentUser || currentUser.role !== 'SystemAdmin') {
        return { success: false, error: 'Chỉ Quản trị viên mới có quyền thực hiện thao tác này.' };
      }

      const validation = validateAndConvertAttachments(params.attachments);
      if (!validation.valid) {
        return { success: false, error: validation.error };
      }

      try {
        const updatedDto = await casesApiService.updateAdminDisputeStatus(params.disputeId, {
          status: params.status,
          message: params.message.trim(),
          attachments: validation.uploads.length > 0 ? validation.uploads : null,
        });

        await refresh();
        return { success: true, dispute: mapDisputeDetailDto(updatedDto) };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật trạng thái khiếu nại.',
        };
      }
    },
    userReplySupportCase: async (params: {
      caseId: string;
      message: string;
      attachments?: AttachmentMetadata[];
    }): Promise<{ success: boolean; case?: SupportCase; error?: string }> => {
      if (!currentUser) return { success: false, error: 'Chưa đăng nhập' };

      const validation = validateAndConvertAttachments(params.attachments);
      if (!validation.valid) {
        return { success: false, error: validation.error };
      }

      try {
        const updatedDto = await casesApiService.replySupportCase(params.caseId, {
          message: params.message.trim() || 'Người dùng đã bổ sung thông tin.',
          attachments: validation.uploads.length > 0 ? validation.uploads : null,
        });

        await refresh();
        return { success: true, case: mapSupportCaseDetailDto(updatedDto) };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi phản hồi yêu cầu hỗ trợ.',
        };
      }
    },
    userReplyDispute: async (params: {
      disputeId: string;
      message: string;
      attachments?: AttachmentMetadata[];
    }): Promise<{ success: boolean; dispute?: DisputeRecord; error?: string }> => {
      if (!currentUser) return { success: false, error: 'Chưa đăng nhập' };

      const validation = validateAndConvertAttachments(params.attachments);
      if (!validation.valid) {
        return { success: false, error: validation.error };
      }

      try {
        const updatedDto = await casesApiService.replyDispute(params.disputeId, {
          message: params.message.trim() || 'Người dùng đã bổ sung bằng chứng.',
          attachments: validation.uploads.length > 0 ? validation.uploads : null,
        });

        await refresh();
        return { success: true, dispute: mapDisputeDetailDto(updatedDto) };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi phản hồi khiếu nại.',
        };
      }
    },
  };
}
