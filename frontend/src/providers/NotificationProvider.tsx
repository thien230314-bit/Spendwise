import React, { createContext, useContext, useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { NotificationItem, AccountNotificationPreference } from '../types';
import { useAuth } from './AuthProvider';
import { useFinancial } from './FinancialProvider';
import {
  notificationsApiService,
  NotificationDto,
  NotificationPreferenceDto,
} from '../services/notificationsApiService';

export interface AccountFilterOption {
  id: string;
  name: string;
  type: 'all' | 'spending' | 'savings' | 'emergency' | 'security' | 'system' | 'group' | 'bank';
  accountNumber?: string;
  unreadCount: number;
  totalCount: number;
  balance?: number;
}

interface NotificationContextType {
  notifications: NotificationItem[];
  filteredNotifications: NotificationItem[];
  unreadCount: number;
  unreadByAccount: Record<string, number>;
  selectedAccountId: string;
  setSelectedAccountId: (id: string) => void;
  selectedCategory: string;
  setSelectedCategory: (cat: string) => void;
  searchQuery: string;
  setSearchQuery: (query: string) => void;
  isLoading: boolean;
  error: string | null;
  refreshNotifications: () => Promise<void>;
  markAsRead: (id: string) => Promise<void>;
  markAllAsRead: (accountId?: string) => Promise<void>;
  deleteNotification: (id: string) => Promise<void>;
  clearAll: (accountId?: string) => Promise<void>;
  addNotification: (item: Partial<NotificationItem>) => void;
  accountPreferences: Record<string, AccountNotificationPreference>;
  updateAccountPreferences: (
    accountId: string,
    prefs: Partial<AccountNotificationPreference>
  ) => Promise<void>;
  accountList: AccountFilterOption[];
}

const NotificationContext = createContext<NotificationContextType | undefined>(undefined);

const DEFAULT_PREFERENCES: Record<string, AccountNotificationPreference> = {
  'all': {
    accountId: 'all',
    accountName: 'Tất cả tài khoản',
    notifyBalanceChanges: true,
    notifyBudgetAlerts: true,
    notifyLargeTransactions: true,
    largeTransactionThreshold: 1000000,
    notifyMinBalanceAlert: true,
    minBalanceThreshold: 500000,
  },
};

/**
 * Maps backend NotificationType and DataJson into the frontend NotificationItem shape.
 * Defensively extracts action metadata without fabricating invalid IDs or URLs.
 */
function mapDtoToNotificationItem(
  dto: NotificationDto,
  userId: string,
  wallets: Array<{ id: string; name: string; type?: any; accountNumber?: string }>
): NotificationItem {
  let parsedData: any = null;
  if (dto.dataJson) {
    try {
      parsedData = JSON.parse(dto.dataJson);
    } catch {
      parsedData = null;
    }
  }

  const typeStr = (dto.type || '').trim();
  let uiType: NotificationItem['type'] = 'system';
  let uiCategory: NotificationItem['category'] = 'system';
  let statusVariant: NotificationItem['statusVariant'] = 'default';

  switch (typeStr) {
    case 'MoneyReceived':
    case 'MoneySent':
    case 'BalanceChanged':
    case 'LargeTransaction':
    case 'BankDirectPaymentCompleted':
    case 'BankTransactionImported':
      uiType = 'payment';
      uiCategory = 'transaction';
      break;

    case 'BankDirectPaymentFailed':
      uiType = 'payment';
      uiCategory = 'transaction';
      statusVariant = 'danger';
      break;

    case 'MoneyRequest':
    case 'DebtReminder':
      uiType = 'transfer';
      uiCategory = 'transaction';
      break;

    case 'BudgetWarning':
      uiType = 'budget';
      uiCategory = 'budget';
      statusVariant = 'default';
      break;

    case 'BudgetExceeded':
      uiType = 'budget';
      uiCategory = 'budget';
      statusVariant = 'danger';
      break;

    case 'MinimumBalance':
      uiType = 'budget';
      uiCategory = 'budget';
      statusVariant = 'danger';
      break;

    case 'RecurringDue':
      uiType = 'recurring';
      uiCategory = 'recurring';
      break;

    case 'RecurringOverdue':
      uiType = 'recurring';
      uiCategory = 'recurring';
      statusVariant = 'danger';
      break;

    case 'GroupInvite':
    case 'ContributionRequest':
    case 'GroupFundRequest':
    case 'GroupFundRound':
    case 'GroupDeletionVote':
    case 'GroupDeletionReady':
    case 'GroupRefundAction':
    case 'GroupRefundCompleted':
      uiType = 'group';
      uiCategory = 'group';
      break;

    case 'GroupDeletionRejected':
      uiType = 'group';
      uiCategory = 'group';
      statusVariant = 'danger';
      break;

    case 'FriendRequest':
      uiType = 'friend_request';
      uiCategory = 'friend';
      break;

    case 'FriendAccepted':
      uiType = 'friend_accepted';
      uiCategory = 'friend';
      statusVariant = 'success';
      break;

    case 'FriendRejected':
      uiType = 'friend_rejected';
      uiCategory = 'friend';
      statusVariant = 'danger';
      break;

    case 'SecurityAlert':
      uiType = 'security';
      uiCategory = 'security';
      statusVariant = 'danger';
      break;

    case 'SupportUpdated':
    case 'DisputeUpdated':
    case 'SystemAnnouncement':
    case 'MaintenanceChanged':
    default:
      uiType = 'system';
      uiCategory = 'system';
      break;
  }

  // Determine accountId & accountName defensively
  let accountId: string | undefined = undefined;
  let accountName: string | undefined = undefined;
  let accountType: NotificationItem['accountType'] = undefined;
  let accountNumber: string | undefined = undefined;

  if (parsedData?.walletId) {
    const matchedWallet = wallets.find((w) => w.id === parsedData.walletId);
    if (matchedWallet) {
      accountId = matchedWallet.id;
      accountName = matchedWallet.name;
      accountType = matchedWallet.type;
      accountNumber = matchedWallet.accountNumber;
    } else {
      accountId = parsedData.walletId;
    }
  }

  if (!accountId) {
    if (uiCategory === 'group' || parsedData?.groupId) {
      accountId = parsedData?.groupId ? `group-${parsedData.groupId}` : 'group-all';
      accountName = parsedData?.groupName || 'Nhóm & Quỹ';
      accountType = 'system';
    } else if (uiCategory === 'security') {
      accountId = 'security';
      accountName = 'Bảo mật & Đăng nhập';
      accountType = 'security';
    } else if (uiCategory === 'friend') {
      accountId = 'friend';
      accountName = 'Bạn bè';
      accountType = 'friend';
    } else if (typeStr === 'SupportUpdated' || typeStr === 'DisputeUpdated') {
      accountId = 'system-support';
      accountName = 'Khiếu nại & Tra soát';
      accountType = 'system';
    } else {
      // If user has a default wallet, we don't force it unless it's a financial notification
      if (uiCategory === 'transaction' && wallets.length > 0) {
        const first = wallets[0];
        accountId = first.id;
        accountName = first.name;
        accountType = first.type;
        accountNumber = first.accountNumber;
      } else {
        accountId = 'system';
        accountName = 'Hệ thống SpendWise';
        accountType = 'system';
      }
    }
  }

  const createdDate = new Date(dto.createdAt);
  const timestamp = isNaN(createdDate.getTime()) ? Date.now() : createdDate.getTime();

  return {
    id: dto.id,
    userId,
    accountId,
    accountName,
    accountType,
    accountNumber,
    title: dto.title,
    message: dto.message,
    detail: parsedData?.detail,
    titleKey: parsedData?.titleKey,
    messageKey: parsedData?.messageKey,
    detailKey: parsedData?.detailKey,
    i18nParams: parsedData?.i18nParams,
    time: dto.createdAt,
    timestamp,
    isRead: Boolean(dto.isRead),
    type: uiType,
    category: uiCategory,
    amountChange: typeof parsedData?.amount === 'number' ? parsedData.amount : undefined,
    balanceAfter: typeof parsedData?.balanceAfter === 'number' ? parsedData.balanceAfter : undefined,
    actionRoute: parsedData?.actionRoute,
    actionLabelKey: parsedData?.actionLabelKey,
    relatedTransactionId: parsedData?.transactionId,
    friendRequestId: parsedData?.friendRequestId,
    friendRequesterId: parsedData?.friendRequesterId,
    statusVariant,
  };
}

export const NotificationProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { currentUser } = useAuth();
  const financial = useFinancial();

  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [accountPreferences, setAccountPreferences] = useState<
    Record<string, AccountNotificationPreference>
  >(DEFAULT_PREFERENCES);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const [selectedAccountId, setSelectedAccountId] = useState<string>('all');
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');

  // Track the user ID associated with the latest fetch request to discard race conditions
  const activeUserIdRef = useRef<string | null>(null);
  const latestFetchRequestId = useRef<number>(0);

  // ---------------------------------------------------------------------------
  // Backend Load: Notifications & Preferences
  // ---------------------------------------------------------------------------
  const loadAuthoritativeData = useCallback(
    async (userId: string, isSilent = false) => {
      if (currentUser?.role === 'SystemAdmin') return;
      const requestId = ++latestFetchRequestId.current;
      if (!isSilent) {
        setIsLoading(true);
        setError(null);
      }

      try {
        const [dtos, prefs] = await Promise.all([
          notificationsApiService.getNotifications(),
          notificationsApiService.getPreferences().catch(() => [] as NotificationPreferenceDto[]),
        ]);

        // Discard result if user switched or another request was triggered
        if (activeUserIdRef.current !== userId || requestId !== latestFetchRequestId.current) {
          return;
        }

        // Map items
        const mapped = dtos.map((dto) =>
          mapDtoToNotificationItem(dto, userId, financial.wallets)
        );
        // Sort descending by timestamp / createdAt
        mapped.sort((a, b) => b.timestamp - a.timestamp);
        setNotifications(mapped);

        // Map preferences
        const prefMap: Record<string, AccountNotificationPreference> = {
          ...DEFAULT_PREFERENCES,
        };
        prefs.forEach((p) => {
          const key = p.walletId || p.accountKey;
          prefMap[key] = {
            accountId: key,
            accountName: p.accountName,
            notifyBalanceChanges: p.notifyBalanceChanges,
            notifyBudgetAlerts: p.notifyBudgetAlerts,
            notifyLargeTransactions: p.notifyLargeTransactions,
            largeTransactionThreshold: p.largeTransactionThreshold,
            notifyMinBalanceAlert: p.notifyMinBalanceAlert,
            minBalanceThreshold: p.minBalanceThreshold,
          };
        });
        setAccountPreferences(prefMap);
      } catch (err: any) {
        if (activeUserIdRef.current === userId && requestId === latestFetchRequestId.current) {
          console.error('Failed to load notifications from backend:', err);
          setError(err?.message || 'Failed to load notifications from server');
        }
      } finally {
        if (activeUserIdRef.current === userId && requestId === latestFetchRequestId.current) {
          if (!isSilent) setIsLoading(false);
        }
      }
    },
    [financial.wallets]
  );

  // User session lifecycle: load on login, clear immediately on logout/switch
  useEffect(() => {
    const currentId = currentUser?.id || null;
    const isSystemAdmin = currentUser?.role === 'SystemAdmin';
    activeUserIdRef.current = currentId;

    if (!currentId || isSystemAdmin) {
      setNotifications([]);
      setAccountPreferences(DEFAULT_PREFERENCES);
      setIsLoading(false);
      setError(null);
      setSelectedAccountId('all');
      setSelectedCategory('all');
      setSearchQuery('');
      return;
    }

    loadAuthoritativeData(currentId, false);
  }, [currentUser?.id, currentUser?.role, loadAuthoritativeData]);

  // Window Focus & 30s Polling for cross-browser synchronization (User only)
  useEffect(() => {
    if (!currentUser?.id || currentUser.role === 'SystemAdmin') return;
    const userId = currentUser.id;

    const handleFocus = () => {
      loadAuthoritativeData(userId, true);
    };

    window.addEventListener('focus', handleFocus);
    const interval = setInterval(() => {
      loadAuthoritativeData(userId, true);
    }, 30000);

    return () => {
      window.removeEventListener('focus', handleFocus);
      clearInterval(interval);
    };
  }, [currentUser?.id, currentUser?.role, loadAuthoritativeData]);

  const refreshNotifications = useCallback(async () => {
    if (!currentUser?.id || currentUser.role === 'SystemAdmin') return;
    await loadAuthoritativeData(currentUser.id, false);
  }, [currentUser?.id, currentUser?.role, loadAuthoritativeData]);

  // ---------------------------------------------------------------------------
  // Mutations: Mark as read, Delete, Clear all, Preferences
  // ---------------------------------------------------------------------------

  const markAsRead = useCallback(async (id: string) => {
    // Optimistic update with revert on failure
    let previousState: NotificationItem[] = [];
    setNotifications((prev) => {
      previousState = prev;
      return prev.map((n) => (n.id === id ? { ...n, isRead: true } : n));
    });

    try {
      await notificationsApiService.markAsRead(id);
    } catch (err) {
      console.error('Failed to mark notification as read on backend:', err);
      // Revert on failure
      setNotifications(previousState);
      throw err;
    }
  }, []);

  const markAllAsRead = useCallback(async (_accountId?: string) => {
    // The backend provides POST /api/notifications/read-all
    let previousState: NotificationItem[] = [];
    setNotifications((prev) => {
      previousState = prev;
      return prev.map((n) => ({ ...n, isRead: true }));
    });

    try {
      await notificationsApiService.markAllAsRead();
    } catch (err) {
      console.error('Failed to mark all notifications as read on backend:', err);
      setNotifications(previousState);
      throw err;
    }
  }, []);

  const deleteNotification = useCallback(async (id: string) => {
    let previousState: NotificationItem[] = [];
    setNotifications((prev) => {
      previousState = prev;
      return prev.filter((n) => n.id !== id);
    });

    try {
      await notificationsApiService.deleteNotification(id);
    } catch (err) {
      console.error('Failed to delete notification on backend:', err);
      setNotifications(previousState);
      throw err;
    }
  }, []);

  const clearAll = useCallback(async (_accountId?: string) => {
    // Backend DELETE /api/notifications clears all user notifications
    let previousState: NotificationItem[] = [];
    setNotifications((prev) => {
      previousState = prev;
      return [];
    });

    try {
      await notificationsApiService.clearAllNotifications();
    } catch (err) {
      console.error('Failed to clear notifications on backend:', err);
      setNotifications(previousState);
      throw err;
    }
  }, []);

  /**
   * Safe programmatic addition for transient/custom events if needed.
   * Scoped strictly to current user.
   */
  const addNotification = useCallback((item: Partial<NotificationItem>) => {
    if (!currentUser?.id) return;
    if (item.userId && item.userId !== currentUser.id) return;

    // Do not fabricate an ID that looks like a backend GUID
    const newItem: NotificationItem = {
      id: item.id || `local_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`,
      userId: currentUser.id,
      accountId: item.accountId || 'system',
      accountName: item.accountName || 'Hệ thống',
      accountType: item.accountType || 'system',
      accountNumber: item.accountNumber,
      title: item.title || 'Thông báo',
      message: item.message || '',
      detail: item.detail,
      titleKey: item.titleKey,
      messageKey: item.messageKey,
      detailKey: item.detailKey,
      i18nParams: item.i18nParams,
      time: item.time || new Date().toISOString(),
      timestamp: item.timestamp || Date.now(),
      isRead: item.isRead ?? false,
      type: item.type || 'system',
      category: item.category || 'system',
      amountChange: item.amountChange,
      balanceAfter: item.balanceAfter,
      actionRoute: item.actionRoute,
      actionLabelKey: item.actionLabelKey,
      relatedTransactionId: item.relatedTransactionId,
      friendRequestId: item.friendRequestId,
      friendRequesterId: item.friendRequesterId,
      statusVariant: item.statusVariant || 'default',
    };

    setNotifications((prev) => {
      if (prev.some((n) => n.id === newItem.id)) return prev;
      return [newItem, ...prev];
    });
  }, [currentUser?.id]);

  /**
   * Update and persist notification preferences to backend PUT /api/notifications/preferences
   */
  const updateAccountPreferences = useCallback(
    async (targetId: string, prefs: Partial<AccountNotificationPreference>) => {
      if (!currentUser?.id) return;

      const existing = accountPreferences[targetId] || {
        accountId: targetId,
        accountName: targetId,
        notifyBalanceChanges: true,
        notifyBudgetAlerts: true,
        notifyLargeTransactions: true,
        largeTransactionThreshold: 1000000,
        notifyMinBalanceAlert: true,
        minBalanceThreshold: 500000,
      };

      const updatedPref: AccountNotificationPreference = {
        ...existing,
        ...prefs,
      };

      // Determine backend AccountKey & WalletId
      // Backend rule: WalletId is Guid? (must be actual Guid if present)
      // If targetId is a valid UUID, pass walletId and accountKey = walletId
      // Otherwise, backend requires key is ('all' | 'business') and walletId is null
      const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(targetId);
      const walletId = isGuid ? targetId : null;
      const accountKey = walletId ? targetId : targetId === 'all' ? 'all' : 'all';

      // Find proper account name
      const matchedWallet = financial.wallets.find((w) => w.id === targetId);
      const accountName = updatedPref.accountName || matchedWallet?.name || 'Tất cả tài khoản';

      const backendReq = {
        accountKey,
        accountName,
        walletId,
        notifyBalanceChanges: Boolean(updatedPref.notifyBalanceChanges),
        notifyBudgetAlerts: Boolean(updatedPref.notifyBudgetAlerts),
        notifyLargeTransactions: Boolean(updatedPref.notifyLargeTransactions),
        largeTransactionThreshold: Number(updatedPref.largeTransactionThreshold) || 0,
        notifyMinBalanceAlert: Boolean(updatedPref.notifyMinBalanceAlert),
        minBalanceThreshold: Number(updatedPref.minBalanceThreshold) || 0,
      };

      const res = await notificationsApiService.savePreference(backendReq);

      // Update state with confirmed backend data
      const confirmedKey = res.walletId || res.accountKey;
      setAccountPreferences((prev) => ({
        ...prev,
        [targetId]: {
          accountId: targetId,
          accountName: res.accountName,
          notifyBalanceChanges: res.notifyBalanceChanges,
          notifyBudgetAlerts: res.notifyBudgetAlerts,
          notifyLargeTransactions: res.notifyLargeTransactions,
          largeTransactionThreshold: res.largeTransactionThreshold,
          notifyMinBalanceAlert: res.notifyMinBalanceAlert,
          minBalanceThreshold: res.minBalanceThreshold,
        },
        [confirmedKey]: {
          accountId: confirmedKey,
          accountName: res.accountName,
          notifyBalanceChanges: res.notifyBalanceChanges,
          notifyBudgetAlerts: res.notifyBudgetAlerts,
          notifyLargeTransactions: res.notifyLargeTransactions,
          largeTransactionThreshold: res.largeTransactionThreshold,
          notifyMinBalanceAlert: res.notifyMinBalanceAlert,
          minBalanceThreshold: res.minBalanceThreshold,
        },
      }));
    },
    [accountPreferences, currentUser?.id, financial.wallets]
  );

  // ---------------------------------------------------------------------------
  // Computations & Filtered Views
  // ---------------------------------------------------------------------------

  const unreadCount = useMemo(() => {
    return notifications.filter((n) => !n.isRead).length;
  }, [notifications]);

  const unreadByAccount = useMemo(() => {
    const map: Record<string, number> = {};
    notifications.forEach((n) => {
      if (!n.isRead && n.accountId) {
        map[n.accountId] = (map[n.accountId] || 0) + 1;
      }
    });
    return map;
  }, [notifications]);

  const countByAccount = useMemo(() => {
    const map: Record<string, number> = {};
    notifications.forEach((n) => {
      if (n.accountId) {
        map[n.accountId] = (map[n.accountId] || 0) + 1;
      }
    });
    return map;
  }, [notifications]);

  const accountList = useMemo<AccountFilterOption[]>(() => {
    if (currentUser?.role === 'SystemAdmin') {
      return [
        {
          id: 'all',
          name: 'Tất cả thông báo hệ thống',
          type: 'all',
          unreadCount,
          totalCount: notifications.length,
        },
        {
          id: 'system-risk',
          name: 'Cảnh báo Rủi ro & Fraud',
          type: 'system',
          unreadCount: unreadByAccount['system-risk'] || 0,
          totalCount: countByAccount['system-risk'] || 0,
        },
        {
          id: 'system-audit',
          name: 'Đối soát & Kiểm toán',
          type: 'system',
          unreadCount: unreadByAccount['system-audit'] || 0,
          totalCount: countByAccount['system-audit'] || 0,
        },
        {
          id: 'system-support',
          name: 'Khiếu nại & Tra soát',
          type: 'system',
          unreadCount: unreadByAccount['system-support'] || 0,
          totalCount: countByAccount['system-support'] || 0,
        },
      ];
    }

    const baseList: AccountFilterOption[] = [
      {
        id: 'all',
        name: 'Tất cả tài khoản',
        type: 'all',
        unreadCount,
        totalCount: notifications.length,
      },
    ];

    // Add user's financial wallets from FinancialProvider
    financial.wallets.forEach((w) => {
      baseList.push({
        id: w.id,
        name: w.name,
        type: w.type,
        accountNumber: w.accountNumber,
        unreadCount: unreadByAccount[w.id] || 0,
        totalCount: countByAccount[w.id] || 0,
        balance: w.balance,
      });
    });

    // Group filter option if group notifications exist
    const groupNotifCount = notifications.filter(
      (n) => n.category === 'group' || n.type === 'group' || n.accountId?.startsWith('group-')
    ).length;
    const groupUnreadCount = notifications.filter(
      (n) => !n.isRead && (n.category === 'group' || n.type === 'group' || n.accountId?.startsWith('group-'))
    ).length;

    if (groupNotifCount > 0) {
      baseList.push({
        id: 'group-all',
        name: 'Nhóm & Quỹ chung',
        type: 'group',
        unreadCount: groupUnreadCount,
        totalCount: groupNotifCount,
      });
    }

    // Security filter option if security notifications exist
    const securityNotifCount = notifications.filter(
      (n) => n.category === 'security' || n.type === 'security' || n.accountId === 'security'
    ).length;
    const securityUnreadCount = notifications.filter(
      (n) => !n.isRead && (n.category === 'security' || n.type === 'security' || n.accountId === 'security')
    ).length;

    if (securityNotifCount > 0) {
      baseList.push({
        id: 'security',
        name: 'Bảo mật & Đăng nhập',
        type: 'security',
        unreadCount: securityUnreadCount,
        totalCount: securityNotifCount,
      });
    }

    return baseList;
  }, [currentUser?.role, financial.wallets, notifications, unreadCount, unreadByAccount, countByAccount]);

  const filteredNotifications = useMemo(() => {
    return notifications.filter((n) => {
      // 1. Account filter
      if (selectedAccountId === 'group-all') {
        const isGroupNotif =
          n.category === 'group' ||
          n.type === 'group' ||
          n.accountId === 'group-all' ||
          Boolean(n.accountId?.startsWith('group-'));
        if (!isGroupNotif) return false;
      } else if (selectedAccountId === 'security') {
        const isSecurityNotif =
          n.category === 'security' ||
          n.type === 'security' ||
          n.accountId === 'security';
        if (!isSecurityNotif) return false;
      } else if (selectedAccountId !== 'all' && n.accountId !== selectedAccountId) {
        return false;
      }

      // 2. Category / unread filter
      if (selectedCategory === 'unread') {
        if (n.isRead) return false;
      } else if (selectedCategory === 'transaction') {
        if (n.category !== 'transaction' && n.type !== 'payment' && n.type !== 'transfer') return false;
      } else if (selectedCategory === 'budget') {
        if (n.category !== 'budget' && n.type !== 'budget') return false;
      } else if (selectedCategory === 'recurring') {
        if (n.category !== 'recurring' && n.type !== 'subscription') return false;
      } else if (selectedCategory === 'group') {
        if (n.category !== 'group' && n.type !== 'group') return false;
      } else if (selectedCategory === 'security') {
        if (n.category !== 'security' && n.type !== 'security') return false;
      } else if (selectedCategory === 'system') {
        if (n.category !== 'system' && n.type !== 'system') return false;
      }

      // 3. Search query
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const matchTitle = n.title?.toLowerCase().includes(q);
        const matchMessage = n.message?.toLowerCase().includes(q);
        const matchDetail = n.detail?.toLowerCase().includes(q);
        const matchAccount = n.accountName?.toLowerCase().includes(q);
        if (!matchTitle && !matchMessage && !matchDetail && !matchAccount) {
          return false;
        }
      }

      return true;
    });
  }, [notifications, selectedAccountId, selectedCategory, searchQuery]);

  const value = useMemo(
    () => ({
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
      addNotification,
      accountPreferences,
      updateAccountPreferences,
      accountList,
    }),
    [
      notifications,
      filteredNotifications,
      unreadCount,
      unreadByAccount,
      selectedAccountId,
      selectedCategory,
      searchQuery,
      isLoading,
      error,
      refreshNotifications,
      markAsRead,
      markAllAsRead,
      deleteNotification,
      clearAll,
      addNotification,
      accountPreferences,
      updateAccountPreferences,
      accountList,
    ]
  );

  return <NotificationContext.Provider value={value}>{children}</NotificationContext.Provider>;
};

export const useNotifications = (): NotificationContextType => {
  const context = useContext(NotificationContext);
  if (!context) {
    throw new Error('useNotifications must be used within a NotificationProvider');
  }
  return context;
};
