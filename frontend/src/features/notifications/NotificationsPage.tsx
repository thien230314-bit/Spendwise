import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Bell,
  CheckCheck,
  CheckCircle2,
  AlertTriangle,
  ArrowDownLeft,
  ArrowUpRight,
  Calendar,
  Users,
  ShieldCheck,
  ShieldAlert,
  Wallet,
  PiggyBank,
  SlidersHorizontal,
  Search,
  Trash2,
  Check,
  ExternalLink,
  CreditCard,
  Building2,
  XCircle,
  UserCheck,
  UserX,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { useNotifications, AccountFilterOption } from '../../providers/NotificationProvider';
import { friendService } from '../../services/friendService';
import { NotificationItem } from '../../types';
import { Button } from '../../components/ui/Button';
import { AccountPrefModal } from './AccountPrefModal';
import { cn } from '../../utils/cn';
import {
  getLocalizedWalletName,
  getLocalizedAccountDisplayName,
  getLocalizedNotificationTime,
  getLocalizedActionLabel,
  getLocalizedNotificationItem,
  getLocalizedErrorMessage,
} from '../../utils/localizationHelpers';

import { NotificationsSkeleton } from '../../components/skeletons/NotificationsSkeleton';

export const NotificationsPage: React.FC = () => {
  const { t, locale } = useI18n();
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const [, setFriendTick] = useState<number>(0);
  const [busyFriendReqIds, setBusyFriendReqIds] = useState<Set<string>>(new Set());

  // Subscribe to friendService changes to reactively update button state
  useEffect(() => {
    return friendService.subscribe(() => setFriendTick((t) => t + 1));
  }, []);

  const {
    notifications,
    filteredNotifications,
    unreadCount,
    unreadByAccount,
    selectedAccountId,
    setSelectedAccountId,
    selectedCategory,
    setSelectedCategory,
    searchQuery,
    setSearchQuery,
    isLoading,
    error,
    refreshNotifications,
    markAsRead,
    markAllAsRead,
    deleteNotification,
    clearAll,
    accountList,
  } = useNotifications();

  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [toastType, setToastType] = useState<'success' | 'error'>('success');
  const [isPrefModalOpen, setIsPrefModalOpen] = useState<boolean>(false);
  const [prefModalAccountId, setPrefModalAccountId] = useState<string>('all');
  const [isMutating, setIsMutating] = useState<boolean>(false);

  const getAccountDisplayName = (acc?: { id?: string; name?: string; type?: string }) => {
    if (!acc) return '';
    return getLocalizedAccountDisplayName(acc.id, acc.name, locale, acc.type);
  };

  const getAccountTypeLabel = (type?: string) => {
    switch (type) {
      case 'spending':
        return locale === 'vi-VN' ? 'Chi tiêu' : 'Spending';
      case 'savings':
        return locale === 'vi-VN' ? 'Tiết kiệm' : 'Savings';
      case 'emergency':
        return locale === 'vi-VN' ? 'Dự phòng' : 'Emergency';
      case 'security':
        return locale === 'vi-VN' ? 'Bảo mật' : 'Security';
      case 'system':
        return locale === 'vi-VN' ? 'Hệ thống' : 'System';
      case 'group':
        return locale === 'vi-VN' ? 'Nhóm' : 'Group';
      case 'bank':
        return locale === 'vi-VN' ? 'Ngân hàng' : 'Bank';
      case 'all':
        return locale === 'vi-VN' ? 'Tất cả' : 'All';
      default:
        return type || '';
    }
  };

  const showToast = (msg: string, type: 'success' | 'error' = 'success') => {
    setToastMessage(msg);
    setToastType(type);
    setTimeout(() => setToastMessage(null), 3500);
  };

  const handleMarkAllRead = async () => {
    if (isMutating) return;
    setIsMutating(true);
    try {
      await markAllAsRead(selectedAccountId === 'all' ? undefined : selectedAccountId);
      if (selectedAccountId === 'all') {
        showToast(t('notifications.toastAllRead'));
      } else {
        const acc = accountList.find((a) => a.id === selectedAccountId);
        showToast(t('notifications.toastAccountRead', { account: getAccountDisplayName(acc) || selectedAccountId }));
      }
    } catch (err: any) {
      showToast(err?.message || (locale === 'vi-VN' ? 'Không thể đánh dấu đã đọc' : 'Failed to mark as read'), 'error');
    } finally {
      setIsMutating(false);
    }
  };

  const handleClearAccountNotifications = async () => {
    if (isMutating) return;
    setIsMutating(true);
    try {
      const acc = accountList.find((a) => a.id === selectedAccountId);
      await clearAll(selectedAccountId);
      if (selectedAccountId === 'all') {
        showToast(locale === 'vi-VN' ? 'Đã xóa tất cả thông báo' : 'Cleared all notifications');
      } else {
        showToast(t('notifications.toastAccountCleared', { account: getAccountDisplayName(acc) || selectedAccountId }));
      }
    } catch (err: any) {
      showToast(err?.message || (locale === 'vi-VN' ? 'Không thể xóa thông báo' : 'Failed to clear notifications'), 'error');
    } finally {
      setIsMutating(false);
    }
  };

  const handleDeleteItem = async (e: React.MouseEvent, id: string) => {
    e.stopPropagation();
    try {
      await deleteNotification(id);
      showToast(t('notifications.toastNotifDeleted'));
    } catch (err: any) {
      showToast(err?.message || (locale === 'vi-VN' ? 'Không thể xóa thông báo' : 'Failed to delete notification'), 'error');
    }
  };

  const handleActionClick = async (notif: NotificationItem) => {
    try {
      if (!notif.isRead) {
        await markAsRead(notif.id);
      }
    } catch {
      // Continue navigation even if markAsRead fails
    }
    if (notif.actionRoute) {
      navigate(notif.actionRoute);
    } else {
      const localized = getLocalizedNotificationItem(notif, locale, t);
      showToast(`${t('common.viewDetails')}: ${localized.title}`);
    }
  };

  const handleOpenAccountPrefs = (accountId?: string) => {
    setPrefModalAccountId(accountId || (selectedAccountId !== 'all' ? selectedAccountId : 'w-main'));
    setIsPrefModalOpen(true);
  };

  const getNotificationIcon = (item: NotificationItem) => {
    if (item.amountChange && item.amountChange > 0) {
      return (
        <div className="h-10 w-10 rounded-2xl bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0 shadow-2xs">
          <ArrowDownLeft className="h-5 w-5" />
        </div>
      );
    }
    if (item.amountChange && item.amountChange < 0) {
      return (
        <div className="h-10 w-10 rounded-2xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 flex items-center justify-center shrink-0 shadow-2xs">
          <ArrowUpRight className="h-5 w-5" />
        </div>
      );
    }
    if (item.type === 'friend_rejected') {
      return (
        <div className="h-10 w-10 rounded-2xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 flex items-center justify-center shrink-0 shadow-2xs">
          <UserX className="h-5 w-5" />
        </div>
      );
    }
    if (item.type === 'friend_accepted') {
      return (
        <div className="h-10 w-10 rounded-2xl bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0 shadow-2xs">
          <UserCheck className="h-5 w-5" />
        </div>
      );
    }
    if (item.accountId === 'system-risk') {
      return (
        <div className="h-10 w-10 rounded-2xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 flex items-center justify-center shrink-0 shadow-2xs">
          <ShieldAlert className="h-5 w-5" />
        </div>
      );
    }
    if (item.accountId === 'system-support') {
      return (
        <div className="h-10 w-10 rounded-2xl bg-amber-100 dark:bg-amber-950/60 text-amber-600 dark:text-amber-400 flex items-center justify-center shrink-0 shadow-2xs">
          <AlertTriangle className="h-5 w-5" />
        </div>
      );
    }
    if (item.accountId === 'system-audit') {
      return (
        <div className="h-10 w-10 rounded-2xl bg-sky-100 dark:bg-sky-950/60 text-sky-600 dark:text-sky-400 flex items-center justify-center shrink-0 shadow-2xs">
          <ShieldCheck className="h-5 w-5" />
        </div>
      );
    }
    if (item.statusVariant === 'danger') {
      return (
        <div className="h-10 w-10 rounded-2xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 flex items-center justify-center shrink-0 shadow-2xs">
          <ShieldAlert className="h-5 w-5" />
        </div>
      );
    }
    if (item.statusVariant === 'success') {
      return (
        <div className="h-10 w-10 rounded-2xl bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0 shadow-2xs">
          <UserCheck className="h-5 w-5" />
        </div>
      );
    }
    switch (item.type) {
      case 'payment':
      case 'transfer':
        return (
          <div className="h-10 w-10 rounded-2xl bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0 shadow-2xs">
            <ArrowDownLeft className="h-5 w-5" />
          </div>
        );
      case 'budget':
        return (
          <div className="h-10 w-10 rounded-2xl bg-amber-100 dark:bg-amber-950/60 text-amber-600 dark:text-amber-400 flex items-center justify-center shrink-0 shadow-2xs">
            <AlertTriangle className="h-5 w-5" />
          </div>
        );
      case 'subscription':
        return (
          <div className="h-10 w-10 rounded-2xl bg-sky-100 dark:bg-sky-950/60 text-sky-600 dark:text-sky-400 flex items-center justify-center shrink-0 shadow-2xs">
            <Calendar className="h-5 w-5" />
          </div>
        );
      case 'group':
        return (
          <div className="h-10 w-10 rounded-2xl bg-teal-100 dark:bg-teal-950/60 text-teal-600 dark:text-teal-400 flex items-center justify-center shrink-0 shadow-2xs">
            <Users className="h-5 w-5" />
          </div>
        );
      case 'friend_request':
        return (
          <div className="h-10 w-10 rounded-2xl bg-indigo-100 dark:bg-indigo-950/60 text-indigo-600 dark:text-indigo-400 flex items-center justify-center shrink-0 shadow-2xs">
            <Users className="h-5 w-5" />
          </div>
        );
      case 'security':
        return (
          <div className="h-10 w-10 rounded-2xl bg-purple-100 dark:bg-purple-950/60 text-purple-600 dark:text-purple-400 flex items-center justify-center shrink-0 shadow-2xs">
            <ShieldCheck className="h-5 w-5" />
          </div>
        );
      default:
        return (
          <div className="h-10 w-10 rounded-2xl bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 flex items-center justify-center shrink-0 shadow-2xs">
            <ShieldAlert className="h-5 w-5" />
          </div>
        );
    }
  };

  const getAccountBadgeStyle = (type?: string) => {
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

  const getAccountIcon = (type: string) => {
    switch (type) {
      case 'all':
        return <Bell className="h-4 w-4" />;
      case 'spending':
        return <Wallet className="h-4 w-4" />;
      case 'savings':
        return <PiggyBank className="h-4 w-4" />;
      case 'emergency':
        return <ShieldAlert className="h-4 w-4" />;
      case 'security':
        return <ShieldCheck className="h-4 w-4" />;
      case 'group':
        return <Users className="h-4 w-4" />;
      default:
        return <CreditCard className="h-4 w-4" />;
    }
  };

  const selectedAccountDetails = accountList.find((a) => a.id === selectedAccountId);

  if (isLoading && notifications.length === 0) {
    return <NotificationsSkeleton />;
  }

  return (
    <div className="space-y-6 animate-in fade-in-50 duration-200 pb-12">
      {/* Toast Notification */}
      {toastMessage && (
        <div
          className={cn(
            'fixed top-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-2xl text-white shadow-xl text-xs font-semibold animate-in slide-in-from-top-3',
            toastType === 'error'
              ? 'bg-rose-900 border border-rose-500/40 text-rose-100'
              : 'bg-slate-900 dark:bg-slate-800 border border-emerald-500/30'
          )}
        >
          {toastType === 'error' ? (
            <AlertTriangle className="h-4 w-4 text-rose-400 shrink-0" />
          ) : (
            <CheckCircle2 className="h-4 w-4 text-emerald-400 shrink-0" />
          )}
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Preferences Modal */}
      <AccountPrefModal
        isOpen={isPrefModalOpen}
        onClose={() => setIsPrefModalOpen(false)}
        initialAccountId={prefModalAccountId}
        onSavedToast={(msg) => showToast(msg, 'success')}
      />

      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2.5">
            <h2 className="text-xl sm:text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
              {t('notifications.pageTitle')}
            </h2>
            {unreadCount > 0 && (
              <span className="px-2.5 py-0.5 rounded-full bg-emerald-500 text-white text-xs font-bold shadow-2xs">
                {t('notifications.unreadCount', { count: unreadCount })}
              </span>
            )}
            {isLoading && (
              <span className="text-xs text-slate-400 flex items-center gap-1">
                <span className="h-2 w-2 rounded-full bg-emerald-500 animate-ping" />
                {locale === 'vi-VN' ? 'Đang đồng bộ...' : 'Syncing...'}
              </span>
            )}
          </div>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            {t('notifications.pageSubtitle')}
          </p>
        </div>

        {/* Global Quick Action Buttons */}
        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => handleOpenAccountPrefs()}
            className="gap-1.5"
          >
            <SlidersHorizontal className="h-4 w-4 text-slate-500" />
            <span className="hidden sm:inline">{t('notifications.accountSettingsBtn')}</span>
            <span className="sm:hidden">{t('navigation.settings')}</span>
          </Button>

          <Button
            variant="outline"
            size="sm"
            onClick={handleMarkAllRead}
            disabled={
              isMutating ||
              (selectedAccountId === 'all'
                ? unreadCount === 0
                : (unreadByAccount[selectedAccountId] || 0) === 0)
            }
            className="gap-1.5"
          >
            <CheckCheck className="h-4 w-4 text-emerald-500" />
            <span>
              {selectedAccountId === 'all'
                ? t('notifications.markAllReadBtn')
                : t('notifications.markAccountReadBtn', {
                    account: getAccountDisplayName(selectedAccountDetails)?.split(' (')[0] || '',
                  })}
            </span>
          </Button>

        </div>
      </div>

      {/* 1. Account Filter Cards Carousel / Grid */}
      <div>
        <div className="flex items-center justify-between mb-2">
          <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
            {t('notifications.accountFilterTitle')}
          </span>
          <span className="text-[11px] text-slate-400">
            {accountList.length} {locale === 'vi-VN' ? 'tài khoản & phân loại' : 'accounts/sources'}
          </span>
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
          {accountList.map((acc) => {
            const isSelected = selectedAccountId === acc.id;
            const unread = acc.id === 'all' ? unreadCount : unreadByAccount[acc.id] || 0;
            return (
              <button
                key={acc.id}
                type="button"
                onClick={() => setSelectedAccountId(acc.id)}
                className={cn(
                  'flex flex-col text-left p-3.5 rounded-2xl border transition-all cursor-pointer relative overflow-hidden',
                  isSelected
                    ? 'bg-emerald-600 text-white border-emerald-600 shadow-md ring-2 ring-emerald-500/30'
                    : 'bg-white dark:bg-slate-900 border-slate-200 dark:border-slate-800 hover:border-emerald-500/60 text-slate-800 dark:text-slate-200 shadow-2xs'
                )}
              >
                {/* Top Row: Icon + Unread pill */}
                <div className="flex items-center justify-between mb-2">
                  <div
                    className={cn(
                      'h-7 w-7 rounded-lg flex items-center justify-center',
                      isSelected
                        ? 'bg-white/20 text-white'
                        : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                    )}
                  >
                    {getAccountIcon(acc.type)}
                  </div>
                  {unread > 0 && (
                    <span
                      className={cn(
                        'text-[10px] font-bold px-1.5 py-0.5 rounded-full',
                        isSelected
                          ? 'bg-white text-emerald-700'
                          : 'bg-emerald-500 text-white'
                      )}
                    >
                      {unread}
                    </span>
                  )}
                </div>

                {/* Account Name */}
                <span
                  className={cn(
                    'text-xs font-bold line-clamp-1 leading-snug',
                    isSelected ? 'text-white' : 'text-slate-900 dark:text-white'
                  )}
                >
                  {getAccountDisplayName(acc)}
                </span>

                {/* Subtitle / Balance */}
                <span
                  className={cn(
                    'text-[11px] mt-1',
                    isSelected ? 'text-emerald-100' : 'text-slate-500 dark:text-slate-400'
                  )}
                >
                  {acc.balance !== undefined
                    ? `${acc.balance.toLocaleString(locale)} ${locale === 'vi-VN' ? '₫' : 'VND'}`
                    : acc.id === 'all'
                    ? `${notifications.length} ${locale === 'vi-VN' ? 'thông báo' : 'items'}`
                    : acc.accountNumber
                    ? `**** ${acc.accountNumber}`
                    : `${acc.totalCount} ${locale === 'vi-VN' ? 'tin' : 'events'}`}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      {/* 2. Selected Account Detail Banner (if a single account is active) */}
      {selectedAccountId !== 'all' && selectedAccountDetails && (
        <div className="p-4 rounded-3xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4 animate-in fade-in-50">
          <div className="flex items-center gap-3.5">
            <div className="h-11 w-11 rounded-2xl bg-emerald-600 text-white flex items-center justify-center shadow-xs">
              {getAccountIcon(selectedAccountDetails.type)}
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  {getAccountDisplayName(selectedAccountDetails)}
                </h3>
                <span
                  className={cn(
                    'text-[10px] font-bold px-2 py-0.5 rounded-md border uppercase',
                    getAccountBadgeStyle(selectedAccountDetails.type)
                  )}
                >
                  {getAccountTypeLabel(selectedAccountDetails.type)}
                </span>
              </div>
              <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5 flex items-center gap-3">
                {selectedAccountDetails.accountNumber && (
                  <span>
                    {t('notifications.accountNumberLabel')}: <b>**** {selectedAccountDetails.accountNumber}</b>
                  </span>
                )}
                {selectedAccountDetails.balance !== undefined && (
                  <span>
                    {t('notifications.currentBalanceLabel')}:{' '}
                    <b className="text-emerald-600 dark:text-emerald-400">
                      {selectedAccountDetails.balance.toLocaleString(locale)} {locale === 'vi-VN' ? '₫' : 'VND'}
                    </b>
                  </span>
                )}
                <span>
                  {t('notifications.unreadCountLabel')}:{' '}
                  <b>{unreadByAccount[selectedAccountId] || 0}</b>
                </span>
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2 self-end md:self-center">
            <Button
              variant="outline"
              size="sm"
              onClick={() => handleOpenAccountPrefs(selectedAccountId)}
              className="gap-1 text-xs"
            >
              <SlidersHorizontal className="h-3.5 w-3.5" />
              <span>{t('notifications.prefModalTitle')}</span>
            </Button>

            <Button
              variant="ghost"
              size="sm"
              onClick={handleClearAccountNotifications}
              className="gap-1 text-xs text-rose-600 hover:text-rose-700 hover:bg-rose-50 dark:hover:bg-rose-950/40"
            >
              <Trash2 className="h-3.5 w-3.5" />
              <span>
                {t('notifications.clearAccountBtn', {
                  account: getAccountDisplayName(selectedAccountDetails).split(' (')[0],
                })}
              </span>
            </Button>
          </div>
        </div>
      )}

      {/* 3. Search & Sub-Category Filters */}
      <div className="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3">
        {/* Category Tabs */}
        <div className="flex items-center gap-1.5 p-1 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-2xs overflow-x-auto no-scrollbar">
          <button
            type="button"
            onClick={() => setSelectedCategory('all')}
            className={cn(
              'px-3 py-1.5 rounded-xl text-xs font-bold transition-all whitespace-nowrap cursor-pointer',
              selectedCategory === 'all'
                ? 'bg-slate-900 dark:bg-slate-800 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            {t('notifications.tabAll', { count: notifications.length })}
          </button>
          <button
            type="button"
            onClick={() => setSelectedCategory('unread')}
            className={cn(
              'px-3 py-1.5 rounded-xl text-xs font-bold transition-all whitespace-nowrap cursor-pointer flex items-center gap-1.5',
              selectedCategory === 'unread'
                ? 'bg-slate-900 dark:bg-slate-800 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            <span>{t('notifications.tabUnread', { count: unreadCount })}</span>
          </button>
          <button
            type="button"
            onClick={() => setSelectedCategory('transaction')}
            className={cn(
              'px-3 py-1.5 rounded-xl text-xs font-bold transition-all whitespace-nowrap cursor-pointer',
              selectedCategory === 'transaction'
                ? 'bg-slate-900 dark:bg-slate-800 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            {t('notifications.tabTransactions')}
          </button>
          <button
            type="button"
            onClick={() => setSelectedCategory('budget')}
            className={cn(
              'px-3 py-1.5 rounded-xl text-xs font-bold transition-all whitespace-nowrap cursor-pointer',
              selectedCategory === 'budget'
                ? 'bg-slate-900 dark:bg-slate-800 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            {t('notifications.tabBudget')}
          </button>
          <button
            type="button"
            onClick={() => setSelectedCategory('recurring')}
            className={cn(
              'px-3 py-1.5 rounded-xl text-xs font-bold transition-all whitespace-nowrap cursor-pointer',
              selectedCategory === 'recurring'
                ? 'bg-slate-900 dark:bg-slate-800 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            {t('notifications.tabRecurring')}
          </button>
          <button
            type="button"
            onClick={() => setSelectedCategory('group')}
            className={cn(
              'px-3 py-1.5 rounded-xl text-xs font-bold transition-all whitespace-nowrap cursor-pointer',
              selectedCategory === 'group'
                ? 'bg-slate-900 dark:bg-slate-800 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            {t('notifications.tabGroups')}
          </button>
        </div>

        {/* Search Bar */}
        <div className="relative w-full md:w-72">
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={t('notifications.searchPlaceholder')}
            className="w-full h-9 bg-white dark:bg-slate-900 rounded-xl pl-9 pr-8 text-xs border border-slate-200 dark:border-slate-800 text-slate-900 dark:text-white placeholder:text-slate-400 focus:ring-1 focus:ring-emerald-500 outline-none"
          />
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
          {searchQuery && (
            <button
              onClick={() => setSearchQuery('')}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
            >
              <XCircle className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
      </div>

      {/* 4. Notifications List */}
      <div className="bg-white dark:bg-slate-900 rounded-3xl border border-slate-200 dark:border-slate-800 shadow-sm divide-y divide-slate-100 dark:divide-slate-800/80 overflow-hidden">
        {filteredNotifications.map((notif) => {
          const isDanger = notif.statusVariant === 'danger' || notif.type === 'friend_rejected';
          const isSuccess = notif.statusVariant === 'success' || notif.type === 'friend_accepted';
          const friendReq = notif.friendRequestId ? friendService.getRequestById(notif.friendRequestId) : undefined;
          const localized = getLocalizedNotificationItem(notif, locale, t);

          return (
            <div
              key={notif.id}
              onClick={() => markAsRead(notif.id)}
              className={cn(
                'p-5 flex flex-col md:flex-row md:items-center justify-between gap-4 transition-colors cursor-pointer group',
                !notif.isRead
                  ? isDanger
                    ? 'bg-rose-50/30 dark:bg-rose-950/20 border-l-4 border-l-rose-500'
                    : isSuccess
                    ? 'bg-emerald-50/30 dark:bg-emerald-950/20 border-l-4 border-l-emerald-500'
                    : 'bg-emerald-50/20 dark:bg-emerald-950/15'
                  : 'hover:bg-slate-50/60 dark:hover:bg-slate-800/30'
              )}
            >
              {/* Left: Icon & Main Details */}
              <div className="flex items-start gap-4 flex-1 min-w-0">
                {getNotificationIcon(notif)}

                <div className="space-y-1.5 flex-1 min-w-0">
                  {/* Account Tag & Timestamp Header */}
                  <div className="flex flex-wrap items-center gap-2">
                    {notif.accountName && (
                      <span
                        className={cn(
                          'text-[10px] font-bold px-2 py-0.5 rounded-md border leading-none inline-flex items-center gap-1',
                          isDanger
                            ? 'bg-rose-50 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300 border-rose-200 dark:border-rose-800/40'
                            : isSuccess
                            ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800/40'
                            : getAccountBadgeStyle(notif.accountType)
                        )}
                      >
                        <Wallet className="h-2.5 w-2.5" />
                        <span>{getLocalizedAccountDisplayName(notif.accountId, notif.accountName, locale, notif.accountType)}</span>
                        {notif.accountNumber && (
                          <span className="opacity-75">• {notif.accountNumber}</span>
                        )}
                      </span>
                    )}
                    <span className="text-[11px] text-slate-400 font-medium">
                      {getLocalizedNotificationTime(notif.time, notif.timestamp, locale)}
                    </span>
                    {!notif.isRead && (
                      <span
                        className={cn(
                          'h-2 w-2 rounded-full shrink-0',
                          isDanger ? 'bg-rose-500' : 'bg-emerald-500'
                        )}
                      />
                    )}
                  </div>

                  {/* Title & Message */}
                  <h4
                    className={cn(
                      'text-sm',
                      isDanger
                        ? 'text-rose-700 dark:text-rose-400 font-bold'
                        : 'text-slate-900 dark:text-white',
                      !notif.isRead ? 'font-bold' : 'font-semibold'
                    )}
                  >
                    {localized.title}
                  </h4>
                  <p
                    className={cn(
                      'text-xs leading-relaxed',
                      isDanger
                        ? 'text-rose-600 dark:text-rose-300 font-medium'
                        : 'text-slate-600 dark:text-slate-300'
                    )}
                  >
                    {localized.message}
                  </p>

                  {/* Detail or Amount */}
                  {(notif.amountChange !== undefined || localized.detail) && (
                    <div className="flex flex-wrap items-center gap-3 pt-0.5">
                      {notif.amountChange !== undefined && (
                        <div className="flex items-center gap-2">
                          <span
                            className={cn(
                              'text-xs font-extrabold px-2 py-0.5 rounded-md',
                              notif.amountChange > 0
                                ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300'
                                : 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300'
                            )}
                          >
                            {notif.amountChange > 0 ? '+' : ''}
                            {notif.amountChange.toLocaleString(locale)} {locale === 'vi-VN' ? '₫' : 'VND'}
                          </span>
                          {notif.balanceAfter !== undefined && (
                            <span className="text-[11px] text-slate-500 dark:text-slate-400">
                              {t('notifications.balanceAfterLabel')}:{' '}
                              <b>{notif.balanceAfter.toLocaleString(locale)} {locale === 'vi-VN' ? '₫' : 'VND'}</b>
                            </span>
                          )}
                        </div>
                      )}
                      {localized.detail && !notif.amountChange && (
                        <p className="text-[11px] text-slate-500 dark:text-slate-400 italic">
                          {localized.detail}
                        </p>
                      )}
                    </div>
                  )}
                </div>
              </div>

              {/* Right: Actions */}
              <div className="flex items-center justify-between md:justify-end gap-2 shrink-0 pt-2 md:pt-0 border-t md:border-t-0 border-slate-100 dark:border-slate-800">
                {/* Friend Request Accept / Reject Direct Buttons */}
                {notif.type === 'friend_request' && (
                  <div className="flex items-center gap-2">
                    {friendReq && friendReq.status === 'pending' ? (
                      <>
                        <button
                          type="button"
                          disabled={busyFriendReqIds.has(friendReq.id)}
                          onClick={async (e) => {
                            e.stopPropagation();
                            setBusyFriendReqIds((prev) => new Set(prev).add(friendReq.id));
                            try {
                              const res = await friendService.acceptFriendRequestAsync(friendReq.id);
                              if (res.success) {
                                markAsRead(notif.id);
                                showToast(locale === 'vi-VN' ? 'Đã chấp nhận lời mời kết bạn' : 'Accepted friend request');
                              } else {
                                showToast(getLocalizedErrorMessage(res.error, locale, t));
                              }
                            } finally {
                              setBusyFriendReqIds((prev) => {
                                const next = new Set(prev);
                                next.delete(friendReq.id);
                                return next;
                              });
                            }
                          }}
                          className="px-3 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer shadow-2xs"
                        >
                          <Check className="h-3.5 w-3.5" />
                          <span>
                            {busyFriendReqIds.has(friendReq.id)
                              ? locale === 'vi-VN'
                                ? 'Đang xử lý...'
                                : 'Processing...'
                              : locale === 'vi-VN'
                              ? 'Chấp nhận'
                              : 'Accept'}
                          </span>
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
                                markAsRead(notif.id);
                                showToast(locale === 'vi-VN' ? 'Đã từ chối lời mời kết bạn' : 'Rejected friend request');
                              } else {
                                showToast(getLocalizedErrorMessage(res.error, locale, t));
                              }
                            } finally {
                              setBusyFriendReqIds((prev) => {
                                const next = new Set(prev);
                                next.delete(friendReq.id);
                                return next;
                              });
                            }
                          }}
                          className="px-3 py-1.5 rounded-xl text-rose-600 dark:text-rose-400 bg-rose-50 hover:bg-rose-100 dark:bg-rose-950/50 dark:hover:bg-rose-900/50 border border-rose-200 dark:border-rose-800/80 disabled:opacity-50 disabled:cursor-not-allowed text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer"
                        >
                          <XCircle className="h-3.5 w-3.5" />
                          <span>{locale === 'vi-VN' ? 'Từ chối' : 'Reject'}</span>
                        </button>
                      </>
                    ) : friendReq && friendReq.status === 'accepted' ? (
                      <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 text-xs font-semibold border border-emerald-200 dark:border-emerald-800/50">
                        <Check className="h-3.5 w-3.5" />
                        <span>{locale === 'vi-VN' ? 'Đã chấp nhận' : 'Accepted'}</span>
                      </span>
                    ) : friendReq && friendReq.status === 'rejected' ? (
                      <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-xl bg-rose-50 dark:bg-rose-950/60 text-rose-700 dark:text-rose-300 text-xs font-semibold border border-rose-200 dark:border-rose-800/50">
                        <XCircle className="h-3.5 w-3.5" />
                        <span>{locale === 'vi-VN' ? 'Đã từ chối' : 'Rejected'}</span>
                      </span>
                    ) : null}
                  </div>
                )}

                {notif.actionLabelKey && notif.type !== 'friend_request' && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleActionClick(notif);
                    }}
                    className="px-3.5 py-1.5 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-emerald-600 hover:text-white dark:hover:bg-emerald-600 dark:hover:text-white text-xs font-bold text-slate-700 dark:text-slate-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-2xs"
                  >
                    <span>{getLocalizedActionLabel(notif.actionLabelKey, t)}</span>
                    <ExternalLink className="h-3 w-3" />
                  </button>
                )}

                {/* Mark Read Toggle & Delete Buttons */}
                <div className="flex items-center gap-1 opacity-80 group-hover:opacity-100 transition-opacity">
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      markAsRead(notif.id);
                    }}
                    title={notif.isRead ? (locale === 'vi-VN' ? 'Đã đọc' : 'Read') : (locale === 'vi-VN' ? 'Đánh dấu đã đọc' : 'Mark as read')}
                    className={cn(
                      'h-8 w-8 rounded-lg flex items-center justify-center transition-colors',
                      notif.isRead
                        ? 'text-slate-300 dark:text-slate-600 hover:text-slate-500'
                        : isDanger
                        ? 'text-rose-600 bg-rose-50 dark:bg-rose-950/60 hover:bg-rose-100'
                        : 'text-emerald-600 bg-emerald-50 dark:bg-emerald-950/60 hover:bg-emerald-100'
                    )}
                  >
                    <Check className="h-4 w-4" />
                  </button>

                  <button
                    type="button"
                    onClick={(e) => handleDeleteItem(e, notif.id)}
                    title={locale === 'vi-VN' ? 'Xóa thông báo này' : 'Delete this notification'}
                    className="h-8 w-8 rounded-lg flex items-center justify-center text-slate-400 hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40 transition-colors"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            </div>
          );
        })}

        {/* Empty State */}
        {filteredNotifications.length === 0 && (
          <div className="p-16 text-center space-y-3">
            <div className="h-14 w-14 rounded-3xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center mx-auto text-slate-400">
              <Bell className="h-7 w-7 opacity-60" />
            </div>
            <div className="max-w-md mx-auto space-y-1">
              <h3 className="text-sm font-bold text-slate-800 dark:text-white">
                {selectedAccountId !== 'all'
                  ? (locale === 'vi-VN' ? 'Không có thông báo nào cho tài khoản này' : 'No notifications for this account')
                  : (locale === 'vi-VN' ? 'Không có thông báo nào' : 'No notifications')}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN'
                  ? 'Khi có biến động số dư hoặc hoạt động mới, thông báo sẽ xuất hiện tại đây.'
                  : 'Notifications will appear here when there is balance activity or new updates.'}
              </p>
            </div>
            {(selectedAccountId !== 'all' || selectedCategory !== 'all' || searchQuery) && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setSelectedAccountId('all');
                  setSelectedCategory('all');
                  setSearchQuery('');
                }}
                className="mt-2 text-xs"
              >
                {locale === 'vi-VN' ? 'Xóa bộ lọc' : 'Reset filters'}
              </Button>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
