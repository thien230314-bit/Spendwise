import React, { useState, useEffect, useRef } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import {
  Bell,
  CheckCheck,
  ArrowUpRight,
  ArrowDownLeft,
  AlertTriangle,
  Calendar,
  Users,
  ShieldAlert,
  Wallet,
  ShieldCheck,
  CreditCard,
  Building2,
  ChevronRight,
  SlidersHorizontal,
  UserCheck,
  UserX,
  Check,
  XCircle,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { useNotifications } from '../../providers/NotificationProvider';
import { friendService } from '../../services/friendService';
import { NotificationItem } from '../../types';
import { cn } from '../../utils/cn';
import {
  getLocalizedWalletName,
  getLocalizedAccountDisplayName,
  getLocalizedNotificationTime,
  getLocalizedNotificationItem,
} from '../../utils/localizationHelpers';

interface NotificationsDropdownProps {
  isOpen: boolean;
  onClose: () => void;
}

export const NotificationsDropdown: React.FC<NotificationsDropdownProps> = ({ isOpen, onClose }) => {
  const { t, locale } = useI18n();
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const {
    notifications,
    unreadCount,
    unreadByAccount,
    markAsRead,
    markAllAsRead,
    accountList,
  } = useNotifications();

  const [activeAccountTab, setActiveAccountTab] = useState<string>('all');
  const [, setFriendTick] = useState(0);
  const [busyFriendReqIds, setBusyFriendReqIds] = useState<Set<string>>(new Set());
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Subscribe to friend service changes so buttons update immediately on accept/reject
  useEffect(() => {
    return friendService.subscribe(() => setFriendTick((t) => t + 1));
  }, []);

  const getAccountDisplayName = (accountId?: string, accountName?: string, accountType?: string): string => {
    return getLocalizedAccountDisplayName(accountId, accountName, locale, accountType);
  };

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isOpen, onClose]);

  const [isMarkingAll, setIsMarkingAll] = useState(false);

  const handleMarkAllRead = async (e: React.MouseEvent) => {
    e.stopPropagation();
    if (isMarkingAll) return;
    setIsMarkingAll(true);
    try {
      await markAllAsRead(activeAccountTab === 'all' ? undefined : activeAccountTab);
    } catch (err) {
      console.error('Failed to mark all as read from dropdown', err);
    } finally {
      setIsMarkingAll(false);
    }
  };

  const handleItemClick = async (item: NotificationItem) => {
    try {
      if (!item.isRead) {
        await markAsRead(item.id);
      }
    } catch (err) {
      console.error('Failed to mark notification as read from dropdown', err);
    }
    if (item.actionRoute) {
      onClose();
      navigate(item.actionRoute);
    }
  };

  const getNotificationIcon = (item: NotificationItem) => {
    if (item.amountChange && item.amountChange > 0) {
      return <ArrowDownLeft className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />;
    }
    if (item.amountChange && item.amountChange < 0) {
      return <ArrowUpRight className="h-4 w-4 text-rose-600 dark:text-rose-400" />;
    }
    if (item.type === 'friend_rejected') {
      return <UserX className="h-4 w-4 text-rose-600 dark:text-rose-400" />;
    }
    if (item.type === 'friend_accepted') {
      return <UserCheck className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />;
    }
    if (item.accountId === 'system-risk') {
      return <ShieldAlert className="h-4 w-4 text-rose-600 dark:text-rose-400" />;
    }
    if (item.accountId === 'system-support') {
      return <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400" />;
    }
    if (item.accountId === 'system-audit') {
      return <ShieldCheck className="h-4 w-4 text-sky-600 dark:text-sky-400" />;
    }
    if (item.statusVariant === 'danger') {
      return <ShieldAlert className="h-4 w-4 text-rose-600 dark:text-rose-400" />;
    }
    if (item.statusVariant === 'success') {
      return <UserCheck className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />;
    }
    switch (item.type) {
      case 'payment':
      case 'transfer':
        return <ArrowDownLeft className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />;
      case 'budget':
        return <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400" />;
      case 'subscription':
        return <Calendar className="h-4 w-4 text-sky-600 dark:text-sky-400" />;
      case 'group':
        return <Users className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />;
      case 'friend_request':
        return <Users className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />;
      case 'security':
        return <ShieldCheck className="h-4 w-4 text-purple-600 dark:text-purple-400" />;
      default:
        return <ShieldAlert className="h-4 w-4 text-stone-600 dark:text-slate-400" />;
    }
  };

  const getAccountBadgeColor = (type?: string) => {
    switch (type) {
      case 'spending':
        return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800/40';
      case 'savings':
        return 'bg-teal-50 text-teal-700 dark:bg-teal-950/60 dark:text-teal-300 border-teal-200 dark:border-teal-800/40';
      case 'emergency':
        return 'bg-indigo-50 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300 border-indigo-200 dark:border-indigo-800/40';
      case 'security':
        return 'bg-purple-50 text-purple-700 dark:bg-purple-950/60 dark:text-purple-300 border-purple-200 dark:border-purple-800/40';
      case 'friend':
        return 'bg-indigo-50 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300 border-indigo-200 dark:border-indigo-800/40';
      case 'system':
        return 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300 border-slate-200 dark:border-slate-700';
      default:
        return 'bg-stone-100 text-stone-700 dark:bg-slate-800 dark:text-slate-300 border-stone-200 dark:border-slate-700';
    }
  };

  if (!isOpen) return null;

  // Filter dropdown notifications by selected account tab
  const displayedNotifications = notifications.filter((n) => {
    if (activeAccountTab === 'all') return true;
    return n.accountId === activeAccountTab;
  });

  const tabUnreadCount =
    activeAccountTab === 'all'
      ? unreadCount
      : unreadByAccount[activeAccountTab] || 0;

  return (
    <div
      ref={dropdownRef}
      className="absolute right-0 top-full mt-2 w-84 sm:w-96 rounded-2xl border border-stone-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-2xl z-50 overflow-hidden animate-in fade-in-0 zoom-in-95 duration-150"
    >
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-stone-100 dark:border-slate-800 bg-stone-50/70 dark:bg-slate-900/80">
        <div className="flex items-center gap-2">
          <span className="text-sm font-bold text-stone-900 dark:text-white">
            {t('common.notifications')}
          </span>
          {tabUnreadCount > 0 && (
            <span className="text-[11px] font-bold px-2 py-0.5 rounded-full bg-emerald-500 text-white shadow-2xs">
              {tabUnreadCount}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {tabUnreadCount > 0 && (
            <button
              onClick={handleMarkAllRead}
              className="flex items-center gap-1 text-xs text-stone-500 hover:text-emerald-600 dark:text-slate-400 dark:hover:text-emerald-400 font-semibold transition-colors cursor-pointer"
            >
              <CheckCheck className="h-3.5 w-3.5" />
              <span>{t('common.markAllRead')}</span>
            </button>
          )}
        </div>
      </div>

      {/* Account Switcher Bar (Filter by Account in Dropdown) */}
      <div className="flex items-center gap-1.5 px-3 py-2 border-b border-stone-100 dark:border-slate-800/80 overflow-x-auto no-scrollbar bg-white dark:bg-slate-900">
        {accountList.slice(0, 4).map((acc) => {
          const isSelected = activeAccountTab === acc.id;
          const count = acc.id === 'all' ? unreadCount : unreadByAccount[acc.id] || 0;
          return (
            <button
              key={acc.id}
              onClick={() => setActiveAccountTab(acc.id)}
              className={cn(
                'flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-semibold whitespace-nowrap transition-all cursor-pointer',
                isSelected
                  ? 'bg-emerald-600 text-white shadow-2xs font-bold'
                  : 'bg-stone-100 dark:bg-slate-800/80 text-stone-600 dark:text-slate-400 hover:bg-stone-200 dark:hover:bg-slate-700'
              )}
            >
              <span>{getAccountDisplayName(acc.id, acc.name).split(' (')[0]}</span>
              {count > 0 && (
                <span
                  className={cn(
                    'text-[10px] px-1 py-0.2 rounded-full font-bold',
                    isSelected
                      ? 'bg-white/25 text-white'
                      : 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300'
                  )}
                >
                  {count}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* List */}
      <div className="max-h-84 overflow-y-auto divide-y divide-stone-100 dark:divide-slate-800/60">
        {displayedNotifications.length === 0 ? (
          <div className="p-8 text-center">
            <Bell className="h-8 w-8 text-stone-300 dark:text-slate-600 mx-auto mb-2 opacity-50" />
            <p className="text-xs font-semibold text-stone-500 dark:text-slate-400">
              {t('notifications.noNotificationsCategory')}
            </p>
          </div>
        ) : (
          displayedNotifications.map((item) => {
            const isDanger = item.statusVariant === 'danger' || item.type === 'friend_rejected';
            const isSuccess = item.statusVariant === 'success' || item.type === 'friend_accepted';
            const friendReq = item.friendRequestId ? friendService.getRequestById(item.friendRequestId) : undefined;
            const localized = getLocalizedNotificationItem(item, locale, t);

            return (
              <div
                key={item.id}
                onClick={() => handleItemClick(item)}
                className={cn(
                  'flex items-start gap-3 p-3.5 hover:bg-stone-50 dark:hover:bg-slate-800/50 transition-colors cursor-pointer group',
                  !item.isRead && (
                    isDanger
                      ? 'bg-rose-50/40 dark:bg-rose-950/20 border-l-2 border-l-rose-500'
                      : isSuccess
                      ? 'bg-emerald-50/40 dark:bg-emerald-950/20 border-l-2 border-l-emerald-500'
                      : 'bg-emerald-50/30 dark:bg-emerald-950/20'
                  )
                )}
              >
                <div
                  className={cn(
                    'flex h-8 w-8 shrink-0 items-center justify-center rounded-xl mt-0.5',
                    isDanger
                      ? 'bg-rose-100 dark:bg-rose-950/60'
                      : isSuccess
                      ? 'bg-emerald-100 dark:bg-emerald-950/60'
                      : 'bg-stone-100 dark:bg-slate-800'
                  )}
                >
                  {getNotificationIcon(item)}
                </div>

                <div className="flex-1 min-w-0">
                  {/* Account Tag & Time */}
                  <div className="flex items-center justify-between gap-1 mb-1">
                    {item.accountName && (
                      <span
                        className={cn(
                          'text-[10px] font-bold px-1.5 py-0.5 rounded border leading-none',
                          isDanger
                            ? 'bg-rose-50 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300 border-rose-200 dark:border-rose-800/40'
                            : isSuccess
                            ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800/40'
                            : getAccountBadgeColor(item.accountType)
                        )}
                      >
                        {getLocalizedAccountDisplayName(item.accountId, item.accountName, locale, item.accountType)}
                      </span>
                    )}
                    <span className="text-[10px] text-stone-400 dark:text-slate-500 whitespace-nowrap ml-auto">
                      {getLocalizedNotificationTime(item.time, item.timestamp, locale)}
                    </span>
                  </div>

                  <div className="flex items-start justify-between gap-2">
                    <div className="flex-1 min-w-0">
                      {localized.title && (
                        <p
                          className={cn(
                            'text-xs leading-snug',
                            isDanger
                              ? 'text-rose-700 dark:text-rose-300'
                              : 'text-stone-900 dark:text-slate-200',
                            !item.isRead ? 'font-bold' : 'font-semibold'
                          )}
                        >
                          {localized.title}
                        </p>
                      )}
                      <p
                        className={cn(
                          'text-xs leading-snug',
                          localized.title
                            ? 'text-stone-600 dark:text-slate-400 mt-0.5 text-[11px]'
                            : isDanger
                            ? 'text-rose-700 dark:text-rose-300'
                            : 'text-stone-900 dark:text-slate-200',
                          !localized.title && !item.isRead ? 'font-bold' : 'font-normal'
                        )}
                      >
                        {localized.message}
                      </p>
                    </div>
                    {!item.isRead && (
                      <span
                        className={cn(
                          'h-2 w-2 rounded-full shrink-0 mt-1',
                          isDanger ? 'bg-rose-500' : 'bg-emerald-500'
                        )}
                      />
                    )}
                  </div>

                  {/* Friend Request Accept / Reject Inline Actions */}
                  {item.type === 'friend_request' && (
                    <>
                      {friendReq && friendReq.status === 'pending' ? (
                        <div className="flex items-center gap-2 mt-2 pt-1">
                          <button
                            type="button"
                            disabled={busyFriendReqIds.has(friendReq.id)}
                            onClick={async (e) => {
                              e.stopPropagation();
                              setBusyFriendReqIds((prev) => new Set(prev).add(friendReq.id));
                              try {
                                const res = await friendService.acceptFriendRequestAsync(friendReq.id);
                                if (res.success) {
                                  markAsRead(item.id);
                                }
                              } finally {
                                setBusyFriendReqIds((prev) => {
                                  const next = new Set(prev);
                                  next.delete(friendReq.id);
                                  return next;
                                });
                              }
                            }}
                            className="px-2.5 py-1 rounded-lg bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-[11px] font-semibold transition cursor-pointer shadow-2xs"
                          >
                            {busyFriendReqIds.has(friendReq.id)
                              ? locale === 'vi-VN'
                                ? 'Đang xử lý...'
                                : 'Processing...'
                              : locale === 'vi-VN'
                              ? 'Chấp nhận'
                              : 'Accept'}
                          </button>
                          <button
                            type="button"
                            disabled={busyFriendReqIds.has(friendReq.id)}
                            onClick={async (e) => {
                              e.stopPropagation();
                              setBusyFriendReqIds((prev) => new Set(prev).add(friendReq.id));
                              try {
                                const res = await friendService.rejectFriendRequestAsync(friendReq.id);
                                if (res.success) {
                                  markAsRead(item.id);
                                }
                              } finally {
                                setBusyFriendReqIds((prev) => {
                                  const next = new Set(prev);
                                  next.delete(friendReq.id);
                                  return next;
                                });
                              }
                            }}
                            className="px-2.5 py-1 rounded-lg text-rose-600 dark:text-rose-400 bg-rose-50 hover:bg-rose-100 dark:bg-rose-950/50 dark:hover:bg-rose-900/50 border border-rose-200 dark:border-rose-800/80 disabled:opacity-50 disabled:cursor-not-allowed text-[11px] font-semibold transition cursor-pointer"
                          >
                            {locale === 'vi-VN' ? 'Từ chối' : 'Reject'}
                          </button>
                        </div>
                      ) : friendReq && friendReq.status === 'accepted' ? (
                        <div className="mt-1.5 flex items-center gap-1 text-[11px] font-medium text-emerald-600 dark:text-emerald-400">
                          <Check className="w-3 h-3" />
                          <span>{locale === 'vi-VN' ? 'Đã chấp nhận' : 'Accepted'}</span>
                        </div>
                      ) : friendReq && friendReq.status === 'rejected' ? (
                        <div className="mt-1.5 flex items-center gap-1 text-[11px] font-medium text-rose-600 dark:text-rose-400">
                          <XCircle className="w-3 h-3" />
                          <span>{locale === 'vi-VN' ? 'Đã từ chối' : 'Rejected'}</span>
                        </div>
                      ) : null}
                    </>
                  )}

                  {/* Amount Change or Detail */}
                  {item.amountChange !== undefined && (
                    <div className="mt-1 flex items-center gap-2">
                      <span
                        className={cn(
                          'text-xs font-bold',
                          item.amountChange > 0
                            ? 'text-emerald-600 dark:text-emerald-400'
                            : 'text-rose-600 dark:text-rose-400'
                        )}
                      >
                        {item.amountChange > 0 ? '+' : ''}
                        {item.amountChange.toLocaleString(locale)} {locale === 'vi-VN' ? '₫' : 'VND'}
                      </span>
                      {item.balanceAfter !== undefined && (
                        <span className="text-[10px] text-stone-400 dark:text-slate-500">
                          • {t('notifications.balanceAfterLabel')}: {item.balanceAfter.toLocaleString(locale)} {locale === 'vi-VN' ? '₫' : 'VND'}
                        </span>
                      )}
                    </div>
                  )}

                  {localized.detail && !item.amountChange && (
                    <p className="text-[11px] text-stone-500 dark:text-slate-400 mt-0.5 line-clamp-1 italic">
                      {localized.detail}
                    </p>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Footer */}
      <div className="p-2.5 border-t border-stone-100 dark:border-slate-800 bg-stone-50/70 dark:bg-slate-900/80 flex items-center justify-between">
        {currentUser?.role !== 'SystemAdmin' && <NavLink
          to="/notifications"
          onClick={onClose}
          className="text-xs font-bold text-emerald-700 hover:text-emerald-800 dark:text-emerald-400 dark:hover:text-emerald-300 flex items-center gap-1 py-0.5 px-2 rounded hover:bg-emerald-50 dark:hover:bg-emerald-950/40 transition-colors"
        >
          <span>{t('common.viewAll')}</span>
          <ChevronRight className="h-3.5 w-3.5" />
        </NavLink>}

        <span className="ml-auto text-[11px] text-stone-400 dark:text-slate-500">
          {displayedNotifications.length} {locale === 'vi-VN' ? 'thông báo' : 'items'}
        </span>
      </div>
    </div>
  );
};
