// ---------------------------------------------------------------------------
// AI UI Types — client-side only representations
// ---------------------------------------------------------------------------

export interface AiAttachment {
  id: string;
  name: string;
  size: number;
  type: string;
  isImage: boolean;
  previewUrl?: string; // In-memory/session object URL or thumbnail
  textContent?: string; // Safely read text snippet for TXT, CSV, JSON (max 200KB)
  file?: File;
  backendId?: string;
}

export interface AiMessage {
  id: string;
  sender: 'user' | 'ai';
  text: string;
  time: string;
  createdAt: string;
  attachments?: AiAttachment[];
  proposedAction?: any;
  provider?: string;
  isFallback?: boolean;
}

export interface AiConversation {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  isPinned: boolean;
  contextType?: 'general' | 'group';
  contextId?: string;
  messages: AiMessage[];
}

// ---------------------------------------------------------------------------
// File Attachment Validation Constants
// ---------------------------------------------------------------------------

export const MAX_ATTACHMENTS = 5;
export const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB
export const MAX_TEXT_FILE_READ_BYTES = 200 * 1024; // 200 KB safe limit

const ALLOWED_IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/webp'];
const ALLOWED_DOC_TYPES = [
  'application/pdf',
  'text/plain',
  'text/csv',
  'application/json',
];

const ALLOWED_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp', '.pdf', '.txt', '.csv', '.json'];

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function validateAttachment(
  file: File,
  currentCount: number,
  locale: 'vi-VN' | 'en-US'
): { valid: boolean; error?: string; isImage: boolean } {
  if (currentCount >= MAX_ATTACHMENTS) {
    return {
      valid: false,
      error:
        locale === 'vi-VN'
          ? 'Tối đa 5 tệp đính kèm cho mỗi tin nhắn.'
          : 'Maximum 5 attachments per message.',
      isImage: false,
    };
  }

  const extension = '.' + file.name.split('.').pop()?.toLowerCase();
  const mimeType = file.type.toLowerCase();

  const isImageMime = ALLOWED_IMAGE_TYPES.includes(mimeType);
  const isImageExt = ['.png', '.jpg', '.jpeg', '.webp'].includes(extension);
  const isImage = isImageMime || isImageExt;

  const isDocMime = ALLOWED_DOC_TYPES.includes(mimeType);
  const isDocExt = ALLOWED_EXTENSIONS.includes(extension);

  if (!isImage && !isDocMime && !isDocExt) {
    return {
      valid: false,
      error:
        locale === 'vi-VN'
          ? 'Định dạng tệp không được hỗ trợ. Chỉ chấp nhận PNG, JPG, WEBP, PDF, TXT, CSV, JSON.'
          : 'Unsupported file type. Only PNG, JPG, WEBP, PDF, TXT, CSV, JSON are supported.',
      isImage: false,
    };
  }

  if (file.size > MAX_FILE_SIZE_BYTES) {
    return {
      valid: false,
      error:
        locale === 'vi-VN'
          ? `Dung lượng tệp không được vượt quá 10 MB: ${file.name}`
          : `File size must not exceed 10 MB: ${file.name}`,
      isImage,
    };
  }

  return { valid: true, isImage };
}
