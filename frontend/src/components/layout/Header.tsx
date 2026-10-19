import React, { useState, useEffect, useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  Bell,
  Search,
  Sun,
  Moon,
  Laptop,
  Globe,
  X,
  ArrowRight,
  ArrowLeft,
  Send,
  UserPlus,
  Check,
  UserCheck,
  Clock,
  AlertCircle,
  Loader2,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '../../providers/ThemeProvider';
import { useAuth } from '../../providers/AuthProvider';
import { useNotifications } from '../../providers/NotificationProvider';
import { friendService } from '../../services/friendService';
import { friendsApiService, FriendContactDto } from '../../services/friendsApiService';
import { deriveInitials } from '../../services/authMapper';
import { AddFriendModal } from '../../features/friends/modals/AddFriendModal';
import { NotificationsDropdown } from './NotificationsDropdown';
import { UserDropdown } from './UserDropdown';
import { Tooltip } from '../ui/Tooltip';
import { cn } from '../../utils/cn';

interface HeaderProps {
  pageTitle?: string;
  pageSubtitle?: string;
}

export const Header: React.FC<HeaderProps> = ({ pageTitle, pageSubtitle }) => {
  const { locale, setLocale, t } = useI18n();
  const { theme, setTheme } = useTheme();
  const { currentUser } = useAuth();
  const { unreadCount } = useNotifications();
  const location = useLocation();
  const navigate = useNavigate();

  const [isNotifOpen, setIsNotifOpen] = useState(false);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [isSearchModalOpen, setIsSearchModalOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedContact, setSelectedContact] = useState<FriendContactDto | null>(null);
  const [isActionViewOpen, setIsActionViewOpen] = useState(false);
  const [, setFriendTick] = useState(0);

  // Search state for contact lookup
  const [searchContactResult, setSearchContactResult] = useState<FriendContactDto | null>(null);
  const [isSearchingContact, setIsSearchingContact] = useState(false);
  const [searchContactError, setSearchContactError] = useState<string | null>(null);

  // Subscribe to friendship updates
  useEffect(() => {
    return friendService.subscribe(() => setFriendTick((t) => t + 1));
  }, []);

  // Close modal and reset search state
  const handleCloseModal = () => {
    setIsSearchModalOpen(false);
    setSearchQuery('');
    setSelectedContact(null);
    setIsActionViewOpen(false);
    setSearchContactResult(null);
    setSearchContactError(null);
    setIsSearchingContact(false);
  };

  // Keyboard shortcut: Ctrl+K or Cmd+K to open search, Escape to close/back
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setIsSearchModalOpen((prev) => {
          if (!prev) {
            setIsActionViewOpen(false);
          }
          return !prev;
        });
      }
      if (e.key === 'Escape') {
        if (isActionViewOpen) {
          setIsActionViewOpen(false);
        } else {
          handleCloseModal();
        }
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isActionViewOpen]);

  // Perform backend contact lookup with debounce
  useEffect(() => {
    const trimmed = searchQuery.trim();
    if (!trimmed || !isSearchModalOpen) {
      setSearchContactResult(null);
      setSearchContactError(null);
      setIsSearchingContact(false);
      return;
    }

    let isCancelled = false;
    const timer = setTimeout(async () => {
      setIsSearchingContact(true);
      setSearchContactError(null);
      try {
        const res = await friendsApiService.searchFriendContact(trimmed);
        if (!isCancelled) {
          setSearchContactResult(res);
        }
      } catch (err: unknown) {
        if (!isCancelled) {
          const apiErr = err as { code?: string; title?: string; detail?: string };
          if (
            apiErr.code === 'contact_not_found' ||
            apiErr.title?.toLowerCase().includes('not found') ||
            apiErr.detail?.toLowerCase().includes('not found')
          ) {
            setSearchContactError(
              locale === 'vi-VN'
                ? 'Không tìm thấy người dùng phù hợp với email, số điện thoại hoặc Spend ID này.'
                : 'Eligible contact not found.'
            );
          } else {
            setSearchContactError(apiErr.detail || apiErr.title || (locale === 'vi-VN' ? 'Lỗi khi tìm kiếm.' : 'Search failed.'));
          }
          setSearchContactResult(null);
        }
      } finally {
        if (!isCancelled) {
          setIsSearchingContact(false);
        }
      }
    }, 400);

    return () => {
      isCancelled = true;
      clearTimeout(timer);
    };
  }, [searchQuery, isSearchModalOpen, locale]);

  // Navigate to Transfer with selected contact (Bank-Direct transfer only)
  const handleTransfer = (contact: FriendContactDto) => {
    handleCloseModal();
    if (contact.phone) {
      navigate(`/transfer?recipientPhone=${encodeURIComponent(contact.phone)}`);
    } else {
      navigate('/transfer');
    }
  };

  // State for Add Friend Modal
  const [isAddFriendModalOpen, setIsAddFriendModalOpen] = useState(false);
  const [friendModalContact, setFriendModalContact] = useState<{
    id?: string;
    name: string;
    contact?: string;
    spendId?: string | null;
    email?: string | null;
    phone?: string | null;
    avatarInitials?: string;
  } | null>(null);

  const handleOpenAddFriend = (contact: FriendContactDto) => {
    setFriendModalContact({
      id: contact.userId,
      name: contact.fullName,
      contact: searchQuery.trim() || contact.phone || contact.spendId,
      spendId: contact.spendId,
      email: null,
      phone: contact.phone,
      avatarInitials: deriveInitials(contact.fullName),
    });
    setIsAddFriendModalOpen(true);
  };

  // Determine fallback title from route if not provided
  const getRouteTitle = () => {
    if (pageTitle) return pageTitle;
    const path = location.pathname;
    if (path === '/' || path === '/dashboard') return t('navigation.overview');
    if (path.startsWith('/wallet')) return t('navigation.wallet');
    if (path.startsWith('/transactions')) return t('navigation.transactions');
    if (path.startsWith('/transfer')) return t('navigation.transfer');
    if (path.startsWith('/budget')) return t('navigation.budget');
    if (path.startsWith('/goals')) return t('navigation.goals');
    if (path.startsWith('/groups')) return t('navigation.groups');
    if (path.startsWith('/friends')) return locale === 'vi-VN' ? 'Bạn bè' : 'Friends';
    if (path.startsWith('/notifications')) return t('common.notifications');
    if (path.startsWith('/settings')) return t('navigation.settings');
    return t('common.appName');
  };

  return (
    <header className="sticky top-0 z-20 flex h-16 w-full items-center justify-between border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 px-6 sm:px-8">
      {/* Left: Page Title & Contextual Subtitle */}
      <div className="flex flex-col min-w-0">
        <h1 className="text-base sm:text-lg font-semibold text-slate-800 dark:text-white tracking-tight leading-tight truncate">
          {getRouteTitle()}
        </h1>
        <p className="text-xs text-slate-500 dark:text-slate-400 truncate hidden sm:block">
          {pageSubtitle || (locale === 'vi-VN' ? 'Tình hình tài chính của bạn hôm nay.' : 'Here is your financial situation today.')}
        </p>
      </div>

      {/* Right: Quick Search + Controls */}
      <div className="flex items-center gap-3 sm:gap-4">
        {/* Quick Search pill trigger */}
        <div className="relative hidden md:block w-56 lg:w-72">
          <input
            type="text"
            readOnly
            onClick={() => {
              setIsSearchModalOpen(true);
              setIsActionViewOpen(false);
            }}
            placeholder={t('common.searchContactPlaceholder')}
            className="w-full h-9 bg-slate-100 dark:bg-slate-800/90 text-slate-800 dark:text-slate-200 rounded-full pl-9 pr-14 text-xs sm:text-sm border-none focus:ring-1 focus:ring-emerald-500 outline-none cursor-pointer placeholder:text-slate-400 truncate"
          />
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400 pointer-events-none" />
          <kbd className="absolute right-2.5 top-1/2 -translate-y-1/2 text-[10px] bg-slate-200/80 dark:bg-slate-700 px-1.5 py-0.5 rounded font-mono text-slate-500 dark:text-slate-300 font-medium pointer-events-none">
            Ctrl K
          </kbd>
        </div>

        {/* Search button for small mobile */}
        <button
          type="button"
          onClick={() => {
            setIsSearchModalOpen(true);
            setIsActionViewOpen(false);
          }}
          className="md:hidden flex h-8 w-8 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
          aria-label={locale === 'vi-VN' ? 'Tìm kiếm liên hệ' : 'Find contact'}
        >
          <Search className="h-4 w-4" />
        </button>

        {/* Notifications Icon with Red dot */}
        <div className="relative">
          <button
            type="button"
            onClick={() => setIsNotifOpen((prev) => !prev)}
            className="relative flex h-8 w-8 items-center justify-center rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer text-slate-500 dark:text-slate-400 transition-colors"
            aria-label={locale === 'vi-VN' ? 'Mở thông báo' : 'Open notifications'}
          >
            <Bell className="h-4 w-4" />
            {unreadCount > 0 && (
              <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-red-500 rounded-full ring-2 ring-white dark:ring-slate-900" />
            )}
          </button>

          <NotificationsDropdown isOpen={isNotifOpen} onClose={() => setIsNotifOpen(false)} />
        </div>

        {/* Vertical divider */}
        <div className="h-6 w-px bg-slate-200 dark:bg-slate-800" />

        {/* 1-Click Language Switcher (Direct VI <-> EN toggle) */}
        <Tooltip content={locale === 'vi-VN' ? 'Chuyển sang tiếng Anh' : 'Switch to Vietnamese'} side="bottom">
          <button
            type="button"
            onClick={() => setLocale(locale === 'vi-VN' ? 'en-US' : 'vi-VN')}
            className="flex items-center gap-1.5 h-8 px-2.5 rounded-lg bg-slate-100 hover:bg-slate-200/80 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 font-bold text-xs transition-colors cursor-pointer select-none"
            aria-label={locale === 'vi-VN' ? 'Chuyển ngôn ngữ' : 'Toggle language'}
          >
            <Globe className="h-3.5 w-3.5 text-slate-500 dark:text-slate-400" />
            <span className="uppercase tracking-wider font-bold">{locale === 'vi-VN' ? 'VI' : 'EN'}</span>
          </button>
        </Tooltip>

        {/* Theme Mode Selector (Light, Dark, System) - Icon Buttons */}
        <div className="flex items-center p-0.5 rounded-lg bg-slate-100 dark:bg-slate-800 border border-slate-200/60 dark:border-slate-700/60">
          <Tooltip content={t('common.light') || 'Light'} side="bottom">
            <button
              type="button"
              onClick={() => setTheme('light')}
              className={cn(
                'flex h-7 w-7 items-center justify-center rounded-md transition-all cursor-pointer',
                theme === 'light'
                  ? 'bg-white dark:bg-slate-900 text-amber-500 shadow-xs font-bold'
                  : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
              )}
              aria-label={locale === 'vi-VN' ? 'Chế độ sáng' : 'Light mode'}
            >
              <Sun className="h-3.5 w-3.5" />
            </button>
          </Tooltip>

          <Tooltip content={t('common.dark') || 'Dark'} side="bottom">
            <button
              type="button"
              onClick={() => setTheme('dark')}
              className={cn(
                'flex h-7 w-7 items-center justify-center rounded-md transition-all cursor-pointer',
                theme === 'dark'
                  ? 'bg-white dark:bg-slate-900 text-sky-400 shadow-xs font-bold'
                  : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
              )}
              aria-label={locale === 'vi-VN' ? 'Chế độ tối' : 'Dark mode'}
            >
              <Moon className="h-3.5 w-3.5" />
            </button>
          </Tooltip>

          <Tooltip content={t('common.system') || 'System'} side="bottom">
            <button
              type="button"
              onClick={() => setTheme('system')}
              className={cn(
                'flex h-7 w-7 items-center justify-center rounded-md transition-all cursor-pointer',
                theme === 'system'
                  ? 'bg-white dark:bg-slate-900 text-emerald-500 shadow-xs font-bold'
                  : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
              )}
              aria-label={locale === 'vi-VN' ? 'Theo hệ thống' : 'System mode'}
            >
              <Laptop className="h-3.5 w-3.5" />
            </button>
          </Tooltip>
        </div>

        {/* User Profile Avatar & Dropdown */}
        <div className="relative">
          <button
            type="button"
            onClick={() => setIsUserMenuOpen((prev) => !prev)}
            className="flex items-center gap-1.5 p-0.5 rounded-full hover:ring-2 hover:ring-slate-200 dark:hover:ring-slate-700 transition-all cursor-pointer"
            aria-label={locale === 'vi-VN' ? 'Menu tài khoản người dùng' : 'User profile menu'}
          >
            <div className="flex h-7 w-7 items-center justify-center rounded-full bg-emerald-600 text-white font-bold text-xs shadow-sm">
              {currentUser?.avatarInitials || 'U'}
            </div>
          </button>

          <UserDropdown isOpen={isUserMenuOpen} onClose={() => setIsUserMenuOpen(false)} />
        </div>
      </div>

      {/* Contact Search Modal */}
      {isSearchModalOpen && (
        <div className="fixed inset-0 z-50 flex items-start justify-center pt-24 bg-stone-900/40 dark:bg-black/60 backdrop-blur-xs p-4 animate-in fade-in-0">
          {isActionViewOpen && selectedContact ? (
            /* Contact Action View */
            <div className="w-full max-w-lg rounded-2xl border border-stone-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-2xl overflow-hidden animate-in zoom-in-95">
              {/* Action View Header */}
              <div className="flex items-center justify-between px-4 py-3 border-b border-stone-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40">
                <button
                  type="button"
                  onClick={() => setIsActionViewOpen(false)}
                  className="flex items-center gap-1.5 text-xs font-medium text-slate-600 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white cursor-pointer transition-colors"
                >
                  <ArrowLeft className="w-3.5 h-3.5" />
                  <span>{locale === 'vi-VN' ? 'Quay lại tìm kiếm' : 'Back to search'}</span>
                </button>
                <button
                  type="button"
                  onClick={handleCloseModal}
                  className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 cursor-pointer"
                  aria-label="Close"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>

              {/* Contact Profile Summary */}
              <div className="p-6 flex flex-col items-center text-center">
                <div className="w-16 h-16 rounded-full flex items-center justify-center bg-emerald-600 text-white font-bold text-xl shadow-md mb-3">
                  {deriveInitials(selectedContact.fullName)}
                </div>
                <h3 className="font-semibold text-base text-slate-900 dark:text-white">
                  {selectedContact.fullName}
                </h3>
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 font-mono">
                  @{selectedContact.spendId}
                </p>
                {selectedContact.phone && (
                  <p className="text-xs font-mono text-slate-500 dark:text-slate-400 mt-0.5">
                    {selectedContact.phone}
                  </p>
                )}

                {/* Primary Actions: Transfer & Add Friend */}
                <div className="grid grid-cols-2 gap-3 w-full mt-6 pt-4 border-t border-slate-100 dark:border-slate-800">
                  <button
                    type="button"
                    onClick={() => handleTransfer(selectedContact)}
                    className="flex items-center justify-center gap-2 py-2.5 px-4 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs shadow-xs transition-colors cursor-pointer"
                  >
                    <Send className="w-3.5 h-3.5" />
                    <span>{t('common.contactTransfer')}</span>
                  </button>

                  {selectedContact.state === 'FRIENDS' || friendService.getFriendshipStatus(currentUser?.id || '', selectedContact.userId) === 'FRIENDS' ? (
                    <div className="flex items-center justify-center gap-1.5 py-2.5 px-4 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 font-semibold text-xs border border-slate-200/60 dark:border-slate-700/60">
                      <Check className="w-3.5 h-3.5 text-emerald-500" />
                      <span>{locale === 'vi-VN' ? 'Bạn bè' : 'Friends'}</span>
                    </div>
                  ) : selectedContact.state === 'REQUEST_SENT' || friendService.getFriendshipStatus(currentUser?.id || '', selectedContact.userId) === 'REQUEST_SENT' ? (
                    <div className="flex items-center justify-center gap-1.5 py-2.5 px-4 rounded-xl bg-amber-50 dark:bg-amber-950/60 text-amber-700 dark:text-amber-400 font-semibold text-xs border border-amber-200 dark:border-amber-800/60">
                      <Clock className="w-3.5 h-3.5" />
                      <span>{locale === 'vi-VN' ? 'Đã gửi lời mời' : 'Request Sent'}</span>
                    </div>
                  ) : selectedContact.state === 'REQUEST_RECEIVED' || friendService.getFriendshipStatus(currentUser?.id || '', selectedContact.userId) === 'REQUEST_RECEIVED' ? (
                    <button
                      type="button"
                      onClick={() => {
                        handleCloseModal();
                        navigate('/friends?tab=received');
                      }}
                      className="flex items-center justify-center gap-1.5 py-2.5 px-4 rounded-xl bg-indigo-50 hover:bg-indigo-100 dark:bg-indigo-950/60 dark:hover:bg-indigo-900/60 text-indigo-700 dark:text-indigo-300 font-semibold text-xs transition-colors cursor-pointer border border-indigo-200 dark:border-indigo-800/60"
                    >
                      <UserCheck className="w-3.5 h-3.5" />
                      <span>{locale === 'vi-VN' ? 'Phản hồi lời mời' : 'Respond to Request'}</span>
                    </button>
                  ) : selectedContact.state === 'SELF' ? (
                    <div className="flex items-center justify-center gap-1.5 py-2.5 px-4 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-500 font-semibold text-xs">
                      <span>{locale === 'vi-VN' ? 'Tài khoản của bạn' : 'Your Account'}</span>
                    </div>
                  ) : (
                    <button
                      type="button"
                      onClick={() => handleOpenAddFriend(selectedContact)}
                      className="flex items-center justify-center gap-2 py-2.5 px-4 rounded-xl bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200 font-semibold text-xs transition-colors cursor-pointer border border-slate-200/60 dark:border-slate-700/60"
                    >
                      <UserPlus className="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400" />
                      <span>{locale === 'vi-VN' ? 'Kết bạn' : 'Add Friend'}</span>
                    </button>
                  )}
                </div>
              </div>
            </div>
          ) : (
            /* Contact Search Input & Result View */
            <div className="w-full max-w-lg rounded-2xl border border-stone-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-2xl overflow-hidden animate-in zoom-in-95">
              {/* Modal Title Bar */}
              <div className="flex items-center justify-between px-4 py-2.5 border-b border-stone-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40 text-xs font-semibold text-slate-600 dark:text-slate-300">
                <span>{t('common.findContactModalTitle')}</span>
                <span className="text-[10px] bg-slate-200/70 dark:bg-slate-800 px-1.5 py-0.5 rounded font-mono text-slate-500 dark:text-slate-400">
                  ESC
                </span>
              </div>

              {/* Search Input Bar */}
              <div className="flex items-center px-4 py-3 border-b border-stone-100 dark:border-slate-800">
                <Search className="h-4 w-4 text-stone-400 mr-3 shrink-0" />
                <input
                  type="text"
                  autoFocus
                  placeholder={t('common.searchContactPlaceholder')}
                  value={searchQuery}
                  onChange={(e) => {
                    setSearchQuery(e.target.value);
                  }}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault();
                      if (searchContactResult) {
                        setSelectedContact(searchContactResult);
                        setIsActionViewOpen(true);
                      }
                    } else if (e.key === 'Escape') {
                      e.preventDefault();
                      handleCloseModal();
                    }
                  }}
                  className="w-full text-sm bg-transparent text-stone-900 dark:text-white placeholder:text-stone-400 focus:outline-none"
                />
                {searchQuery && (
                  <button
                    type="button"
                    onClick={() => {
                      setSearchQuery('');
                      setSearchContactResult(null);
                      setSearchContactError(null);
                    }}
                    className="text-stone-400 hover:text-stone-600 dark:hover:text-slate-300 mr-2 cursor-pointer p-0.5"
                    aria-label="Clear"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                )}
                <button
                  type="button"
                  onClick={handleCloseModal}
                  className="text-stone-400 hover:text-stone-600 dark:hover:text-slate-300 cursor-pointer"
                  aria-label="Close"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>

              {/* Modal Body / Results */}
              <div className="p-3 text-xs min-h-[160px] flex flex-col justify-center">
                {/* 1. Empty initial state */}
                {!searchQuery.trim() && (
                  <div className="py-8 px-4 flex flex-col items-center text-center">
                    <div className="w-10 h-10 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-400 flex items-center justify-center mb-2.5">
                      <Search className="w-5 h-5" />
                    </div>
                    <p className="text-xs text-slate-500 dark:text-slate-400 max-w-xs leading-relaxed">
                      {locale === 'vi-VN'
                        ? 'Nhập chính xác email, số điện thoại hoặc Spend ID để tìm kiếm người dùng.'
                        : 'Search by exact email, phone number, or Spend ID.'}
                    </p>
                  </div>
                )}

                {/* 2. Loading state */}
                {searchQuery.trim() && isSearchingContact && (
                  <div className="py-8 px-4 flex flex-col items-center text-center">
                    <Loader2 className="w-6 h-6 animate-spin text-emerald-600 mb-2.5" />
                    <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
                      {locale === 'vi-VN' ? 'Đang tìm kiếm...' : 'Searching...'}
                    </p>
                  </div>
                )}

                {/* 3. Error / Not found */}
                {searchQuery.trim() && !isSearchingContact && searchContactError && (
                  <div className="py-8 px-4 flex flex-col items-center text-center">
                    <div className="w-10 h-10 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-400 flex items-center justify-center mb-2.5">
                      <Search className="w-5 h-5" />
                    </div>
                    <p className="text-xs font-medium text-slate-700 dark:text-slate-300 max-w-xs leading-relaxed">
                      {searchContactError}
                    </p>
                  </div>
                )}

                {/* 4. Contact found card */}
                {searchQuery.trim() && !isSearchingContact && searchContactResult && (
                  <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-700/80 space-y-3">
                    <div className="flex items-center justify-between gap-3">
                      <div className="flex items-center gap-3 min-w-0">
                        <div className="w-11 h-11 rounded-full flex items-center justify-center shrink-0 bg-emerald-600 text-white font-bold text-sm shadow-xs">
                          {deriveInitials(searchContactResult.fullName)}
                        </div>
                        <div className="min-w-0">
                          <div className="font-semibold text-sm text-slate-900 dark:text-white truncate">
                            {searchContactResult.fullName}
                          </div>
                          <div className="text-xs text-slate-500 dark:text-slate-400 truncate flex items-center gap-2 mt-0.5 font-mono">
                            <span>@{searchContactResult.spendId}</span>
                            {searchContactResult.phone && (
                              <>
                                <span className="text-slate-300 dark:text-slate-600">•</span>
                                <span>{searchContactResult.phone}</span>
                              </>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>

                    {/* Actions: Transfer & Add Friend */}
                    <div className="flex items-center justify-end gap-2 pt-2 border-t border-slate-200/60 dark:border-slate-700/60">
                      {searchContactResult.state === 'FRIENDS' || friendService.getFriendshipStatus(currentUser?.id || '', searchContactResult.userId) === 'FRIENDS' ? (
                        <span className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-slate-200/70 dark:bg-slate-700 text-slate-700 dark:text-slate-200 text-xs font-semibold">
                          <Check className="w-3.5 h-3.5 text-emerald-500" />
                          <span>{locale === 'vi-VN' ? 'Bạn bè' : 'Friends'}</span>
                        </span>
                      ) : searchContactResult.state === 'REQUEST_SENT' || friendService.getFriendshipStatus(currentUser?.id || '', searchContactResult.userId) === 'REQUEST_SENT' ? (
                        <span className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-amber-50 dark:bg-amber-950/60 text-amber-700 dark:text-amber-400 text-xs font-semibold">
                          <Clock className="w-3.5 h-3.5" />
                          <span>{locale === 'vi-VN' ? 'Đã gửi lời mời' : 'Sent'}</span>
                        </span>
                      ) : searchContactResult.state === 'REQUEST_RECEIVED' || friendService.getFriendshipStatus(currentUser?.id || '', searchContactResult.userId) === 'REQUEST_RECEIVED' ? (
                        <button
                          type="button"
                          onClick={() => {
                            handleCloseModal();
                            navigate('/friends?tab=received');
                          }}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-indigo-50 hover:bg-indigo-100 dark:bg-indigo-950/60 dark:hover:bg-indigo-900/60 text-indigo-700 dark:text-indigo-300 text-xs font-semibold transition cursor-pointer border border-indigo-200 dark:border-indigo-800/60"
                        >
                          <UserCheck className="w-3.5 h-3.5" />
                          <span>{locale === 'vi-VN' ? 'Phản hồi lời mời' : 'Respond'}</span>
                        </button>
                      ) : searchContactResult.state === 'SELF' ? (
                        <span className="inline-flex items-center px-3 py-1.5 rounded-lg bg-slate-200/70 dark:bg-slate-700 text-slate-700 dark:text-slate-200 text-xs font-semibold">
                          <span>{locale === 'vi-VN' ? 'Tài khoản của bạn' : 'Your Account'}</span>
                        </span>
                      ) : (
                        <button
                          type="button"
                          onClick={() => handleOpenAddFriend(searchContactResult)}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-200/80 hover:bg-slate-300/80 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200 text-xs font-semibold transition cursor-pointer"
                        >
                          <UserPlus className="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400" />
                          <span>{locale === 'vi-VN' ? 'Kết bạn' : 'Add Friend'}</span>
                        </button>
                      )}

                      <button
                        type="button"
                        onClick={() => handleTransfer(searchContactResult)}
                        className="flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-xs transition cursor-pointer"
                      >
                        <Send className="w-3 h-3" />
                        <span>{t('common.contactTransfer')}</span>
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Add Friend Modal */}
      <AddFriendModal
        isOpen={isAddFriendModalOpen}
        onClose={() => {
          setIsAddFriendModalOpen(false);
          setFriendModalContact(null);
        }}
        targetUser={friendModalContact}
        onSuccess={() => {
          handleCloseModal();
        }}
      />
    </header>
  );
};
