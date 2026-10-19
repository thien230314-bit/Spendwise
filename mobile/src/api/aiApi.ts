import { apiRequest } from './client';

export interface CreateAiConversationRequest {
  title?: string | null;
  contextType?: string;
  contextGroupId?: string | null;
}

export interface AiChatRequest {
  conversationId?: string | null;
  message: string;
  mode?: string;
  groupId?: string | null;
  attachmentIds?: string[] | null;
}

export interface AiMessageDto {
  id: string;
  role: string;
  content: string;
  createdAt: string;
}

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

export interface AiProposedActionDto {
  actionId: string;
  actionType: string;
  safeSummary: string;
  requiresPin: boolean;
  requiresEvidence: boolean;
  requiresExplicitConfirmation: boolean;
  expiresAt: string;
}

export interface AiChatResponse {
  conversationId: string;
  message: string;
  provider: string;
  isFallback: boolean;
  proposedAction?: AiProposedActionDto | null;
}

export const aiApi = {
  async getConversations(): Promise<AiConversationDto[]> {
    return apiRequest<AiConversationDto[]>('/ai/conversations', {
      authenticated: true,
    });
  },

  async createConversation(data?: CreateAiConversationRequest): Promise<AiConversationDto> {
    return apiRequest<AiConversationDto>('/ai/conversations', {
      method: 'POST',
      body: JSON.stringify(data || {}),
      authenticated: true,
    });
  },

  async getConversation(id: string): Promise<AiConversationDto> {
    return apiRequest<AiConversationDto>(`/ai/conversations/${id}`, {
      authenticated: true,
    });
  },

  async sendMessage(data: AiChatRequest): Promise<AiChatResponse> {
    return apiRequest<AiChatResponse>('/ai/chat', {
      method: 'POST',
      body: JSON.stringify(data),
      authenticated: true,
    });
  },
};
