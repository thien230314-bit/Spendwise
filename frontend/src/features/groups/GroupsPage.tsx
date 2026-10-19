import React, { useState, useMemo, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Users,
  Plus,
  Receipt,
  CheckCircle2,
  ChevronRight,
  Building,
  Palmtree,
  HeartHandshake,
  DollarSign,
  User,
  Coffee,
  ShoppingBag,
  Loader2,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useBalanceVisibility } from '../../providers/BalanceVisibilityProvider';
import { useGroup } from '../../providers/GroupProvider';
import { useAuth } from '../../providers/AuthProvider';
import { formatMoney } from '../../utils/money';
import { Button } from '../../components/ui/Button';
import { CreateGroupModal } from './modals/CreateGroupModal';
import { Group, GroupCategory } from '../../types/groups';
import { cn } from '../../utils/cn';
import { groupApiService, GroupInviteDto } from '../../services/groupApiService';
import { GroupsSkeleton } from '../../components/skeletons/GroupsSkeleton';

export const GroupsPage: React.FC = () => {
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();
  const { groups: contextGroups, createGroup, refreshGroups, isLoading: isGroupsLoading } = useGroup();
  const { currentUser } = useAuth();
  const currentUserId = currentUser?.id;
  const navigate = useNavigate();

  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [invitations, setInvitations] = useState<GroupInviteDto[]>([]);
  const [isLoadingInvites, setIsLoadingInvites] = useState(false);
  const [actioningInviteId, setActioningInviteId] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;
    if (currentUser?.id) {
      setIsLoadingInvites(true);
      groupApiService
        .getInvitations()
        .then((invs) => {
          if (!isMounted) return;
          const pending = (invs || []).filter(
            (inv) => !inv.status || inv.status.toLowerCase() === 'pending'
          );
          setInvitations(pending);
        })
        .catch((err) => {
          console.warn('Failed to load group invitations:', err);
        })
        .finally(() => {
          if (isMounted) setIsLoadingInvites(false);
        });
    }
    return () => {
      isMounted = false;
    };
  }, [currentUser?.id]);

  const handleAcceptInvite = async (invite: GroupInviteDto) => {
    setActioningInviteId(invite.id);
    try {
      await groupApiService.acceptInvite(invite.id, invite.token);
      setInvitations((prev) => prev.filter((i) => i.id !== invite.id));
      await refreshGroups();
      showToast(locale === 'vi-VN' ? `Đã tham gia nhóm ${invite.groupName}` : `Joined group ${invite.groupName}`);
    } catch {
      showToast(locale === 'vi-VN' ? 'Không thể chấp nhận lời mời' : 'Failed to accept invitation');
    } finally {
      setActioningInviteId(null);
    }
  };

  const handleRejectInvite = async (invite: GroupInviteDto) => {
    setActioningInviteId(invite.id);
    try {
      await groupApiService.rejectInvite(invite.id);
      setInvitations((prev) => prev.filter((i) => i.id !== invite.id));
      showToast(locale === 'vi-VN' ? `Đã từ chối lời mời vào ${invite.groupName}` : `Declined invitation to ${invite.groupName}`);
    } catch {
      showToast(locale === 'vi-VN' ? 'Không thể từ chối lời mời' : 'Failed to decline invitation');
    } finally {
      setActioningInviteId(null);
    }
  };

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3000);
  };

  // Helper for Category Icons & Styling
  const getCategoryLabel = (type: GroupCategory) => {
    switch (type) {
      case 'Roommates':
        return locale === 'vi-VN' ? 'Phòng trọ' : 'Roommates';
      case 'Trip':
        return locale === 'vi-VN' ? 'Du lịch' : 'Trip';
      case 'Family':
        return locale === 'vi-VN' ? 'Gia đình' : 'Family';
      case 'Couple':
        return locale === 'vi-VN' ? 'Cặp đôi' : 'Couple';
      case 'Friends':
        return locale === 'vi-VN' ? 'Nhóm bạn' : 'Friends';
      case 'Other':
      default:
        return locale === 'vi-VN' ? 'Khác' : 'Other';
    }
  };

  const getCategoryMeta = (type: GroupCategory) => {
    switch (type) {
      case 'Trip':
        return {
          icon: Palmtree,
          iconBg: 'bg-teal-100 dark:bg-teal-950/60',
          iconColor: 'text-teal-600 dark:text-teal-400',
        };
      case 'Roommates':
        return {
          icon: Building,
          iconBg: 'bg-indigo-100 dark:bg-indigo-950/60',
          iconColor: 'text-indigo-600 dark:text-indigo-400',
        };
      case 'Family':
        return {
          icon: HeartHandshake,
          iconBg: 'bg-rose-100 dark:bg-rose-950/60',
          iconColor: 'text-rose-600 dark:text-rose-400',
        };
      case 'Couple':
        return {
          icon: HeartHandshake,
          iconBg: 'bg-pink-100 dark:bg-pink-950/60',
          iconColor: 'text-pink-600 dark:text-pink-400',
        };
      case 'Friends':
        return {
          icon: Coffee,
          iconBg: 'bg-amber-100 dark:bg-amber-950/60',
          iconColor: 'text-amber-600 dark:text-amber-400',
        };
      default:
        return {
          icon: Users,
          iconBg: 'bg-slate-100 dark:bg-slate-800',
          iconColor: 'text-slate-600 dark:text-slate-300',
        };
    }
  };

  // Aggregated metrics from live contextGroups
  const { totalGroups, totalMembersCount, totalFundBalance, activeFundRoundsCount, userPendingObligations, recentExpenses } = useMemo(() => {
    let totalFund = 0;
    let activeFundRoundsCount = 0;
    let userPendingObligations = 0;
    const memberIdSet = new Set<string>();
    const allExpenses: {
      id: string;
      groupName: string;
      title: string;
      payer: string;
      amount: number;
      time: string;
    }[] = [];

    contextGroups.forEach((g) => {
      totalFund += g.sharedFund?.balance || 0;
      g.members.forEach((m) => {
        if (m.userId) memberIdSet.add(m.userId);
      });

      // Compute active fund rounds and user pending obligations
      (g.fundRounds || []).forEach((r) => {
        if (r.status === 'Active') {
          activeFundRoundsCount += 1;
          const mem = r.members.find((m) => m.userId === currentUserId);
          if (mem && mem.status !== 'Paid') {
            userPendingObligations += Math.max(0, mem.requiredAmount - mem.paidAmount);
          }
        }
      });

      // Collect expenses
      const myMember = g.members.find(
        (m) => currentUserId && m.userId === currentUserId
      );
      const myMemberUserId = myMember?.userId || currentUserId;

      g.expenses.forEach((e) => {
        const isPaidByMe =
          e.paidByType === 'user' &&
          Boolean(
            (myMemberUserId && e.paidByUserId === myMemberUserId) ||
            (currentUserId && e.paidByUserId === currentUserId)
          );

        allExpenses.push({
          id: e.id,
          groupName: g.name,
          title: e.title,
          payer: e.paidByType === 'fund' ? (locale === 'vi-VN' ? 'Quỹ chung' : 'Shared Fund') : (isPaidByMe ? (locale === 'vi-VN' ? 'Bạn' : 'You') : e.paidByUserName),
          amount: e.amount,
          time: e.date,
        });
      });
    });

    return {
      totalGroups: contextGroups.length,
      totalMembersCount: memberIdSet.size || contextGroups.reduce((acc, g) => acc + g.members.length, 0),
      totalFundBalance: totalFund,
      activeFundRoundsCount,
      userPendingObligations,
      recentExpenses: allExpenses.slice(0, 5),
    };
  }, [contextGroups, currentUserId, locale, t]);

  if (isGroupsLoading && contextGroups.length === 0) {
    return <GroupsSkeleton />;
  }

  return (
    <div className="space-y-6 animate-in fade-in-50 duration-200">
      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed top-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-2xl bg-slate-900 text-white shadow-xl border border-emerald-500/30 text-xs font-semibold animate-in slide-in-from-top-3">
          <CheckCircle2 className="h-4 w-4 text-emerald-400" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl sm:text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
            {t('groups.pageTitle')}
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            {t('groups.pageSubtitle')}
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button
            onClick={() => setIsCreateModalOpen(true)}
            className="gap-1.5"
          >
            <Plus className="h-4 w-4" />
            <span>{t('groups.createGroup')}</span>
          </Button>
        </div>
      </div>

      {/* Incoming Group Invitations Banner */}
      {invitations.length > 0 && (
        <div className="bg-indigo-50/70 dark:bg-indigo-950/40 border border-indigo-100 dark:border-indigo-900/60 rounded-3xl p-5 space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-bold text-indigo-950 dark:text-indigo-200 flex items-center gap-2">
              <Users className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
              <span>
                {locale === 'vi-VN' ? 'Lời mời tham gia nhóm' : 'Group Invitations'} ({invitations.length})
              </span>
            </h3>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            {invitations.map((inv) => (
              <div
                key={inv.id}
                className="bg-white dark:bg-slate-900 rounded-2xl p-4 border border-slate-100 dark:border-slate-800 flex items-center justify-between gap-3 shadow-xs"
              >
                <div className="min-w-0">
                  <p className="text-sm font-bold text-slate-900 dark:text-slate-100 truncate">
                    {inv.groupName}
                  </p>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Lời mời tham gia nhóm' : 'Group invitation'}
                  </p>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  <button
                    type="button"
                    disabled={actioningInviteId === inv.id}
                    onClick={() => handleRejectInvite(inv)}
                    className="px-3 py-1.5 rounded-xl border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-xs font-medium text-slate-700 dark:text-slate-300 transition cursor-pointer disabled:opacity-50"
                  >
                    {locale === 'vi-VN' ? 'Từ chối' : 'Reject'}
                  </button>
                  <button
                    type="button"
                    disabled={actioningInviteId === inv.id}
                    onClick={() => handleAcceptInvite(inv)}
                    className="px-3 py-1.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold shadow-xs transition cursor-pointer disabled:opacity-50 flex items-center gap-1.5"
                  >
                    {actioningInviteId === inv.id && (
                      <Loader2 className="w-3.5 h-3.5 animate-spin" />
                    )}
                    <span>{locale === 'vi-VN' ? 'Chấp nhận' : 'Accept'}</span>
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Group Summary Metrics Card */}
      <div className="bg-white dark:bg-[#0F172A] text-slate-900 dark:text-white rounded-3xl p-6 sm:p-8 border border-slate-100 dark:border-slate-800 shadow-sm">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 items-center">
          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              {t('groups.groupsJoined')}
            </span>
            <p className="text-2xl sm:text-3xl font-bold text-slate-900 dark:text-white mt-1">
              {t('groups.groupsCount', { count: totalGroups })}
            </p>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              {t('groups.connectedMembers', { count: totalMembersCount })}
            </p>
          </div>

          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              {t('groups.totalSharedFund')}
            </span>
            <p className="text-2xl sm:text-3xl font-bold text-slate-900 dark:text-white mt-1 tabular-nums">
              {isHidden ? '••••••••' : formatMoney(totalFundBalance, locale)}
            </p>
            <p className="text-xs text-emerald-600 dark:text-emerald-400 mt-0.5">{t('groups.coManaged')}</p>
          </div>

          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN' ? 'Đợt góp đang chạy' : 'Active Fund Rounds'}
            </span>
            <p className="text-2xl sm:text-3xl font-bold text-slate-900 dark:text-white mt-1 tabular-nums">
              {activeFundRoundsCount}
            </p>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              {locale === 'vi-VN' ? 'Đợt đóng quỹ' : 'Fund rounds'}
            </p>
          </div>

          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN' ? 'Phần bạn cần đóng' : 'Pending Contribution'}
            </span>
            <p className={cn(
              "text-2xl sm:text-3xl font-bold mt-1 tabular-nums",
              userPendingObligations > 0 ? "text-indigo-600 dark:text-indigo-400" : "text-emerald-600 dark:text-emerald-400"
            )}>
              {isHidden
                ? '••••••••'
                : userPendingObligations === 0
                ? (locale === 'vi-VN' ? 'Đã hoàn tất' : 'All Settled')
                : formatMoney(userPendingObligations, locale)}
            </p>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              {userPendingObligations === 0
                ? (locale === 'vi-VN' ? 'Đã hoàn thành mọi đợt góp' : 'All contributions completed')
                : (locale === 'vi-VN' ? 'Cần đóng trong các đợt góp' : 'Pending in active rounds')}
            </p>
          </div>
        </div>
      </div>

      {/* Groups Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {contextGroups.map((group) => {
          const { icon: CategoryIcon, iconBg, iconColor } = getCategoryMeta(group.type);
          
          // Calculate active fund round status in this specific group
          const activeRounds = (group.fundRounds || []).filter((r) => r.status === 'Active');
          const userActiveRounds = activeRounds.filter((r) =>
            r.members.some((m) => m.userId === currentUserId)
          );
          const userRemainingInGroup = userActiveRounds.reduce((acc, r) => {
            const mem = r.members.find((m) => m.userId === currentUserId);
            if (mem && mem.status !== 'Paid') {
              return acc + Math.max(0, mem.requiredAmount - mem.paidAmount);
            }
            return acc;
          }, 0);

          let roundStatusText = locale === 'vi-VN' ? 'Không có đợt góp' : 'No active rounds';
          let roundStatusClass = 'text-slate-500 dark:text-slate-400';
          if (userActiveRounds.length > 0 && userRemainingInGroup > 0) {
            roundStatusText = `${locale === 'vi-VN' ? 'Cần đóng: ' : 'Pending: '}${formatMoney(userRemainingInGroup, locale)}`;
            roundStatusClass = 'text-indigo-600 dark:text-indigo-400 font-bold';
          } else if (userActiveRounds.length > 0) {
            roundStatusText =
              locale === 'vi-VN'
                ? 'Phần của bạn đã hoàn thành'
                : 'Your share is complete';
            roundStatusClass = 'text-emerald-600 dark:text-emerald-400 font-medium';
          } else if (activeRounds.length > 0) {
            roundStatusText =
              locale === 'vi-VN'
                ? `${activeRounds.length} đợt đang mở`
                : `${activeRounds.length} active round${activeRounds.length > 1 ? 's' : ''}`;
            roundStatusClass = 'text-slate-600 dark:text-slate-300 font-medium';
          }

          const memberDisplayNames = group.members.map((m) => {
            const isMe = currentUserId ? m.userId === currentUserId : false;
            return isMe ? t('common.you') : m.name;
          });

          return (
            <div
              key={group.id}
              className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-sm flex flex-col justify-between space-y-5 hover:border-slate-300 dark:hover:border-slate-700 transition-colors"
            >
              <div>
                {/* Group Header */}
                <div className="flex items-start justify-between gap-3 mb-4">
                  <div className="flex items-center gap-3">
                    <div
                      className={`h-11 w-11 rounded-2xl ${iconBg} ${iconColor} flex items-center justify-center shrink-0 shadow-2xs`}
                    >
                      <CategoryIcon className="h-5 w-5" />
                    </div>
                    <div>
                      <h4 className="text-sm font-bold text-slate-900 dark:text-white leading-snug">
                        {group.name}
                      </h4>
                      <span className="text-[11px] text-slate-400">{getCategoryLabel(group.type)}</span>
                    </div>
                  </div>

                  <span className="px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 text-[10px] font-bold">
                    {t('groups.memberCount', { count: group.members.length })}
                  </span>
                </div>

                {/* Members Avatars */}
                <div className="flex items-center gap-1.5 mb-4">
                  <div className="flex -space-x-2">
                    {group.members.slice(0, 4).map((m, idx) => (
                      <div
                        key={idx}
                        className="h-7 w-7 rounded-full bg-slate-200 dark:bg-slate-700 border-2 border-white dark:border-slate-900 text-slate-700 dark:text-slate-200 text-[10px] font-bold flex items-center justify-center shadow-2xs"
                      >
                        {m.avatarInitials || m.name.slice(0, 1)}
                      </div>
                    ))}
                    {group.members.length > 4 && (
                      <div className="h-7 w-7 rounded-full bg-emerald-500 text-white border-2 border-white dark:border-slate-900 text-[10px] font-bold flex items-center justify-center shadow-2xs">
                        +{group.members.length - 4}
                      </div>
                    )}
                  </div>
                  <span className="text-[11px] text-slate-400 pl-2 truncate max-w-[180px]">
                    {memberDisplayNames.join(', ')}
                  </span>
                </div>

                {/* Funds & Fund Round Status */}
                <div className="space-y-2 text-xs bg-slate-50/70 dark:bg-slate-800/40 p-3.5 rounded-2xl border border-slate-100 dark:border-slate-800">
                  <div className="flex justify-between items-center">
                    <span className="text-slate-500">{t('groups.groupFundBalance')}:</span>
                    <span className="font-bold text-slate-900 dark:text-white tabular-nums">
                      {isHidden ? '••••••••' : formatMoney(group.sharedFund?.balance || 0, locale)}
                    </span>
                  </div>

                  <div className="flex justify-between items-center pt-1 border-t border-slate-200/60 dark:border-slate-700/60">
                    <span className="text-slate-500">{locale === 'vi-VN' ? 'Đợt góp quỹ:' : 'Fund Rounds:'}</span>
                    <span className={cn('text-right text-xs', roundStatusClass)}>
                      {roundStatusText}
                    </span>
                  </div>
                </div>
              </div>

              {/* Group Action Buttons */}
              <div className="pt-1">
                <Button
                  size="sm"
                  onClick={() => navigate(`/groups/${group.id}`)}
                  className="w-full gap-1.5 text-xs justify-center"
                >
                  <span>{t('groups.details')}</span>
                  <ChevronRight className="h-3.5 w-3.5" />
                </Button>
              </div>
            </div>
          );
        })}
      </div>

      {/* Recent Shared Expenses Activity Feed */}
      {recentExpenses.length > 0 && (
        <div className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-sm space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="text-base font-bold text-slate-800 dark:text-white flex items-center gap-2">
              <Receipt className="h-4 w-4 text-emerald-500" />
              <span>{t('groups.recentSharedExpenses')}</span>
            </h3>
            <span className="text-xs font-semibold text-slate-400">
              {t('groups.newEvents', { count: recentExpenses.length })}
            </span>
          </div>

          <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
            {recentExpenses.map((exp) => (
              <div key={exp.id} className="py-3.5 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="h-10 w-10 rounded-2xl bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 flex items-center justify-center font-bold text-xs shrink-0">
                    <DollarSign className="h-5 w-5 text-emerald-500" />
                  </div>
                  <div>
                    <div className="flex items-center gap-2">
                      <p className="text-xs sm:text-sm font-bold text-slate-800 dark:text-slate-200">
                        {exp.title}
                      </p>
                      <span className="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 font-medium">
                        {exp.groupName}
                      </span>
                    </div>
                    <p className="text-xs text-slate-400 mt-0.5">
                      {t('groups.paidBy')}: <strong className="text-slate-700 dark:text-slate-300">{exp.payer}</strong> • {exp.time}
                    </p>
                  </div>
                </div>

                <div className="text-right">
                  <span className="text-sm font-bold text-slate-900 dark:text-white tabular-nums">
                    {formatMoney(exp.amount, locale)}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Create Group Modal */}
      <CreateGroupModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        onSubmit={async (params) => {
          const res = await createGroup(params);
          if (res.success && res.group) {
            showToast(t('groups.toastGroupCreated', { name: params.name }));
            navigate(`/groups/${res.group.id}`);
          }
        }}
      />
    </div>
  );
};

