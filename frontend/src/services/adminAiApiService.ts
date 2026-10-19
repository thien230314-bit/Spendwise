import { apiClient } from '../lib';
import { parseApiError } from '../lib/apiError';
import { getAccessToken } from '../lib/tokenStorage';

// ---------------------------------------------------------------------------
// Request & Response DTOs — Authoritative Admin AI contract
// ---------------------------------------------------------------------------

export interface CreateAdminAiConversationRequest {
  title?: string | null;
  contextType?: string;
  contextGroupId?: string | null;
}

export interface UpdateAdminAiConversationRequest {
  title?: string | null;
  isPinned?: boolean | null;
  contextType?: string | null;
  contextGroupId?: string | null;
}

export interface AdminAiChatRequest {
  conversationId?: string | null;
  message: string;
  mode?: string;
  groupId?: string | null;
  attachmentIds?: string[] | null;
}

export interface AdminAiMessageDto {
  id: string;
  role: 'User' | 'Assistant' | string;
  content: string;
  createdAt: string;
  attachments?: unknown[] | null;
}

export interface AdminAiConversationDto {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  isPinned: boolean;
  contextType: string;
  contextGroupId?: string | null;
  messages: AdminAiMessageDto[];
}

export interface AdminAiChatResponse {
  conversationId: string;
  message: string;
  provider: string;
  isFallback: boolean;
  proposedAction?: unknown | null;
}

export type AdminAiReportFormat = 'Word' | 'Excel';
export type AdminAiReportType = 'SystemOverview';

export interface AdminAiReportRequest {
  format: AdminAiReportFormat;
  reportType: AdminAiReportType;
  periodDays: number;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function getApiBaseUrl(): string {
  const url = import.meta.env.VITE_API_BASE_URL;
  if (!url) return '';
  return url.endsWith('/') ? url.slice(0, -1) : url;
}

function extractFilenameFromHeader(header: string | null, fallback: string): string {
  if (!header) return fallback;

  // e.g. filename*=UTF-8''filename.ext or filename="filename.ext" or filename=filename.ext
  const utf8Match = header.match(/filename\*=UTF-8''([^;]+)/i);
  if (utf8Match && utf8Match[1]) {
    try {
      return decodeURIComponent(utf8Match[1].trim());
    } catch {
      return utf8Match[1].trim();
    }
  }

  const standardMatch = header.match(/filename=["']?([^"';]+)["']?/i);
  if (standardMatch && standardMatch[1]) {
    return standardMatch[1].trim();
  }

  return fallback;
}

function generateDefaultReportFilename(format: AdminAiReportFormat): string {
  const now = new Date();
  const yyyy = now.getFullYear();
  const mm = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');
  const hh = String(now.getHours()).padStart(2, '0');
  const min = String(now.getMinutes()).padStart(2, '0');
  const ext = format === 'Word' ? 'docx' : 'xlsx';
  return `SpendWise-System-Overview-${yyyy}${mm}${dd}-${hh}${min}.${ext}`;
}

// ---------------------------------------------------------------------------
// Admin AI API Service
// ---------------------------------------------------------------------------

export const adminAiApiService = {
  /**
   * GET /api/admin/ai/conversations?search={text}
   */
  async getConversations(search?: string): Promise<AdminAiConversationDto[]> {
    const query = search ? `?search=${encodeURIComponent(search)}` : '';
    return apiClient.get<AdminAiConversationDto[]>(`/admin/ai/conversations${query}`);
  },

  /**
   * POST /api/admin/ai/conversations
   */
  async createConversation(data?: CreateAdminAiConversationRequest): Promise<AdminAiConversationDto> {
    return apiClient.post<AdminAiConversationDto>('/admin/ai/conversations', data || {});
  },

  /**
   * GET /api/admin/ai/conversations/{id}
   */
  async getConversationById(id: string): Promise<AdminAiConversationDto> {
    return apiClient.get<AdminAiConversationDto>(`/admin/ai/conversations/${id}`);
  },

  /**
   * PUT /api/admin/ai/conversations/{id}
   */
  async updateConversation(id: string, data: UpdateAdminAiConversationRequest): Promise<AdminAiConversationDto> {
    return apiClient.put<AdminAiConversationDto>(`/admin/ai/conversations/${id}`, data);
  },

  /**
   * DELETE /api/admin/ai/conversations/{id}
   */
  async deleteConversation(id: string): Promise<void> {
    return apiClient.delete<void>(`/admin/ai/conversations/${id}`);
  },

  /**
   * POST /api/admin/ai/chat
   */
  async chat(data: AdminAiChatRequest): Promise<AdminAiChatResponse> {
    return apiClient.post<AdminAiChatResponse>('/admin/ai/chat', {
      conversationId: data.conversationId ?? null,
      message: data.message,
      mode: data.mode ?? 'FinancialQuery',
      groupId: null,
      attachmentIds: null,
    });
  },

  /**
   * POST /api/admin/ai/reports/export
   * Authenticated POST with binary response, extraction of filename from Content-Disposition,
   * temporary object URL creation, trigger download, and cleanup.
   */
  async exportReport(requestData: AdminAiReportRequest): Promise<void> {
    const baseUrl = getApiBaseUrl();
    const url = `${baseUrl}/admin/ai/reports/export`;

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'X-Request-Id': `admin-export-${Date.now()}`,
    };

    const token = getAccessToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(url, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        format: requestData.format,
        reportType: requestData.reportType,
        periodDays: requestData.periodDays,
      }),
      credentials: 'include',
    });

    if (!response.ok) {
      throw await parseApiError(response);
    }

    const blob = await response.blob();
    const fallbackFilename = generateDefaultReportFilename(requestData.format);
    const disposition = response.headers.get('Content-Disposition') || response.headers.get('content-disposition');
    const filename = extractFilenameFromHeader(disposition, fallbackFilename);

    const blobUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = blobUrl;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(blobUrl);
  },
};
