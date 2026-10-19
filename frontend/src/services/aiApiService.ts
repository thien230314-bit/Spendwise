import { apiClient } from '../lib';

// ---------------------------------------------------------------------------
// Request DTOs — exact backend contract
// ---------------------------------------------------------------------------

export interface CreateAiConversationRequest {
  title?: string | null;
  contextType?: string;
  contextGroupId?: string | null;
}

export interface UpdateAiConversationRequest {
  title?: string | null;
  isPinned?: boolean | null;
  contextType?: string | null;
  contextGroupId?: string | null;
}

export interface AiChatRequest {
  conversationId?: string | null;
  message: string;
  mode?: string;
  groupId?: string | null;
  attachmentIds?: string[] | null;
}

// ---------------------------------------------------------------------------
// Attachment DTO — exact backend contract
// ---------------------------------------------------------------------------

export interface AiAttachmentDto {
  id: string;
  originalFileName: string;
  contentType: string;
  length: number;
  contentHash: string;
  createdAt: string;
}

// ---------------------------------------------------------------------------
// Message DTO — exact backend contract
// ---------------------------------------------------------------------------

export interface AiMessageDto {
  id: string;
  role: string;
  content: string;
  createdAt: string;
  attachments?: AiAttachmentDto[] | null;
}

// ---------------------------------------------------------------------------
// Conversation DTO — exact backend contract
// ---------------------------------------------------------------------------

export interface AiConversationDto {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  isPinned: boolean;
  contextType: string;
  contextGroupId?: string | null;
  messages: AiMessageDto[];
}

// ---------------------------------------------------------------------------
// Proposed Action DTO — exact backend contract
// ---------------------------------------------------------------------------

export interface AiProposedActionDto {
  actionId: string;
  actionType: string;
  safeSummary: string;
  requiresPin: boolean;
  requiresEvidence: boolean;
  requiresExplicitConfirmation: boolean;
  expiresAt: string;
}

// ---------------------------------------------------------------------------
// Execute Action Request — exact backend contract
// ---------------------------------------------------------------------------

export interface ExecuteAiActionRequest {
  securityPin?: string | null;
  evidenceIds?: string[] | null;
  explicitConfirmation: boolean;
}

// ---------------------------------------------------------------------------
// Action Result DTO — exact backend contract
// ---------------------------------------------------------------------------

export interface AiActionDto {
  id: string;
  actionType: string;
  safeSummary: string;
  status: string;
  expiresAt: string;
  executedAt?: string | null;
  failureCode?: string | null;
  result?: unknown;
}

// ---------------------------------------------------------------------------
// Chat Response — exact backend contract
// ---------------------------------------------------------------------------

export interface AiChatResponse {
  conversationId: string;
  message: string;
  provider: string;
  isFallback: boolean;
  proposedAction?: AiProposedActionDto | null;
}

// ---------------------------------------------------------------------------
// API Service
// ---------------------------------------------------------------------------

export const aiApiService = {
  /**
   * GET /api/ai/conversations
   */
  async getConversations(): Promise<AiConversationDto[]> {
    return apiClient.get<AiConversationDto[]>('/ai/conversations');
  },

  /**
   * POST /api/ai/conversations
   */
  async createConversation(data?: CreateAiConversationRequest): Promise<AiConversationDto> {
    return apiClient.post<AiConversationDto>('/ai/conversations', data || {});
  },

  /**
   * GET /api/ai/conversations/{id}
   */
  async getConversationById(id: string): Promise<AiConversationDto> {
    return apiClient.get<AiConversationDto>(`/ai/conversations/${id}`);
  },

  /**
   * PUT /api/ai/conversations/{id}
   */
  async updateConversation(id: string, data: UpdateAiConversationRequest): Promise<AiConversationDto> {
    return apiClient.put<AiConversationDto>(`/ai/conversations/${id}`, data);
  },

  /**
   * DELETE /api/ai/conversations/{id}
   */
  async deleteConversation(id: string): Promise<void> {
    return apiClient.delete<void>(`/ai/conversations/${id}`);
  },

  /**
   * POST /api/ai/chat
   */
  async chat(data: AiChatRequest): Promise<AiChatResponse> {
    return apiClient.post<AiChatResponse>('/ai/chat', data);
  },

  /**
   * POST /api/ai/attachments
   */
  async uploadAttachment(file: File): Promise<AiAttachmentDto> {
    const formData = new FormData();
    formData.append('file', file);
    return apiClient.upload<AiAttachmentDto>('/ai/attachments', formData);
  },

  /**
   * GET /api/ai/attachments/{id}
   */
  async getAttachmentBlob(id: string): Promise<Blob> {
    return apiClient.getBlob(`/ai/attachments/${id}`);
  },

  /**
   * GET /api/ai/actions/{actionId}
   */
  async getActionById(actionId: string): Promise<AiActionDto> {
    return apiClient.get<AiActionDto>(`/ai/actions/${actionId}`);
  },

  /**
   * POST /api/ai/actions/{actionId}/execute
   */
  async executeAction(actionId: string, data: ExecuteAiActionRequest): Promise<AiActionDto> {
    return apiClient.post<AiActionDto>(`/ai/actions/${actionId}/execute`, data);
  },

  /**
   * POST /api/ai/actions/{actionId}/cancel
   */
  async cancelAction(actionId: string): Promise<AiActionDto> {
    return apiClient.post<AiActionDto>(`/ai/actions/${actionId}/cancel`, {});
  },
};
