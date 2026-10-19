import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import {
  Sparkles,
  X,
  Send,
  Plus,
  History,
  Search,
  Pin,
  PinOff,
  Edit2,
  Trash2,
  Check,
  ChevronLeft,
  ChevronDown,
  ChevronUp,
  FileDown,
  AlertCircle,
  FileText,
  Clock,
  Loader2,
  HelpCircle,
  BarChart3,
  Calendar,
  Layers,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { cn } from '../../utils/cn';
import {
  adminAiApiService,
  AdminAiConversationDto,
  AdminAiReportFormat,
} from '../../services/adminAiApiService';
import { isApiError } from '../../lib/apiError';

export const FloatingAdminAiAssistant: React.FC = () => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const isVi = locale === 'vi-VN';

  // Floating window open/close & view states
  const [isOpen, setIsOpen] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const [showReports, setShowReports] = useState(false);

  // Conversations state
  const [conversations, setConversations] = useState<AdminAiConversationDto[]>([]);
  const [selectedConvId, setSelectedConvId] = useState<string | null>(null);
  const [currentConversation, setCurrentConversation] = useState<AdminAiConversationDto | null>(null);
  const [isLoadingConversations, setIsLoadingConversations] = useState(false);
  const [isLoadingChat, setIsLoadingChat] = useState(false);
  const [isSending, setIsSending] = useState(false);
  const [conversationSearch, setConversationSearch] = useState('');

  // Chat input & error state
  const [inputMessage, setInputMessage] = useState('');
  const [chatError, setChatError] = useState<string | null>(null);

  // Rename & Delete state
  const [editingConvId, setEditingConvId] = useState<string | null>(null);
  const [editTitleValue, setEditTitleValue] = useState('');
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  // Report export state
  const [reportPeriodDays, setReportPeriodDays] = useState<number>(30);
  const [isExportingWord, setIsExportingWord] = useState<boolean>(false);
  const [isExportingExcel, setIsExportingExcel] = useState<boolean>(false);
  const [exportMessage, setExportMessage] = useState<{ text: string; isError: boolean } | null>(null);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    if (isOpen && !showHistory) {
      scrollToBottom();
    }
  }, [currentConversation?.messages, isSending, isOpen, showHistory]);

  // Focus input when opening or switching away from history
  useEffect(() => {
    if (isOpen && !showHistory) {
      setTimeout(() => {
        inputRef.current?.focus();
      }, 150);
    }
  }, [isOpen, showHistory]);

  // Load conversations from backend
  const loadConversations = useCallback(async (searchQuery?: string) => {
    try {
      setIsLoadingConversations(true);
      const list = await adminAiApiService.getConversations(searchQuery);
      setConversations(list);
    } catch {
      // Ignore background refresh errors
    } finally {
      setIsLoadingConversations(false);
    }
  }, []);

  useEffect(() => {
    if (isOpen) {
      loadConversations();
    }
  }, [isOpen, loadConversations]);

  // Debounced search
  useEffect(() => {
    if (!isOpen) return;
    const handler = setTimeout(() => {
      loadConversations(conversationSearch.trim() || undefined);
    }, 300);
    return () => clearTimeout(handler);
  }, [conversationSearch, isOpen, loadConversations]);

  // Load specific conversation
  const loadConversationDetails = useCallback(async (id: string) => {
    try {
      setIsLoadingChat(true);
      setChatError(null);
      const conv = await adminAiApiService.getConversationById(id);
      setCurrentConversation(conv);
    } catch (err) {
      if (isApiError(err) && err.status === 404) {
        setSelectedConvId(null);
        setCurrentConversation(null);
        loadConversations();
      } else {
        setChatError(t('adminAi.generalError'));
      }
    } finally {
      setIsLoadingChat(false);
    }
  }, [loadConversations, t]);

  useEffect(() => {
    if (selectedConvId) {
      loadConversationDetails(selectedConvId);
    } else {
      setCurrentConversation(null);
    }
  }, [selectedConvId, loadConversationDetails]);

  // New Chat handler
  const handleNewChat = () => {
    setSelectedConvId(null);
    setCurrentConversation(null);
    setChatError(null);
    setInputMessage('');
    setShowHistory(false);
    setShowReports(false);
  };

  // Send message
  const handleSendMessage = async (customPrompt?: string) => {
    const textToSend = (customPrompt || inputMessage).trim();
    if (!textToSend || isSending) return;

    setInputMessage('');
    setChatError(null);
    setIsSending(true);

    try {
      const res = await adminAiApiService.chat({
        conversationId: selectedConvId,
        message: textToSend,
        mode: 'FinancialQuery',
      });

      if (!selectedConvId && res.conversationId) {
        setSelectedConvId(res.conversationId);
      } else if (res.conversationId) {
        await loadConversationDetails(res.conversationId);
      }
      await loadConversations();
    } catch (err) {
      if (isApiError(err)) {
        if (err.status === 429) {
          setChatError(t('adminAi.rateLimitExceeded'));
        } else if (err.status === 503) {
          setChatError(t('adminAi.geminiUnavailable'));
        } else if (err.status === 401) {
          setChatError(t('adminAi.unauthorized'));
        } else if (err.status === 403) {
          setChatError(t('adminAi.forbidden'));
        } else {
          setChatError(err.detail || err.title || t('adminAi.generalError'));
        }
      } else {
        setChatError(t('adminAi.generalError'));
      }
    } finally {
      setIsSending(false);
    }
  };

  // Toggle Pin
  const handleTogglePin = async (conv: AdminAiConversationDto, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await adminAiApiService.updateConversation(conv.id, {
        isPinned: !conv.isPinned,
      });
      await loadConversations();
      if (selectedConvId === conv.id) {
        await loadConversationDetails(conv.id);
      }
    } catch {
      // Ignore toggle errors
    }
  };

  // Start Rename
  const handleStartRename = (conv: AdminAiConversationDto, e: React.MouseEvent) => {
    e.stopPropagation();
    setEditingConvId(conv.id);
    setEditTitleValue(conv.title || '');
  };

  // Save Rename
  const handleSaveRename = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!editTitleValue.trim()) {
      setEditingConvId(null);
      return;
    }
    try {
      await adminAiApiService.updateConversation(id, {
        title: editTitleValue.trim(),
      });
      setEditingConvId(null);
      await loadConversations();
      if (selectedConvId === id) {
        await loadConversationDetails(id);
      }
    } catch {
      // Ignore save rename error
    }
  };

  // Delete Conversation
  const handleDeleteConversation = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await adminAiApiService.deleteConversation(id);
      setDeleteConfirmId(null);
      if (selectedConvId === id) {
        setSelectedConvId(null);
        setCurrentConversation(null);
      }
      await loadConversations();
    } catch {
      // Ignore delete error
    }
  };

  // Export Report (Word / Excel)
  const handleExportReport = async (format: AdminAiReportFormat) => {
    if (format === 'Word') setIsExportingWord(true);
    if (format === 'Excel') setIsExportingExcel(true);
    setExportMessage(null);

    try {
      await adminAiApiService.exportReport({
        format,
        reportType: 'SystemOverview',
        periodDays: Math.min(Math.max(reportPeriodDays, 1), 365),
      });
      setExportMessage({
        text: t('adminAi.exportSuccess'),
        isError: false,
      });
    } catch (err) {
      if (isApiError(err)) {
        setExportMessage({
          text: err.detail || err.title || t('adminAi.exportFailed'),
          isError: true,
        });
      } else {
        setExportMessage({
          text: t('adminAi.exportFailed'),
          isError: true,
        });
      }
    } finally {
      if (format === 'Word') setIsExportingWord(false);
      if (format === 'Excel') setIsExportingExcel(false);
    }
  };

  const suggestedPrompts = useMemo(() => [
    { title: t('adminAi.prompt1'), desc: isVi ? 'Tóm tắt sức khỏe các thành phần hệ thống' : 'Health overview of system components' },
    { title: t('adminAi.prompt2'), desc: isVi ? 'Tổng hợp số lượng case hỗ trợ mở' : 'Summary of open support cases' },
    { title: t('adminAi.prompt3'), desc: isVi ? 'Tổng hợp số lượng và giá trị giao dịch' : 'Overview of transaction volume and value' },
    { title: t('adminAi.prompt4'), desc: isVi ? 'Tình trạng đồng bộ và đối soát SePay' : 'SePay reconciliation sync status' },
    { title: t('adminAi.prompt5'), desc: isVi ? 'Chuẩn bị dữ liệu báo cáo tổng quan' : 'Prepare system overview report' },
    { title: t('adminAi.prompt6'), desc: isVi ? 'Tổng hợp số liệu hệ thống định dạng bảng' : 'Tabular system statistics for Excel export' },
  ], [t, isVi]);

  // Authorization check: Only SystemAdmin can view
  if (currentUser?.role !== 'SystemAdmin') {
    return null;
  }

  return (
    <>
      {/* Floating Trigger Button: Anchored at bottom-right */}
      {!isOpen && (
        <div className="fixed bottom-6 right-6 z-40 animate-in fade-in zoom-in-95 duration-200">
          <button
            type="button"
            id="spendwise-admin-ai-trigger"
            onClick={() => setIsOpen(true)}
            className="group relative flex items-center gap-2.5 px-4.5 py-3 rounded-full bg-slate-900 dark:bg-[#0E1726] text-white hover:bg-slate-800 dark:hover:bg-slate-800/90 hover:scale-105 shadow-xl shadow-slate-950/25 transition-all duration-200 cursor-pointer select-none focus:outline-hidden focus:ring-2 focus:ring-emerald-500/50 border border-emerald-500/40 dark:border-emerald-500/30"
            aria-label={t('adminAi.title')}
          >
            {/* Subtle emerald ambient aura */}
            <span className="absolute -inset-0.5 rounded-full bg-gradient-to-r from-emerald-500/30 to-teal-500/30 opacity-50 blur-xs group-hover:opacity-80 transition duration-300 pointer-events-none" />

            <div className="relative flex items-center gap-2">
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-emerald-600 dark:bg-emerald-500 text-white shadow-xs">
                <Sparkles className="h-4 w-4" />
              </div>
              <span className="text-sm font-bold tracking-tight">
                {t('adminAi.title')}
              </span>
              <span className="inline-flex items-center text-[10px] px-1.5 py-0.5 rounded bg-white/10 dark:bg-emerald-950/80 text-emerald-400 font-mono font-semibold uppercase tracking-wider">
                Gemini
              </span>
              <span className="flex h-2 w-2 rounded-full bg-emerald-400 animate-pulse ml-0.5" />
            </div>
          </button>
        </div>
      )}

      {/* Floating Admin AI Panel */}
      {isOpen && (
        <div
          id="spendwise-admin-ai-window"
          className="fixed bottom-6 right-6 z-50 w-[94vw] sm:w-[500px] h-[660px] max-h-[calc(100vh-80px)] rounded-3xl bg-white dark:bg-[#0E1726] border border-slate-200 dark:border-slate-800 shadow-2xl flex flex-col overflow-hidden animate-in slide-from-bottom-5 fade-in duration-200"
        >
          {/* 1. Header: SpendWise Admin AI Identity, Gemini Badge & Control Buttons */}
          <div className="flex items-center justify-between px-5 py-3.5 bg-slate-900 dark:bg-[#090E18] text-white border-b border-slate-800 shrink-0">
            <div className="flex items-center gap-2.5 min-w-0">
              <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 shrink-0">
                <Sparkles className="h-4 w-4" />
              </div>
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <h3 className="text-sm font-bold text-white leading-tight truncate">
                    {t('adminAi.title')}
                  </h3>
                  <span className="inline-flex items-center gap-1 px-1.5 py-0.2 rounded text-[10px] font-mono bg-emerald-950/70 border border-emerald-500/30 text-emerald-400 font-semibold">
                    <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
                    Gemini
                  </span>
                </div>
                <p className="text-[11px] text-slate-400 truncate max-w-[220px]">
                  {currentConversation?.title || t('adminAi.subtitle')}
                </p>
              </div>
            </div>

            {/* Header Actions: History, Reports, New Chat, Close */}
            <div className="flex items-center gap-1 shrink-0">
              <button
                type="button"
                id="admin-ai-btn-reports"
                onClick={() => {
                  setShowReports((prev) => !prev);
                  if (!showReports) setShowHistory(false);
                }}
                title={t('adminAi.reportsTitle')}
                className={cn(
                  'p-1.5 rounded-lg text-slate-300 hover:text-white hover:bg-white/10 transition-colors cursor-pointer',
                  showReports && 'bg-white/20 text-white'
                )}
              >
                <BarChart3 className="h-4 w-4" />
              </button>

              <button
                type="button"
                id="admin-ai-btn-history"
                onClick={() => {
                  setShowHistory((prev) => !prev);
                  if (!showHistory) setShowReports(false);
                }}
                title={t('adminAi.conversations')}
                className={cn(
                  'p-1.5 rounded-lg text-slate-300 hover:text-white hover:bg-white/10 transition-colors cursor-pointer',
                  showHistory && 'bg-white/20 text-white'
                )}
              >
                <History className="h-4 w-4" />
              </button>

              <button
                type="button"
                id="admin-ai-btn-new-chat"
                onClick={handleNewChat}
                title={t('adminAi.newChat')}
                className="p-1.5 rounded-lg text-slate-300 hover:text-white hover:bg-white/10 transition-colors cursor-pointer"
              >
                <Plus className="h-4 w-4" />
              </button>

              <button
                type="button"
                id="admin-ai-btn-close"
                onClick={() => setIsOpen(false)}
                title={t('common.close')}
                className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-white/10 transition-colors cursor-pointer ml-1"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          </div>

          {/* 2. Main Panel Canvas */}
          <div className="flex-1 relative flex flex-col min-h-0 bg-slate-50/50 dark:bg-[#0B1120]">
            {/* VIEW A: Report Export Drawer */}
            {showReports && (
              <div className="absolute inset-0 z-20 bg-white dark:bg-[#0E1726] flex flex-col p-5 overflow-y-auto animate-in slide-from-top-4 duration-150">
                <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-800">
                  <div className="flex items-center gap-2">
                    <BarChart3 className="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
                    <h4 className="text-xs font-bold text-slate-900 dark:text-white uppercase tracking-wider">
                      {t('adminAi.reportsTitle')}
                    </h4>
                  </div>
                  <button
                    type="button"
                    onClick={() => setShowReports(false)}
                    className="p-1 rounded-md text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>

                <p className="text-xs text-slate-500 dark:text-slate-400 mt-3 leading-relaxed">
                  {t('adminAi.reportsSubtitle')}
                </p>

                <div className="space-y-4 pt-4">
                  <div>
                    <label className="text-[11px] font-semibold text-slate-600 dark:text-slate-400 block mb-1">
                      {t('adminAi.reportType')}
                    </label>
                    <div className="flex items-center gap-2 px-3 py-2 bg-slate-50 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800 rounded-xl text-xs text-slate-700 dark:text-slate-300 font-medium">
                      <Layers className="w-3.5 h-3.5 text-emerald-500 shrink-0" />
                      <span>{t('adminAi.systemOverview')}</span>
                    </div>
                  </div>

                  <div>
                    <label className="text-[11px] font-semibold text-slate-600 dark:text-slate-400 block mb-1">
                      {t('adminAi.periodDays')}
                    </label>
                    <div className="flex items-center gap-2">
                      <div className="relative flex-1">
                        <Calendar className="w-3.5 h-3.5 text-slate-400 absolute left-3 top-2.5" />
                        <input
                          type="number"
                          min={1}
                          max={365}
                          value={reportPeriodDays}
                          onChange={(e) => setReportPeriodDays(parseInt(e.target.value, 10) || 30)}
                          className="w-full pl-9 pr-3 py-1.5 text-xs bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl text-slate-800 dark:text-slate-200 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
                        />
                      </div>
                      <span className="text-xs text-slate-500 font-medium shrink-0">
                        {t('adminAi.daysSuffix')}
                      </span>
                    </div>
                  </div>

                  {exportMessage && (
                    <div
                      className={cn(
                        'p-2.5 rounded-xl text-xs flex items-center gap-2 border',
                        exportMessage.isError
                          ? 'bg-rose-50 dark:bg-rose-950/40 border-rose-200 dark:border-rose-900/50 text-rose-700 dark:text-rose-300'
                          : 'bg-emerald-50 dark:bg-emerald-950/40 border-emerald-200 dark:border-emerald-900/50 text-emerald-700 dark:text-emerald-300'
                      )}
                    >
                      <AlertCircle className="w-3.5 h-3.5 shrink-0" />
                      <span className="truncate flex-1">{exportMessage.text}</span>
                    </div>
                  )}

                  <div className="grid grid-cols-2 gap-2.5 pt-2">
                    <button
                      type="button"
                      onClick={() => handleExportReport('Word')}
                      disabled={isExportingWord || isExportingExcel}
                      className="flex items-center justify-center gap-2 px-3 py-2.5 rounded-xl border border-blue-200 dark:border-blue-900/40 bg-blue-50/60 dark:bg-blue-950/30 hover:bg-blue-100/60 dark:hover:bg-blue-900/50 text-xs font-semibold text-blue-700 dark:text-blue-300 transition disabled:opacity-50 shadow-xs cursor-pointer"
                    >
                      {isExportingWord ? (
                        <Loader2 className="w-3.5 h-3.5 animate-spin text-blue-600" />
                      ) : (
                        <FileText className="w-3.5 h-3.5 text-blue-600" />
                      )}
                      <span>Word (.docx)</span>
                    </button>

                    <button
                      type="button"
                      onClick={() => handleExportReport('Excel')}
                      disabled={isExportingWord || isExportingExcel}
                      className="flex items-center justify-center gap-2 px-3 py-2.5 rounded-xl border border-emerald-200 dark:border-emerald-900/40 bg-emerald-50/60 dark:bg-emerald-950/30 hover:bg-emerald-100/60 dark:hover:bg-emerald-900/50 text-xs font-semibold text-emerald-700 dark:text-emerald-300 transition disabled:opacity-50 shadow-xs cursor-pointer"
                    >
                      {isExportingExcel ? (
                        <Loader2 className="w-3.5 h-3.5 animate-spin text-emerald-600" />
                      ) : (
                        <FileDown className="w-3.5 h-3.5 text-emerald-600" />
                      )}
                      <span>Excel (.xlsx)</span>
                    </button>
                  </div>
                </div>
              </div>
            )}

            {/* VIEW B: Conversation History Drawer */}
            {showHistory && (
              <div className="absolute inset-0 z-20 bg-white dark:bg-[#0E1726] flex flex-col p-4 animate-in slide-from-left-4 duration-150">
                <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-800">
                  <div className="flex items-center gap-2">
                    <Clock className="w-4 h-4 text-slate-400" />
                    <h4 className="text-xs font-bold text-slate-900 dark:text-white uppercase tracking-wider">
                      {t('adminAi.conversations')} ({conversations.length})
                    </h4>
                  </div>
                  <button
                    type="button"
                    onClick={() => setShowHistory(false)}
                    className="p-1 rounded-md text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>

                {/* Search */}
                <div className="relative my-3">
                  <Search className="w-3.5 h-3.5 text-slate-400 absolute left-3 top-2.5" />
                  <input
                    type="text"
                    value={conversationSearch}
                    onChange={(e) => setConversationSearch(e.target.value)}
                    placeholder={t('adminAi.searchPlaceholder')}
                    className="w-full pl-8 pr-3 py-1.5 text-xs bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl text-slate-800 dark:text-slate-200 placeholder-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
                  />
                </div>

                {/* List */}
                <div className="flex-1 overflow-y-auto space-y-1.5 pr-1 scrollbar-thin">
                  {isLoadingConversations && conversations.length === 0 ? (
                    <div className="flex flex-col items-center justify-center h-40 text-slate-400">
                      <Loader2 className="w-5 h-5 animate-spin mb-2 text-emerald-500" />
                      <span className="text-xs">{t('common.loading')}</span>
                    </div>
                  ) : conversations.length === 0 ? (
                    <div className="text-center py-10 px-4 text-xs text-slate-400">
                      <p>{t('adminAi.noConversations')}</p>
                    </div>
                  ) : (
                    conversations.map((conv) => {
                      const isSelected = selectedConvId === conv.id;
                      const isEditing = editingConvId === conv.id;
                      const isDeleting = deleteConfirmId === conv.id;

                      return (
                        <div
                          key={conv.id}
                          onClick={() => {
                            if (!isEditing && !isDeleting) {
                              setSelectedConvId(conv.id);
                              setShowHistory(false);
                            }
                          }}
                          className={cn(
                            'group flex items-center justify-between p-2.5 rounded-xl text-xs cursor-pointer transition border',
                            isSelected
                              ? 'bg-emerald-50 dark:bg-emerald-950/40 border-emerald-500/40 text-emerald-900 dark:text-emerald-200'
                              : 'bg-transparent hover:bg-slate-50 dark:hover:bg-slate-900/60 border-transparent text-slate-700 dark:text-slate-300'
                          )}
                        >
                          <div className="flex items-center gap-2 min-w-0 flex-1">
                            {conv.isPinned && (
                              <Pin className="w-3 h-3 text-amber-500 shrink-0 rotate-45" />
                            )}
                            {isEditing ? (
                              <div className="flex items-center gap-1 flex-1" onClick={(e) => e.stopPropagation()}>
                                <input
                                  type="text"
                                  value={editTitleValue}
                                  onChange={(e) => setEditTitleValue(e.target.value)}
                                  className="w-full px-2 py-0.5 text-xs bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-md text-slate-900 dark:text-slate-100 focus:outline-hidden"
                                  autoFocus
                                />
                                <button
                                  type="button"
                                  onClick={(e) => handleSaveRename(conv.id, e)}
                                  className="p-1 hover:text-emerald-600 dark:hover:text-emerald-400"
                                  title={t('adminAi.save')}
                                >
                                  <Check className="w-3.5 h-3.5" />
                                </button>
                                <button
                                  type="button"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setEditingConvId(null);
                                  }}
                                  className="p-1 hover:text-rose-600 dark:hover:text-rose-400"
                                  title={t('adminAi.cancel')}
                                >
                                  <X className="w-3.5 h-3.5" />
                                </button>
                              </div>
                            ) : isDeleting ? (
                              <div className="flex items-center justify-between w-full" onClick={(e) => e.stopPropagation()}>
                                <span className="text-[11px] text-rose-600 font-semibold truncate">
                                  {t('adminAi.confirm')}?
                                </span>
                                <div className="flex items-center gap-1">
                                  <button
                                    type="button"
                                    onClick={(e) => handleDeleteConversation(conv.id, e)}
                                    className="px-2 py-0.5 bg-rose-600 text-white rounded text-[10px] font-bold"
                                  >
                                    {t('adminAi.delete')}
                                  </button>
                                  <button
                                    type="button"
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      setDeleteConfirmId(null);
                                    }}
                                    className="px-2 py-0.5 bg-slate-200 dark:bg-slate-800 rounded text-[10px]"
                                  >
                                    {t('adminAi.cancel')}
                                  </button>
                                </div>
                              </div>
                            ) : (
                              <div className="min-w-0 flex-1">
                                <p className="font-semibold truncate">{conv.title || t('adminAi.conversationFallback')}</p>
                                <span className="text-[10px] text-slate-400 block font-mono">
                                  {new Date(conv.updatedAt || conv.createdAt).toLocaleDateString(locale, {
                                    month: 'numeric',
                                    day: 'numeric',
                                    hour: '2-digit',
                                    minute: '2-digit',
                                  })}
                                </span>
                              </div>
                            )}
                          </div>

                          {!isEditing && !isDeleting && (
                            <div className="flex items-center gap-1 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity ml-1">
                              <button
                                type="button"
                                onClick={(e) => handleTogglePin(conv, e)}
                                className="p-1 text-slate-400 hover:text-amber-500 rounded transition"
                                title={conv.isPinned ? t('adminAi.unpin') : t('adminAi.pin')}
                              >
                                {conv.isPinned ? <PinOff className="w-3 h-3" /> : <Pin className="w-3 h-3" />}
                              </button>
                              <button
                                type="button"
                                onClick={(e) => handleStartRename(conv, e)}
                                className="p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 rounded transition"
                                title={t('adminAi.rename')}
                              >
                                <Edit2 className="w-3 h-3" />
                              </button>
                              <button
                                type="button"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setDeleteConfirmId(conv.id);
                                }}
                                className="p-1 text-slate-400 hover:text-rose-500 rounded transition"
                                title={t('adminAi.delete')}
                              >
                                <Trash2 className="w-3 h-3" />
                              </button>
                            </div>
                          )}
                        </div>
                      );
                    })
                  )}
                </div>
              </div>
            )}

            {/* VIEW C: Active Chat Screen */}
            <div className="flex-1 overflow-y-auto p-4 space-y-4 scrollbar-thin">
              {isLoadingChat ? (
                <div className="flex flex-col items-center justify-center h-full text-slate-400">
                  <Loader2 className="w-6 h-6 animate-spin mb-2 text-emerald-500" />
                  <span className="text-xs">{t('common.loading')}</span>
                </div>
              ) : (!currentConversation || currentConversation.messages.length === 0) && !isSending ? (
                /* Empty State with Suggested Prompts */
                <div className="h-full flex flex-col justify-center items-center text-center px-2 py-4 space-y-4">
                  <div className="w-12 h-12 rounded-2xl bg-emerald-50 dark:bg-emerald-950/50 border border-emerald-200 dark:border-emerald-800/60 flex items-center justify-center text-emerald-600 dark:text-emerald-400 shadow-sm">
                    <Sparkles className="w-6 h-6" />
                  </div>
                  <div>
                    <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                      {t('adminAi.emptyTitle')}
                    </h4>
                    <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-1 leading-relaxed max-w-sm">
                      {t('adminAi.emptySubtitle')}
                    </p>
                  </div>

                  {/* Suggested operational prompt shortcuts */}
                  <div className="w-full text-left space-y-2 pt-2">
                    <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400 flex items-center gap-1.5 px-1">
                      <HelpCircle className="w-3 h-3" />
                      <span>{t('adminAi.suggestedTitle')}</span>
                    </div>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                      {suggestedPrompts.map((p, idx) => (
                        <button
                          key={idx}
                          type="button"
                          onClick={() => handleSendMessage(p.title)}
                          className="text-left p-2.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-[#090E18] hover:bg-emerald-50/50 dark:hover:bg-emerald-950/30 hover:border-emerald-500/40 transition group cursor-pointer shadow-2xs"
                        >
                          <p className="text-xs font-semibold text-slate-800 dark:text-slate-200 group-hover:text-emerald-600 dark:group-hover:text-emerald-400 transition">
                            {p.title}
                          </p>
                          <p className="text-[10.5px] text-slate-400 mt-0.5 line-clamp-1">
                            {p.desc}
                          </p>
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              ) : (
                /* Message List */
                <div className="space-y-3.5">
                  {currentConversation?.messages.map((msg) => {
                    const isUser = msg.role.toLowerCase() === 'user';
                    return (
                      <div
                        key={msg.id}
                        className={cn(
                          'flex flex-col max-w-[88%]',
                          isUser ? 'ml-auto items-end' : 'mr-auto items-start'
                        )}
                      >
                        <div
                          className={cn(
                            'p-3.5 rounded-2xl text-xs leading-relaxed shadow-xs whitespace-pre-wrap break-words',
                            isUser
                              ? 'bg-emerald-600 text-white rounded-tr-none'
                              : 'bg-white dark:bg-[#090E18] text-slate-900 dark:text-slate-100 border border-slate-200 dark:border-slate-800 rounded-tl-none'
                          )}
                        >
                          {msg.content}
                        </div>

                        <div className="flex items-center gap-1.5 mt-1 px-1 text-[10px] text-slate-400 font-mono">
                          <span>
                            {new Date(msg.createdAt).toLocaleTimeString([], {
                              hour: '2-digit',
                              minute: '2-digit',
                            })}
                          </span>
                          {!isUser && (
                            <>
                              <span>•</span>
                              <span className="text-emerald-600 dark:text-emerald-400 font-semibold">Gemini</span>
                            </>
                          )}
                        </div>
                      </div>
                    );
                  })}

                  {/* Processing indicator */}
                  {isSending && (
                    <div className="flex flex-col mr-auto items-start max-w-[85%]">
                      <div className="p-3 rounded-2xl bg-white dark:bg-[#090E18] border border-slate-200 dark:border-slate-800 rounded-tl-none text-xs flex items-center gap-2 text-slate-500 dark:text-slate-400">
                        <Loader2 className="w-3.5 h-3.5 animate-spin text-emerald-500" />
                        <span>{t('adminAi.processing')}</span>
                      </div>
                    </div>
                  )}

                  <div ref={messagesEndRef} />
                </div>
              )}
            </div>

            {/* Error Banner in Chat */}
            {chatError && (
              <div className="px-4 py-2 bg-rose-50 dark:bg-rose-950/40 border-t border-rose-200 dark:border-rose-900/50 text-xs text-rose-700 dark:text-rose-300 flex items-center justify-between shrink-0">
                <div className="flex items-center gap-2">
                  <AlertCircle className="w-3.5 h-3.5 text-rose-500 shrink-0" />
                  <span>{chatError}</span>
                </div>
                <button
                  type="button"
                  onClick={() => setChatError(null)}
                  className="text-rose-500 hover:text-rose-700 dark:hover:text-rose-300 p-0.5"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              </div>
            )}

            {/* 3. Composer / Input Area */}
            <div className="p-3 border-t border-slate-200 dark:border-slate-800 bg-white dark:bg-[#090E18] shrink-0">
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  handleSendMessage();
                }}
                className="flex items-center gap-2"
              >
                <div className="relative flex-1">
                  <input
                    ref={inputRef}
                    type="text"
                    value={inputMessage}
                    onChange={(e) => setInputMessage(e.target.value)}
                    placeholder={t('adminAi.inputPlaceholder')}
                    disabled={isSending}
                    className="w-full px-3.5 py-2 text-xs bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl text-slate-800 dark:text-slate-200 placeholder-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500 disabled:opacity-60"
                  />
                </div>

                <button
                  type="submit"
                  disabled={!inputMessage.trim() || isSending}
                  className="flex items-center justify-center h-8.5 w-8.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-200 dark:disabled:bg-slate-800 text-white disabled:text-slate-400 transition shadow-xs disabled:shadow-none shrink-0 cursor-pointer disabled:cursor-not-allowed"
                >
                  {isSending ? (
                    <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  ) : (
                    <Send className="w-3.5 h-3.5" />
                  )}
                </button>
              </form>
            </div>
          </div>
        </div>
      )}
    </>
  );
};
