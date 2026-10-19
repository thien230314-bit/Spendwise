import React, { useState, useRef, useEffect, useMemo } from 'react';
import {
  Sparkles,
  X,
  Send,
  Bot,
  Plus,
  History,
  Search,
  Pin,
  PinOff,
  Edit3,
  Trash2,
  MoreVertical,
  ChevronLeft,
  MessageSquare,
  Zap,
  Paperclip,
  FileText,
  UploadCloud,
  AlertCircle,
  ShieldCheck,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { cn } from '../../utils/cn';
import { useSecurityPinChallenge } from '../../providers/SecurityPinChallengeProvider';
import { isApiError } from '../../lib/apiError';
import { aiApiService, AiProposedActionDto } from '../../services/aiApiService';
import { financialApiService } from '../../services/financialApiService';
import {
  AiAttachment,
  AiMessage,
  AiConversation,
  validateAttachment,
  formatFileSize,
} from '../../features/ai/aiService';

export const FloatingAiAssistant: React.FC = () => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const [isOpen, setIsOpen] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const [historySearch, setHistorySearch] = useState('');
  const [inputValue, setInputValue] = useState('');
  const [isTyping, setIsTyping] = useState(false);

  // Attachments State
  const [pendingAttachments, setPendingAttachments] = useState<AiAttachment[]>([]);
  const [attachmentError, setAttachmentError] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Active Menu / Modals inside panel
  const [menuOpenConvId, setMenuOpenConvId] = useState<string | null>(null);
  const [deleteConfirmConvId, setDeleteConfirmConvId] = useState<string | null>(null);
  const [renamingConvId, setRenamingConvId] = useState<string | null>(null);
  const [renameInput, setRenameInput] = useState('');

  const { challengePinWithValue } = useSecurityPinChallenge();
  const [executingActionType, setExecutingActionType] = useState<string | null>(null);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const renameInputRef = useRef<HTMLInputElement>(null);

  // File Attachment Processing
  const processFiles = async (files: FileList | File[]) => {
    setAttachmentError(null);
    const fileArray = Array.from(files);
    if (fileArray.length === 0) return;

    const currentCount = pendingAttachments.length;
    if (currentCount + fileArray.length > 5) {
      setAttachmentError(t('ai.maxFilesExceeded'));
      return;
    }

    const newAttachments: AiAttachment[] = [];

    for (const file of fileArray) {
      const validation = validateAttachment(file, currentCount + newAttachments.length, locale);
      if (!validation.valid) {
        setAttachmentError(validation.error || t('ai.unsupportedFileType'));
        return;
      }

      const id = `att-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      let previewUrl: string | undefined = undefined;
      let textContent: string | undefined = undefined;

      if (validation.isImage) {
        previewUrl = URL.createObjectURL(file);
      } else {
        const ext = '.' + file.name.split('.').pop()?.toLowerCase();
        if (['.txt', '.csv', '.json'].includes(ext) && file.size <= 200 * 1024) {
          try {
            textContent = await new Promise<string>((resolve) => {
              const reader = new FileReader();
              reader.onload = () => resolve((reader.result as string) || '');
              reader.onerror = () => resolve('');
              reader.readAsText(file.slice(0, 200 * 1024));
            });
          } catch {
            textContent = undefined;
          }
        }
      }

      newAttachments.push({
        id,
        name: file.name,
        size: file.size,
        type: file.type || file.name.split('.').pop()?.toUpperCase() || 'FILE',
        isImage: validation.isImage,
        previewUrl,
        textContent,
        file,
      });
    }

    setPendingAttachments((prev) => [...prev, ...newAttachments]);
  };

  const handleRemovePendingAttachment = (id: string) => {
    setPendingAttachments((prev) => {
      const target = prev.find((a) => a.id === id);
      if (target?.previewUrl && target.previewUrl.startsWith('blob:')) {
        URL.revokeObjectURL(target.previewUrl);
      }
      return prev.filter((a) => a.id !== id);
    });
  };

  // Revoke blob URLs on unmount / cleanup
  useEffect(() => {
    return () => {
      pendingAttachments.forEach((a) => {
        if (a.previewUrl && a.previewUrl.startsWith('blob:')) {
          URL.revokeObjectURL(a.previewUrl);
        }
      });
    };
  }, [pendingAttachments]);

  const getInitialGreeting = (loc: string): string => {
    const rawName = currentUser?.name || 'User';
    const parts = rawName.split(' ');
    const shortName = parts.length > 2 ? parts.slice(-2).join(' ') : rawName;
    return loc === 'vi-VN'
      ? `Xin chào ${shortName} 👋\nTôi có thể giúp bạn kiểm tra chi tiêu, ngân sách, quỹ nhóm và hướng dẫn sử dụng SpendWise.`
      : `Hello ${shortName} 👋\nI can help you check spending, budgets, group funds, and guide you through SpendWise.`;
  };

  const createNewGreetingMessage = (loc: string): AiMessage => ({
    id: `greeting-${Date.now()}`,
    sender: 'ai',
    text: getInitialGreeting(loc),
    time: loc === 'vi-VN' ? 'Vừa xong' : 'Just now',
    createdAt: new Date().toISOString(),
  });

  const createDefaultConversation = (loc: string): AiConversation => {
    const now = new Date().toISOString();
    return {
      id: `conv-${Date.now()}`,
      title: loc === 'vi-VN' ? 'Cuộc trò chuyện mới' : 'New conversation',
      createdAt: now,
      updatedAt: now,
      isPinned: false,
      contextType: 'general',
      messages: [createNewGreetingMessage(loc)],
    };
  };

  // State initialized with default conversation, then populated from backend
  const [conversations, setConversations] = useState<AiConversation[]>([createDefaultConversation(locale)]);
  const [activeConvId, setActiveConvId] = useState<string>(() => conversations[0]?.id || `conv-${Date.now()}`);

  // Load conversations from backend
  useEffect(() => {
    let isCancelled = false;
    const loadBackendConversations = async () => {
      if (!currentUser || currentUser.role === 'SystemAdmin') return;
      try {
        const dtos = await aiApiService.getConversations();
        if (isCancelled) return;
        if (dtos && dtos.length > 0) {
          const mapped: AiConversation[] = dtos.map((d) => ({
            id: d.id,
            title: d.title,
            createdAt: d.createdAt,
            updatedAt: d.updatedAt,
            isPinned: Boolean(d.isPinned),
            contextType: (d.contextType as any) || 'general',
            contextId: d.contextGroupId, // backend field: contextGroupId → UI: contextId
            messages: (d.messages && d.messages.length > 0)
              ? d.messages.map((m) => ({
                  id: m.id,
                  sender: m.role === 'Assistant' ? ('ai' as const) : ('user' as const), // role→sender
                  text: m.content, // content→text
                  time: m.createdAt ? new Date(m.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : 'Vừa xong',
                  createdAt: m.createdAt,
                  attachments: m.attachments
                    ? m.attachments.map((a) => ({
                        id: a.id,
                        name: a.originalFileName,       // originalFileName→name
                        size: a.length,                  // length→size
                        type: a.contentType,
                        isImage: a.contentType.startsWith('image/'), // computed from contentType
                      }))
                    : undefined,
                }))
              : [createNewGreetingMessage(locale)],
          }));
          setConversations(mapped);
          setActiveConvId(mapped[0]?.id || '');
        } else {
          const fresh = [createDefaultConversation(locale)];
          setConversations(fresh);
          setActiveConvId(fresh[0].id);
        }
      } catch (err) {
        console.warn('Failed to load AI conversations from backend:', err);
        const fresh = [createDefaultConversation(locale)];
        setConversations(fresh);
        setActiveConvId(fresh[0].id);
      }
    };

    loadBackendConversations();
    return () => {
      isCancelled = true;
    };
  }, [currentUser?.id, locale]);

  // Ensure active conversation always exists in list
  useEffect(() => {
    if (!conversations.some((c) => c.id === activeConvId)) {
      if (conversations.length > 0) {
        setActiveConvId(conversations[0].id);
      } else {
        const fresh = createDefaultConversation(locale);
        setConversations([fresh]);
        setActiveConvId(fresh.id);
      }
    }
  }, [conversations, activeConvId, locale]);

  const activeConversation = useMemo(() => {
    return (
      conversations.find((c) => c.id === activeConvId) ||
      conversations[0] ||
      createDefaultConversation(locale)
    );
  }, [conversations, activeConvId, locale]);

  // Focus rename input on open
  useEffect(() => {
    if (renamingConvId) {
      setTimeout(() => renameInputRef.current?.focus(), 50);
    }
  }, [renamingConvId]);

  const suggestedPrompts = useMemo(() => {
    return [
      {
        label: locale === 'vi-VN' ? 'Tháng này tôi đã chi bao nhiêu?' : 'How much did I spend this month?',
        query: locale === 'vi-VN' ? 'Tháng này tôi đã chi bao nhiêu?' : 'How much did I spend this month?',
      },
      {
        label: locale === 'vi-VN' ? 'Tôi còn bao nhiêu ngân sách ăn uống?' : 'How much dining budget left?',
        query: locale === 'vi-VN' ? 'Tôi còn bao nhiêu ngân sách ăn uống?' : 'How much dining budget left?',
      },
      {
        label: locale === 'vi-VN' ? 'Tôi còn phải đóng bao nhiêu tiền quỹ nhóm?' : 'How much do I still need to contribute?',
        query: locale === 'vi-VN' ? 'Tôi còn phải đóng bao nhiêu tiền quỹ nhóm?' : 'How much do I still need to contribute?',
      },
      {
        label: locale === 'vi-VN' ? 'Tình hình quỹ các nhóm của tôi?' : 'What is the status of my group funds?',
        query: locale === 'vi-VN' ? 'Tình hình quỹ các nhóm của tôi?' : 'What is the status of my group funds?',
      },
      {
        label: locale === 'vi-VN' ? 'Làm sao chuyển tiền?' : 'How to transfer money?',
        query: locale === 'vi-VN' ? 'Làm sao chuyển tiền?' : 'How to transfer money?',
      },
      {
        label: locale === 'vi-VN' ? 'Quỹ nhóm hoạt động thế nào?' : 'How do group funds work?',
        query: locale === 'vi-VN' ? 'Quỹ nhóm hoạt động thế nào?' : 'How do group funds work?',
      },
    ];
  }, [locale]);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    if (isOpen && !showHistory) {
      scrollToBottom();
    }
  }, [activeConversation.messages, isOpen, isTyping, showHistory]);

  const deriveShortTitle = (query: string, currentTitle: string): string => {
    // If title was already custom edited, do not override
    const defaultVI = 'Cuộc trò chuyện mới';
    const defaultEN = 'New conversation';
    if (currentTitle !== defaultVI && currentTitle !== defaultEN) {
      return currentTitle;
    }

    const q = query.trim().toLowerCase();
    if (q.includes('chi bao nhiêu') || q.includes('spend this month') || q.includes('tổng chi')) {
      return locale === 'vi-VN' ? 'Chi tiêu tháng này' : 'Monthly spending';
    }
    if (q.includes('ngân sách') || q.includes('budget')) {
      return locale === 'vi-VN' ? 'Ngân sách ăn uống' : 'Dining budget';
    }
    if (q.includes('đóng quỹ') || q.includes('contribute') || q.includes('phải đóng') || q.includes('đợt góp')) {
      return locale === 'vi-VN' ? 'Đóng góp quỹ nhóm' : 'Fund contributions';
    }
    if (q.includes('chuyển tiền') || q.includes('transfer')) {
      return locale === 'vi-VN' ? 'Hướng dẫn chuyển tiền' : 'Transfer guide';
    }
    if (q.includes('quỹ chung') || q.includes('nhóm') || q.includes('group')) {
      return locale === 'vi-VN' ? 'Quản lý quỹ nhóm' : 'Shared group fund';
    }
    if (q.includes('tiết kiệm') || q.includes('savings') || q.includes('goal')) {
      return locale === 'vi-VN' ? 'Mục tiêu tiết kiệm' : 'Savings goals';
    }
    if (q.includes('ví') || q.includes('wallet') || q.includes('số dư')) {
      return locale === 'vi-VN' ? 'Kiểm tra số dư ví' : 'Wallet balances';
    }

    // Fallback: clean and truncate query
    const clean = query.replace(/[?.,!]/g, '').trim();
    if (clean.length > 28) {
      return clean.slice(0, 28) + '...';
    }
    return clean || (locale === 'vi-VN' ? 'Cuộc trò chuyện' : 'Conversation');
  };

  const handleSendMessage = async (textToSend?: string) => {
    const query = (textToSend || inputValue).trim();
    if (!query && pendingAttachments.length === 0) return;

    const now = new Date();
    const currentAttachments = [...pendingAttachments];
    setPendingAttachments([]);
    setAttachmentError(null);

    const userMsg: AiMessage = {
      id: `user-${Date.now()}`,
      sender: 'user',
      text: query || (locale === 'vi-VN' ? '[Đã gửi tệp đính kèm]' : '[Sent attachments]'),
      time: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      createdAt: now.toISOString(),
      attachments: currentAttachments.length > 0 ? currentAttachments : undefined,
    };

    // Calculate updated title if this is the first user message
    const isFirstUserMessage = !activeConversation.messages.some((m) => m.sender === 'user');
    const newTitle = isFirstUserMessage
      ? deriveShortTitle(query || 'Tệp đính kèm', activeConversation.title)
      : activeConversation.title;

    setConversations((prev) =>
      prev.map((c) => {
        if (c.id === activeConvId) {
          return {
            ...c,
            title: newTitle,
            updatedAt: now.toISOString(),
            messages: [...c.messages, userMsg],
          };
        }
        return c;
      })
    );

    if (!textToSend) setInputValue('');
    setIsTyping(true);

    try {
      // 1. Upload any attachments first to get attachment IDs from backend.
      //    If ANY upload fails, STOP and show an error — do NOT send silently without the intended file.
      const uploadedAttachmentIds: string[] = [];
      for (const att of currentAttachments) {
        if (att.file) {
          try {
            const uploaded = await aiApiService.uploadAttachment(att.file);
            if (uploaded?.id) {
              uploadedAttachmentIds.push(uploaded.id);
            }
          } catch (uploadErr) {
            // Attachment upload failure — abort the entire send
            setIsTyping(false);
            const uploadErrText = isApiError(uploadErr)
              ? (uploadErr.detail || uploadErr.title || att.file.name)
              : att.file.name;
            setAttachmentError(
              locale === 'vi-VN'
                ? `Không thể tải lên tệp đính kèm: ${uploadErrText}. Vui lòng thử lại.`
                : `Failed to upload attachment: ${uploadErrText}. Please try again.`
            );
            // Remove the optimistic user message since we aborted
            setConversations((prev) =>
              prev.map((c) => {
                if (c.id === activeConvId) {
                  return {
                    ...c,
                    messages: c.messages.filter((m) => m.id !== userMsg.id),
                  };
                }
                return c;
              })
            );
            return;
          }
        }
      }

      const response = await aiApiService.chat({
        conversationId: activeConvId.startsWith('conv-') ? undefined : activeConvId,
        message: query,
        attachmentIds: uploadedAttachmentIds.length > 0 ? uploadedAttachmentIds : undefined,
        // Note: clientContext is NOT a backend field — omitted
      });

      const aiNow = new Date();
      const aiMsg: AiMessage = {
        id: `ai-${Date.now()}`,
        sender: 'ai',
        text: response.message,
        time: aiNow.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        createdAt: aiNow.toISOString(),
        proposedAction: response.proposedAction || undefined,
        provider: response.provider,
        isFallback: response.isFallback,
      };

      setConversations((prev) =>
        prev.map((c) => {
          if (c.id === activeConvId) {
            return {
              ...c,
              id: response.conversationId || c.id,
              title: newTitle,
              updatedAt: aiNow.toISOString(),
              messages: [...c.messages, aiMsg],
            };
          }
          return c;
        })
      );
      if (response.conversationId && response.conversationId !== activeConvId) {
        setActiveConvId(response.conversationId);
      }
    } catch (err) {
      console.error('Backend AI chat error:', err);
      const errorMsgText = isApiError(err)
        ? (err.detail || err.title || 'Dịch vụ trợ lý AI hiện không khả dụng.')
        : (locale === 'vi-VN' ? 'Dịch vụ trợ lý AI hiện không khả dụng. Vui lòng thử lại sau.' : 'AI Assistant service is currently unavailable. Please try again later.');

      const aiNow = new Date();
      const aiMsg: AiMessage = {
        id: `ai-err-${Date.now()}`,
        sender: 'ai',
        text: `⚠️ ${errorMsgText}`,
        time: aiNow.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        createdAt: aiNow.toISOString(),
      };

      setConversations((prev) =>
        prev.map((c) => {
          if (c.id === activeConvId) {
            return {
              ...c,
              updatedAt: aiNow.toISOString(),
              messages: [...c.messages, aiMsg],
            };
          }
          return c;
        })
      );
    } finally {
      setIsTyping(false);
    }
  };

  const handleExecuteAction = async (action: AiProposedActionDto) => {
    let pin: string | null = null;
    if (action.requiresPin) {
      pin = await challengePinWithValue({
        title: locale === 'vi-VN' ? 'Xác thực mã PIN bảo mật' : 'Security PIN Verification',
        description: action.safeSummary || (locale === 'vi-VN' ? 'Xác thực để thực hiện hành động AI' : 'Authenticate to execute AI action'),
      });
      if (!pin) return;
    }

    // Evidence upload step — only for actions that require it
    let evidenceIds: string[] | undefined;
    if (action.requiresEvidence) {
      // Prompt user to select evidence files (1–3 files, max 2 MB each, JPEG/PNG/WEBP/PDF)
      const evidenceFiles = await new Promise<File[] | null>((resolve) => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/jpeg,image/png,image/webp,application/pdf,.jpg,.jpeg,.png,.webp,.pdf';
        input.multiple = true;
        input.onchange = () => {
          const files = input.files ? Array.from(input.files) : [];
          resolve(files.length > 0 ? files : null);
        };
        input.oncancel = () => resolve(null);
        // Some browsers don't fire oncancel; handle via dialog close
        document.body.appendChild(input);
        input.click();
        document.body.removeChild(input);
      });

      if (!evidenceFiles || evidenceFiles.length === 0) {
        return; // User cancelled evidence selection
      }

      if (evidenceFiles.length > 3) {
        const now = new Date();
        const errMsg: AiMessage = {
          id: `ai-action-err-${Date.now()}`,
          sender: 'ai',
          text: locale === 'vi-VN'
            ? '❌ Chỉ được đính kèm tối đa 3 tệp bằng chứng.'
            : '❌ Maximum 3 evidence files allowed.',
          time: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          createdAt: now.toISOString(),
        };
        setConversations((prev) =>
          prev.map((c) => c.id === activeConvId ? { ...c, updatedAt: now.toISOString(), messages: [...c.messages, errMsg] } : c)
        );
        return;
      }

      const MAX_EVIDENCE_SIZE = 2 * 1024 * 1024; // 2 MB
      for (const f of evidenceFiles) {
        if (f.size > MAX_EVIDENCE_SIZE) {
          const now = new Date();
          const errMsg: AiMessage = {
            id: `ai-action-err-${Date.now()}`,
            sender: 'ai',
            text: locale === 'vi-VN'
              ? `❌ Tệp bằng chứng "${f.name}" vượt quá giới hạn 2 MB.`
              : `❌ Evidence file "${f.name}" exceeds the 2 MB limit.`,
            time: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            createdAt: now.toISOString(),
          };
          setConversations((prev) =>
            prev.map((c) => c.id === activeConvId ? { ...c, updatedAt: now.toISOString(), messages: [...c.messages, errMsg] } : c)
          );
          return;
        }
      }

      // Upload each evidence file via POST /api/evidence; collect EvidenceDto.id[]
      const uploadedEvidenceIds: string[] = [];
      for (const f of evidenceFiles) {
        try {
          const evidenceDto = await financialApiService.uploadEvidence(f);
          if (evidenceDto?.id) {
            uploadedEvidenceIds.push(evidenceDto.id);
          }
        } catch (evidenceErr) {
          const now = new Date();
          const uploadErrText = isApiError(evidenceErr)
            ? (evidenceErr.detail || evidenceErr.title || f.name)
            : f.name;
          const errMsg: AiMessage = {
            id: `ai-action-err-${Date.now()}`,
            sender: 'ai',
            text: locale === 'vi-VN'
              ? `❌ Không thể tải lên tệp bằng chứng: ${uploadErrText}.`
              : `❌ Failed to upload evidence file: ${uploadErrText}.`,
            time: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            createdAt: now.toISOString(),
          };
          setConversations((prev) =>
            prev.map((c) => c.id === activeConvId ? { ...c, updatedAt: now.toISOString(), messages: [...c.messages, errMsg] } : c)
          );
          return;
        }
      }
      evidenceIds = uploadedEvidenceIds;
    }

    setExecutingActionType(action.actionType);
    try {
      // Use action.actionId (exact backend field: actionId)
      const res = await aiApiService.executeAction(action.actionId, {
        securityPin: pin,
        evidenceIds: evidenceIds || null,
        explicitConfirmation: true,
      });

      const now = new Date();
      const resultMsg: AiMessage = {
        id: `ai-action-${Date.now()}`,
        sender: 'ai',
        // Use res.safeSummary (backend field: safeSummary, NOT summary)
        text: res.safeSummary || (locale === 'vi-VN' ? '✅ Hành động đã được thực hiện thành công.' : '✅ Action executed successfully.'),
        time: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        createdAt: now.toISOString(),
      };

      setConversations((prev) =>
        prev.map((c) => {
          if (c.id === activeConvId) {
            return {
              ...c,
              updatedAt: now.toISOString(),
              messages: [...c.messages, resultMsg],
            };
          }
          return c;
        })
      );
    } catch (err: any) {
      const errorText = isApiError(err) ? (err.detail || err.title) : (err?.message || (locale === 'vi-VN' ? 'Không thể thực hiện hành động.' : 'Failed to execute action.'));
      const now = new Date();
      const errorMsg: AiMessage = {
        id: `ai-action-err-${Date.now()}`,
        sender: 'ai',
        text: `❌ ${errorText}`,
        time: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        createdAt: now.toISOString(),
      };
      setConversations((prev) =>
        prev.map((c) => {
          if (c.id === activeConvId) {
            return {
              ...c,
              updatedAt: now.toISOString(),
              messages: [...c.messages, errorMsg],
            };
          }
          return c;
        })
      );
    } finally {
      setExecutingActionType(null);
    }
  };

  const handleCancelAction = async (action: AiProposedActionDto) => {
    try {
      await aiApiService.cancelAction(action.actionId); // Use action.actionId (exact backend field: actionId)
      const now = new Date();
      const cancelMsg: AiMessage = {
        id: `ai-action-cancel-${Date.now()}`,
        sender: 'ai',
        text: locale === 'vi-VN' ? 'ℹ️ Hành động đề xuất đã bị hủy.' : 'ℹ️ Proposed action was cancelled.',
        time: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        createdAt: now.toISOString(),
      };
      setConversations((prev) =>
        prev.map((c) => {
          if (c.id === activeConvId) {
            return {
              ...c,
              updatedAt: now.toISOString(),
              messages: [...c.messages, cancelMsg],
            };
          }
          return c;
        })
      );
    } catch (err: any) {
      console.warn('Failed to cancel action:', err);
    }
  };

  const handleCreateNewChat = async () => {
    try {
      const created = await aiApiService.createConversation({
        title: locale === 'vi-VN' ? 'Cuộc trò chuyện mới' : 'New conversation',
        contextType: 'general',
      });
      const newConv: AiConversation = {
        id: created.id,
        title: created.title,
        createdAt: created.createdAt,
        updatedAt: created.updatedAt,
        isPinned: Boolean(created.isPinned),
        contextType: 'general',
        messages: [createNewGreetingMessage(locale)],
      };
      setConversations((prev) => [newConv, ...prev]);
      setActiveConvId(newConv.id);
    } catch {
      const newConv = createDefaultConversation(locale);
      setConversations((prev) => [newConv, ...prev]);
      setActiveConvId(newConv.id);
    }
    setShowHistory(false);
    setInputValue('');
    setPendingAttachments([]);
    setAttachmentError(null);
  };

  const handleTogglePin = async (convId: string, e?: React.MouseEvent) => {
    e?.stopPropagation();
    const conv = conversations.find((c) => c.id === convId);
    if (!conv) return;
    const nextPinned = !conv.isPinned;
    setConversations((prev) =>
      prev.map((c) => (c.id === convId ? { ...c, isPinned: nextPinned } : c))
    );
    setMenuOpenConvId(null);
    try {
      await aiApiService.updateConversation(convId, { isPinned: nextPinned });
    } catch (err) {
      console.warn('Failed to update pinned state on backend:', err);
    }
  };

  const handleStartRename = (conv: AiConversation, e?: React.MouseEvent) => {
    e?.stopPropagation();
    setRenamingConvId(conv.id);
    setRenameInput(conv.title);
    setMenuOpenConvId(null);
  };

  const handleSaveRename = async () => {
    if (!renamingConvId) return;
    const trimmed = renameInput.trim();
    if (trimmed) {
      setConversations((prev) =>
        prev.map((c) => (c.id === renamingConvId ? { ...c, title: trimmed } : c))
      );
      try {
        await aiApiService.updateConversation(renamingConvId, { title: trimmed });
      } catch (err) {
        console.warn('Failed to update conversation title on backend:', err);
      }
    }
    setRenamingConvId(null);
    setRenameInput('');
  };

  const handleConfirmDelete = async () => {
    if (!deleteConfirmConvId) return;
    const deletingId = deleteConfirmConvId;
    const remaining = conversations.filter((c) => c.id !== deletingId);

    if (remaining.length === 0) {
      const fresh = createDefaultConversation(locale);
      setConversations([fresh]);
      setActiveConvId(fresh.id);
    } else {
      setConversations(remaining);
      if (activeConvId === deletingId) {
        setActiveConvId(remaining[0].id);
      }
    }
    setDeleteConfirmConvId(null);
    setMenuOpenConvId(null);
    try {
      await aiApiService.deleteConversation(deletingId);
    } catch (err) {
      console.warn('Failed to delete conversation on backend:', err);
    }
  };

  const handleSelectConversation = (id: string) => {
    setActiveConvId(id);
    setShowHistory(false);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSendMessage();
    }
  };

  // Grouping conversations for the history drawer
  const filteredConversations = useMemo(() => {
    if (!historySearch.trim()) return conversations;
    const q = historySearch.toLowerCase();
    return conversations.filter(
      (c) =>
        c.title.toLowerCase().includes(q) ||
        c.messages.some((m) => m.text.toLowerCase().includes(q))
    );
  }, [conversations, historySearch]);

  const historySections = useMemo(() => {
    const now = new Date();
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
    const startOfYesterday = startOfToday - 24 * 60 * 60 * 1000;
    const startOf7Days = startOfToday - 7 * 24 * 60 * 1000;

    const pinned: AiConversation[] = [];
    const today: AiConversation[] = [];
    const yesterday: AiConversation[] = [];
    const previous7Days: AiConversation[] = [];
    const older: AiConversation[] = [];

    // Sort by updatedAt desc
    const sorted = [...filteredConversations].sort(
      (a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
    );

    sorted.forEach((c) => {
      if (c.isPinned) {
        pinned.push(c);
        return;
      }
      const time = new Date(c.updatedAt).getTime();
      if (time >= startOfToday) {
        today.push(c);
      } else if (time >= startOfYesterday) {
        yesterday.push(c);
      } else if (time >= startOf7Days) {
        previous7Days.push(c);
      } else {
        older.push(c);
      }
    });

    return [
      { key: 'pinned', title: t('ai.pinned'), items: pinned },
      { key: 'today', title: t('ai.today'), items: today },
      { key: 'yesterday', title: t('ai.yesterday'), items: yesterday },
      { key: 'prev7', title: t('ai.previous7Days'), items: previous7Days },
      { key: 'older', title: t('ai.older'), items: older },
    ].filter((s) => s.items.length > 0);
  }, [filteredConversations, t]);

  const formatHistoryTime = (isoString: string): string => {
    try {
      const d = new Date(isoString);
      if (isNaN(d.getTime())) return '';
      const now = new Date();
      const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
      const time = d.getTime();

      if (time >= startOfToday) {
        return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
      }
      return d.toLocaleDateString(locale, { month: 'numeric', day: 'numeric' });
    } catch {
      return '';
    }
  };

  if (currentUser?.role === 'SystemAdmin') {
    return null;
  }

  return (
    <>
      {/* Floating Trigger Button: HIDDEN when AI panel is OPEN */}
      {!isOpen && (
        <div className="fixed bottom-6 right-6 z-40 animate-in fade-in zoom-in-95 duration-200">
          <button
            type="button"
            id="spendwise-ai-trigger"
            onClick={() => setIsOpen(true)}
            className="group relative flex items-center gap-2.5 px-4 py-3 rounded-full bg-emerald-600 dark:bg-[#0F172A] text-white hover:bg-emerald-700 dark:hover:bg-slate-800 hover:scale-105 shadow-xl shadow-emerald-900/20 dark:shadow-slate-900/40 transition-all duration-200 cursor-pointer select-none focus:outline-none focus:ring-2 focus:ring-emerald-500/50 border border-emerald-500/40 dark:border-slate-700/60"
            aria-label="SpendWise AI Assistant"
          >
            {/* Animated subtle glow */}
            <span className="absolute -inset-0.5 rounded-full bg-gradient-to-r from-emerald-400 to-teal-400 opacity-40 blur-xs group-hover:opacity-75 transition duration-300 pointer-events-none" />

            {/* Button Content */}
            <div className="relative flex items-center gap-2">
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-white/20 dark:bg-emerald-500 text-white shadow-xs">
                <Sparkles className="h-4 w-4" />
              </div>
              <span className="text-sm font-bold tracking-tight">{t('ai.askAi')}</span>
              <span className="flex h-2 w-2 rounded-full bg-emerald-300 dark:bg-emerald-400 animate-pulse" />
            </div>
          </button>
        </div>
      )}

      {/* Floating AI Chat Window / Panel */}
      {isOpen && (
        <div
          id="spendwise-ai-window"
          onDragEnter={(e) => {
            e.preventDefault();
            e.stopPropagation();
            setIsDragging(true);
          }}
          onDragOver={(e) => {
            e.preventDefault();
            e.stopPropagation();
            if (!isDragging) setIsDragging(true);
          }}
          onDragLeave={(e) => {
            e.preventDefault();
            e.stopPropagation();
            if (e.currentTarget.contains(e.relatedTarget as Node)) return;
            setIsDragging(false);
          }}
          onDrop={(e) => {
            e.preventDefault();
            e.stopPropagation();
            setIsDragging(false);
            if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
              processFiles(e.dataTransfer.files);
            }
          }}
          className="fixed bottom-6 right-6 z-50 w-[92vw] sm:w-[440px] h-[600px] max-h-[calc(100vh-80px)] rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-2xl flex flex-col overflow-hidden animate-in slide-from-bottom-5 fade-in duration-200"
        >
          {/* Drag and Drop Active Overlay */}
          {isDragging && (
            <div className="absolute inset-0 z-50 bg-emerald-600/20 dark:bg-emerald-950/80 backdrop-blur-xs border-2 border-dashed border-emerald-500 rounded-3xl flex flex-col items-center justify-center p-6 text-center pointer-events-none animate-in fade-in duration-150">
              <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-emerald-500 text-white shadow-lg mb-3">
                <UploadCloud className="h-7 w-7 animate-bounce" />
              </div>
              <h4 className="text-base font-bold text-slate-900 dark:text-white">
                {t('ai.dropFilesHere')}
              </h4>
              <p className="text-xs text-slate-700 dark:text-slate-300 mt-1 font-medium">
                PNG, JPG, WEBP, PDF, TXT, CSV, JSON (max 10 MB)
              </p>
            </div>
          )}

          {/* Header - EXACT visual layout preserved */}
          <div className="flex items-center justify-between px-5 py-3.5 bg-emerald-600 dark:bg-[#0F172A] text-white border-b border-emerald-700/40 dark:border-white/10 shrink-0">
            <div className="flex items-center gap-3">
              <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-white/20 dark:bg-emerald-500 text-white shadow-xs">
                <Bot className="h-5 w-5" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-sm font-bold text-white leading-tight">
                    {t('ai.name')}
                  </h3>
                  <span className="text-[10px] px-1.5 py-0.2 rounded-md bg-white/20 dark:bg-emerald-500/20 text-white dark:text-emerald-400 font-bold uppercase tracking-wider">
                    {locale === 'vi-VN' ? 'Trợ lý' : 'Assistant'}
                  </span>
                </div>
                <p className="text-[11px] text-emerald-100 dark:text-slate-400 truncate max-w-[200px]">
                  {t('ai.subtitle')}
                </p>
              </div>
            </div>

            {/* Header Action Controls: History -> New Chat -> Reset -> Close */}
            <div className="flex items-center gap-0.5">
              <button
                type="button"
                id="ai-btn-history"
                onClick={() => setShowHistory((prev) => !prev)}
                title={t('ai.history')}
                className={cn(
                  'p-1.5 rounded-lg text-emerald-100 hover:text-white hover:bg-white/10 dark:text-slate-400 dark:hover:text-white transition-colors cursor-pointer',
                  showHistory && 'bg-white/20 text-white dark:bg-slate-800'
                )}
                aria-label={t('ai.history')}
              >
                <History className="h-4 w-4" />
              </button>
              <button
                type="button"
                id="ai-btn-new-chat"
                onClick={handleCreateNewChat}
                title={t('ai.newChat')}
                className="p-1.5 rounded-lg text-emerald-100 hover:text-white hover:bg-white/10 dark:text-slate-400 dark:hover:text-white transition-colors cursor-pointer"
                aria-label={t('ai.newChat')}
              >
                <Plus className="h-4 w-4" />
              </button>
              <button
                type="button"
                id="ai-btn-close"
                onClick={() => setIsOpen(false)}
                title={t('ai.closePanel')}
                className="p-1.5 rounded-lg text-emerald-100 hover:text-white hover:bg-white/10 dark:text-slate-400 dark:hover:text-white transition-colors cursor-pointer"
                aria-label={t('ai.closePanel')}
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          </div>

          {/* MAIN CHAT BODY / MESSAGES */}
          <div className="relative flex-1 flex flex-col min-h-0 overflow-hidden">
            {/* Messages Scroll Area */}
            <div className="flex-1 p-4 overflow-y-auto space-y-3.5 bg-slate-50/60 dark:bg-slate-950/50 text-xs">
              {activeConversation.messages.map((msg) => (
                <div
                  key={msg.id}
                  className={cn(
                    'flex gap-2.5 max-w-[92%]',
                    msg.sender === 'user' ? 'ml-auto flex-row-reverse' : 'mr-auto'
                  )}
                >
                  {msg.sender === 'ai' && (
                    <div className="h-7 w-7 rounded-full bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0 mt-0.5 font-bold">
                      <Sparkles className="h-3.5 w-3.5" />
                    </div>
                  )}

                  <div
                    className={cn(
                      'p-3.5 rounded-2xl leading-relaxed whitespace-pre-line shadow-2xs text-xs',
                      msg.sender === 'user'
                        ? 'bg-slate-900 dark:bg-emerald-600 text-white rounded-br-xs'
                        : 'bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 text-slate-800 dark:text-slate-200 rounded-bl-xs'
                    )}
                  >
                    <p>{msg.text}</p>

                    {/* Message Attachments */}
                    {msg.attachments && msg.attachments.length > 0 && (
                      <div className="mt-2.5 space-y-1.5">
                        {msg.attachments.map((att) =>
                          att.isImage ? (
                            <div
                              key={att.id}
                              className={cn(
                                'rounded-xl overflow-hidden max-w-[200px] border shadow-xs',
                                msg.sender === 'user'
                                  ? 'border-white/20 bg-black/10'
                                  : 'border-slate-200 dark:border-slate-700 bg-slate-100 dark:bg-slate-800'
                              )}
                            >
                              {att.previewUrl ? (
                                <img
                                  src={att.previewUrl}
                                  alt={att.name}
                                  className="w-full h-auto max-h-36 object-cover"
                                />
                              ) : (
                                <div className="p-2 flex items-center gap-2 text-[11px]">
                                  <FileText className="h-3.5 w-3.5 shrink-0" />
                                  <span className="truncate">{att.name}</span>
                                </div>
                              )}
                            </div>
                          ) : (
                            <div
                              key={att.id}
                              className={cn(
                                'flex items-center gap-2 px-2.5 py-1.5 rounded-xl border max-w-[240px]',
                                msg.sender === 'user'
                                  ? 'bg-white/10 border-white/20 text-white'
                                  : 'bg-slate-100 dark:bg-slate-800/80 border-slate-200 dark:border-slate-700 text-slate-800 dark:text-slate-200'
                              )}
                            >
                              <FileText
                                className={cn(
                                  'h-4 w-4 shrink-0',
                                  msg.sender === 'user' ? 'text-white/80' : 'text-emerald-500'
                                )}
                              />
                              <div className="min-w-0 flex-1">
                                <p className="truncate text-[11px] font-medium leading-tight">
                                  {att.name}
                                </p>
                                <p
                                  className={cn(
                                    'text-[9px]',
                                    msg.sender === 'user'
                                      ? 'text-white/70'
                                      : 'text-slate-400 dark:text-slate-500'
                                  )}
                                >
                                  {formatFileSize(att.size)} •{' '}
                                  {att.name.split('.').pop()?.toUpperCase() || 'FILE'}
                                </p>
                              </div>
                            </div>
                          )
                        )}
                      </div>
                    )}

                    {/* Proposed Action */}
                    {msg.proposedAction && (
                      <div className="mt-2.5 pt-2 border-t border-slate-200/60 dark:border-slate-800 space-y-2">
                        <p className="text-[10px] font-semibold text-slate-500 dark:text-slate-400">
                          {locale === 'vi-VN' ? 'Đề xuất hành động:' : 'Proposed action:'}
                        </p>
                        <div className="p-3 rounded-2xl bg-emerald-50/80 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 space-y-2">
                          <p className="text-xs font-semibold text-emerald-900 dark:text-emerald-200">
                            {msg.proposedAction.safeSummary}
                          </p>
                          {msg.proposedAction.requiresEvidence && (
                            <p className="text-[10px] text-amber-700 dark:text-amber-400 flex items-center gap-1">
                              <ShieldCheck className="h-3 w-3" />
                              {locale === 'vi-VN' ? 'Yêu cầu bằng chứng xác minh (1–3 tệp, tối đa 2 MB, JPEG/PNG/WEBP/PDF)' : 'Evidence required (1–3 files, max 2 MB each, JPEG/PNG/WEBP/PDF)'}
                            </p>
                          )}
                          <div className="flex items-center gap-2 pt-1">
                            <button
                              type="button"
                              disabled={executingActionType === msg.proposedAction.actionType}
                              onClick={() => handleExecuteAction(msg.proposedAction)}
                              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-[11px] font-bold transition-all cursor-pointer shadow-2xs disabled:opacity-50"
                            >
                              <Zap className="h-3 w-3 text-amber-300" />
                              <span>
                                {executingActionType === msg.proposedAction.actionType
                                  ? (locale === 'vi-VN' ? 'Đang thực hiện...' : 'Executing...')
                                  : (locale === 'vi-VN' ? 'Xác nhận thực hiện' : 'Confirm Execution')}
                              </span>
                            </button>
                            <button
                              type="button"
                              onClick={() => handleCancelAction(msg.proposedAction)}
                              className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-xl bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-600 dark:text-slate-300 text-[11px] font-medium transition-all cursor-pointer"
                            >
                              <X className="h-3 w-3 text-slate-400" />
                              <span>{locale === 'vi-VN' ? 'Hủy' : 'Cancel'}</span>
                            </button>
                          </div>
                        </div>
                      </div>
                    )}

                    <span
                      className={cn(
                        'block text-[10px] mt-1 text-right',
                        msg.sender === 'user'
                          ? 'text-white/60'
                          : 'text-slate-400 dark:text-slate-500'
                      )}
                    >
                      {msg.time}
                    </span>
                  </div>
                </div>
              ))}

              {isTyping && (
                <div className="flex gap-2 mr-auto items-center text-slate-400 bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 px-3.5 py-2.5 rounded-2xl rounded-bl-xs">
                  <Sparkles className="h-3.5 w-3.5 text-emerald-500 animate-spin" />
                  <span className="text-[11px] font-medium">
                    {locale === 'vi-VN' ? 'SpendWise AI đang phân tích...' : 'SpendWise AI is analyzing...'}
                  </span>
                </div>
              )}

              <div ref={messagesEndRef} />
            </div>

            {/* Suggested Prompt Chips */}
            <div className="p-3 bg-white dark:bg-slate-900 border-t border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-1 text-[11px] font-semibold text-slate-400 dark:text-slate-500 mb-2">
                <Zap className="h-3 w-3 text-amber-500" />
                <span>{t('ai.suggestedTitle')}</span>
              </div>
              <div className="flex flex-wrap gap-1.5 max-h-28 overflow-y-auto pr-0.5">
                {suggestedPrompts.map((chip, idx) => (
                  <button
                    key={idx}
                    type="button"
                    onClick={() => handleSendMessage(chip.query)}
                    className="text-left px-2.5 py-1 rounded-full bg-slate-100 dark:bg-slate-800 hover:bg-emerald-50 hover:text-emerald-700 dark:hover:bg-slate-700 text-[11px] font-medium text-slate-700 dark:text-slate-300 transition-colors border border-slate-200/70 dark:border-slate-700 cursor-pointer"
                  >
                    {chip.label}
                  </button>
                ))}
              </div>
            </div>

            {/* Pending Attachments Strip */}
            {pendingAttachments.length > 0 && (
              <div className="p-2.5 pb-1 bg-white dark:bg-slate-900 border-t border-slate-100 dark:border-slate-800 flex flex-wrap gap-2">
                {pendingAttachments.map((att) => (
                  <div
                    key={att.id}
                    className="relative flex items-center gap-2 p-1.5 pr-2 rounded-xl bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-xs text-slate-800 dark:text-slate-200 max-w-[200px]"
                  >
                    {att.isImage && att.previewUrl ? (
                      <img
                        src={att.previewUrl}
                        alt={att.name}
                        className="h-8 w-8 rounded-lg object-cover border border-slate-200 dark:border-slate-700 shrink-0"
                      />
                    ) : (
                      <div className="h-8 w-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0 border border-emerald-200/60 dark:border-emerald-800/40">
                        <FileText className="h-4 w-4" />
                      </div>
                    )}
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-[11px] font-medium leading-tight">{att.name}</p>
                      <div className="flex items-center gap-1.5 mt-0.5">
                        <span className="text-[10px] text-slate-400">{formatFileSize(att.size)}</span>
                        <span className="text-[9px] px-1 py-0.2 rounded bg-slate-200 dark:bg-slate-700 text-slate-600 dark:text-slate-300 uppercase font-semibold">
                          {att.name.split('.').pop() || 'FILE'}
                        </span>
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() => handleRemovePendingAttachment(att.id)}
                      className="p-1 rounded-full text-slate-400 hover:text-rose-500 hover:bg-rose-50 dark:hover:bg-rose-950/30 transition-colors cursor-pointer"
                      title={t('ai.removeAttachment')}
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </div>
                ))}
              </div>
            )}

            {/* Attachment Error Alert */}
            {attachmentError && (
              <div className="px-3 py-1.5 bg-rose-50 dark:bg-rose-950/40 border-t border-rose-200 dark:border-rose-900/60 flex items-center justify-between text-[11px] text-rose-600 dark:text-rose-400">
                <div className="flex items-center gap-1.5 truncate">
                  <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                  <span className="truncate">{attachmentError}</span>
                </div>
                <button
                  type="button"
                  onClick={() => setAttachmentError(null)}
                  className="p-0.5 hover:text-rose-800 dark:hover:text-rose-200 cursor-pointer"
                >
                  <X className="h-3 w-3" />
                </button>
              </div>
            )}

            {/* Input Bar */}
            <div className="p-3 bg-white dark:bg-slate-900 border-t border-slate-100 dark:border-slate-800 flex items-end gap-2">
              <input
                type="file"
                ref={fileInputRef}
                onChange={(e) => {
                  if (e.target.files) {
                    processFiles(e.target.files);
                    e.target.value = '';
                  }
                }}
                multiple
                accept="image/png,image/jpeg,image/webp,application/pdf,text/plain,text/csv,application/json,.png,.jpg,.jpeg,.webp,.pdf,.txt,.csv,.json"
                className="hidden"
                id="ai-file-input"
              />
              <button
                type="button"
                id="ai-btn-attach"
                onClick={() => fileInputRef.current?.click()}
                title={t('ai.attachFile')}
                aria-label={t('ai.attachFile')}
                className="flex h-8 w-8 items-center justify-center rounded-xl text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors shrink-0 cursor-pointer"
              >
                <Paperclip className="h-4 w-4" />
              </button>
              <textarea
                ref={textareaRef}
                rows={1}
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder={t('ai.inputPlaceholder')}
                className="flex-1 px-3.5 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50/70 dark:bg-slate-800 text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-none focus:ring-1 focus:ring-emerald-500 resize-none max-h-20"
              />
              <button
                type="button"
                id="ai-btn-send"
                onClick={() => handleSendMessage()}
                disabled={!inputValue.trim() && pendingAttachments.length === 0}
                title={t('ai.send')}
                className="flex h-8 w-8 items-center justify-center rounded-xl bg-emerald-500 hover:bg-emerald-600 disabled:opacity-40 text-white transition-colors shrink-0 shadow-xs cursor-pointer"
              >
                <Send className="h-3.5 w-3.5" />
              </button>
            </div>

            {/* HISTORY DRAWER / OVERLAY INSIDE THE PANEL */}
            {showHistory && (
              <div
                id="ai-history-drawer"
                className="absolute inset-0 bg-white dark:bg-slate-900 z-20 flex flex-col animate-in fade-in slide-in-from-left-4 duration-200"
              >
                {/* History Drawer Header */}
                <div className="flex items-center justify-between px-4 py-3 border-b border-slate-100 dark:border-slate-800 bg-slate-50/80 dark:bg-slate-900/80 shrink-0">
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => setShowHistory(false)}
                      className="p-1 rounded-lg text-slate-500 hover:bg-slate-200 dark:hover:bg-slate-800 transition-colors"
                      title={t('common.close')}
                    >
                      <ChevronLeft className="h-4 w-4" />
                    </button>
                    <h4 className="text-xs font-bold text-slate-900 dark:text-white">
                      {t('ai.history')}
                    </h4>
                  </div>
                  <button
                    type="button"
                    onClick={handleCreateNewChat}
                    className="inline-flex items-center gap-1 px-2.5 py-1 text-[11px] font-semibold text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-500/10 rounded-lg hover:bg-emerald-100 transition-colors cursor-pointer"
                  >
                    <Plus className="h-3 w-3" />
                    <span>{t('ai.newChat')}</span>
                  </button>
                </div>

                {/* History Search */}
                <div className="p-3 border-b border-slate-100 dark:border-slate-800 shrink-0">
                  <div className="relative">
                    <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
                    <input
                      type="text"
                      value={historySearch}
                      onChange={(e) => setHistorySearch(e.target.value)}
                      placeholder={t('ai.searchHistory')}
                      className="w-full pl-8 pr-3 py-1.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50/70 dark:bg-slate-800 text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-none focus:ring-1 focus:ring-emerald-500"
                    />
                    {historySearch && (
                      <button
                        type="button"
                        onClick={() => setHistorySearch('')}
                        className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                      >
                        <X className="h-3 w-3" />
                      </button>
                    )}
                  </div>
                </div>

                {/* History List */}
                <div className="flex-1 p-3 overflow-y-auto space-y-4 text-xs">
                  {historySections.length === 0 ? (
                    <div className="text-center py-12 text-slate-400 space-y-1.5">
                      <MessageSquare className="h-7 w-7 mx-auto stroke-1 opacity-50 text-slate-400" />
                      <p className="text-xs">{t('ai.noConversations')}</p>
                    </div>
                  ) : (
                    historySections.map((section) => (
                      <div key={section.key} className="space-y-1">
                        <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider px-2">
                          {section.title}
                        </span>
                        <div className="space-y-0.5 mt-1">
                          {section.items.map((conv) => {
                            const isActive = conv.id === activeConvId;
                            const lastMsg = conv.messages[conv.messages.length - 1];
                            const isMenuOpen = menuOpenConvId === conv.id;

                            return (
                              <div
                                key={conv.id}
                                className={cn(
                                  'group relative flex items-center justify-between p-2 rounded-xl transition-all border cursor-pointer select-none',
                                  isActive
                                    ? 'bg-emerald-50/80 dark:bg-emerald-950/20 border-emerald-200/80 dark:border-emerald-800/40 text-slate-900 dark:text-white'
                                    : 'bg-white dark:bg-slate-900/50 border-transparent hover:bg-slate-50 dark:hover:bg-slate-800/50 text-slate-700 dark:text-slate-300'
                                )}
                                onClick={() => handleSelectConversation(conv.id)}
                              >
                                <div className="min-w-0 flex-1 pr-2">
                                  <div className="flex items-center gap-1.5">
                                    {conv.isPinned && (
                                      <Pin className="h-3 w-3 text-amber-500 shrink-0 fill-amber-500" />
                                    )}
                                    <p
                                      className={cn(
                                        'text-xs font-semibold truncate leading-tight',
                                        isActive
                                          ? 'text-emerald-900 dark:text-emerald-300'
                                          : 'text-slate-800 dark:text-slate-200'
                                      )}
                                    >
                                      {conv.title}
                                    </p>
                                  </div>
                                  <div className="flex items-center gap-1.5 mt-0.5 text-[11px] text-slate-400 dark:text-slate-500">
                                    <span className="truncate max-w-[200px]">
                                      {lastMsg?.text?.replace(/\n/g, ' ') || '...'}
                                    </span>
                                    <span>•</span>
                                    <span className="shrink-0">
                                      {formatHistoryTime(conv.updatedAt)}
                                    </span>
                                  </div>
                                </div>

                                {/* Row More Actions Button (...) */}
                                <div className="relative shrink-0">
                                  <button
                                    type="button"
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      setMenuOpenConvId(isMenuOpen ? null : conv.id);
                                    }}
                                    className="p-1 rounded-md text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                                    title={locale === 'vi-VN' ? 'Tùy chọn' : 'Options'}
                                  >
                                    <MoreVertical className="h-3.5 w-3.5" />
                                  </button>

                                  {/* Row Dropdown Menu */}
                                  {isMenuOpen && (
                                    <>
                                      <div
                                        className="fixed inset-0 z-30"
                                        onClick={(e) => {
                                          e.stopPropagation();
                                          setMenuOpenConvId(null);
                                        }}
                                      />
                                      <div className="absolute right-0 top-6 z-40 w-36 rounded-xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-xl py-1 text-xs animate-in fade-in zoom-in-95 duration-100">
                                        <button
                                          type="button"
                                          onClick={(e) => handleTogglePin(conv.id, e)}
                                          className="w-full flex items-center gap-2 px-3 py-1.5 text-left text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700/60"
                                        >
                                          {conv.isPinned ? (
                                            <>
                                              <PinOff className="h-3.5 w-3.5 text-amber-500" />
                                              <span>{t('ai.unpin')}</span>
                                            </>
                                          ) : (
                                            <>
                                              <Pin className="h-3.5 w-3.5 text-amber-500" />
                                              <span>{t('ai.pin')}</span>
                                            </>
                                          )}
                                        </button>
                                        <button
                                          type="button"
                                          onClick={(e) => handleStartRename(conv, e)}
                                          className="w-full flex items-center gap-2 px-3 py-1.5 text-left text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700/60"
                                        >
                                          <Edit3 className="h-3.5 w-3.5 text-slate-500" />
                                          <span>{t('ai.rename')}</span>
                                        </button>
                                        <div className="my-0.5 border-t border-slate-100 dark:border-slate-700/80" />
                                        <button
                                          type="button"
                                          onClick={(e) => {
                                            e.stopPropagation();
                                            setDeleteConfirmConvId(conv.id);
                                            setMenuOpenConvId(null);
                                          }}
                                          className="w-full flex items-center gap-2 px-3 py-1.5 text-left text-rose-600 dark:text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-950/30"
                                        >
                                          <Trash2 className="h-3.5 w-3.5" />
                                          <span>{t('ai.delete')}</span>
                                        </button>
                                      </div>
                                    </>
                                  )}
                                </div>
                              </div>
                            );
                          })}
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            )}

            {/* DELETE CONFIRMATION MODAL */}
            {deleteConfirmConvId && (
              <div className="absolute inset-0 bg-black/40 backdrop-blur-2xs z-50 flex items-center justify-center p-4 animate-in fade-in duration-150">
                <div className="w-full max-w-[320px] rounded-2xl bg-white dark:bg-slate-800 p-4 border border-slate-200 dark:border-slate-700 shadow-2xl space-y-3">
                  <div className="space-y-1 text-center">
                    <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                      {t('ai.confirmDeleteTitle')}
                    </h4>
                    <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
                      {t('ai.confirmDeleteDesc')}
                    </p>
                  </div>
                  <div className="flex items-center justify-center gap-2 pt-1">
                    <button
                      type="button"
                      onClick={() => setDeleteConfirmConvId(null)}
                      className="flex-1 py-1.5 px-3 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors cursor-pointer"
                    >
                      {t('ai.cancel')}
                    </button>
                    <button
                      type="button"
                      onClick={handleConfirmDelete}
                      className="flex-1 py-1.5 px-3 rounded-xl bg-rose-600 hover:bg-rose-700 text-xs font-semibold text-white transition-colors cursor-pointer shadow-xs"
                    >
                      {t('ai.delete')}
                    </button>
                  </div>
                </div>
              </div>
            )}

            {/* RENAME MODAL */}
            {renamingConvId && (
              <div className="absolute inset-0 bg-black/40 backdrop-blur-2xs z-50 flex items-center justify-center p-4 animate-in fade-in duration-150">
                <div className="w-full max-w-[320px] rounded-2xl bg-white dark:bg-slate-800 p-4 border border-slate-200 dark:border-slate-700 shadow-2xl space-y-3">
                  <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                    {t('ai.renameModalTitle')}
                  </h4>
                  <input
                    ref={renameInputRef}
                    type="text"
                    value={renameInput}
                    maxLength={50}
                    onChange={(e) => setRenameInput(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') handleSaveRename();
                      if (e.key === 'Escape') setRenamingConvId(null);
                    }}
                    placeholder={t('ai.enterNewTitle')}
                    className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-white focus:outline-none focus:ring-1 focus:ring-emerald-500"
                  />
                  <div className="flex items-center justify-end gap-2 pt-1">
                    <button
                      type="button"
                      onClick={() => setRenamingConvId(null)}
                      className="py-1.5 px-3 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors cursor-pointer"
                    >
                      {t('ai.cancel')}
                    </button>
                    <button
                      type="button"
                      onClick={handleSaveRename}
                      disabled={!renameInput.trim()}
                      className="py-1.5 px-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:opacity-40 text-xs font-semibold text-white transition-colors cursor-pointer shadow-xs"
                    >
                      {t('ai.save')}
                    </button>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </>
  );
};
