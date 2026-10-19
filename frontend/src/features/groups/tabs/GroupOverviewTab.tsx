import React, { useMemo } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { Group } from '../../../types/groups';
import {
  Wallet,
  Receipt,
  AlertCircle,
  Clock,
  ArrowRight,
  ShieldCheck,
  Plus,
  Sparkles,
  Users,
  CheckCircle2,
  ChevronRight,
} from 'lucide-react';
import { cn } from '../../../utils/cn';
import { getLocalizedGroupActivity } from '../../../utils/localizationHelpers';

interface GroupOverviewTabProps {
  group: Group;
  onOpenContributeFund: () => void;
  onOpenAddExpense: () => void;
  onOpenCreateFundRound?: () => void;
  onSwitchTab: (tab: 'fund' | 'expenses' | 'members') => void;
}

export const GroupOverviewTab: React.FC<GroupOverviewTabProps> = ({
  group,
  onOpenContributeFund,
  onOpenAddExpense,
  onOpenCreateFundRound,
  onSwitchTab,
}) => {
  const { t, formatCurrency, locale } = useI18n();
  const { isBalanceHidden, maskValue } = useBalanceVisibility();
  const { currentUser } = useAuth();
  const currentUserId = currentUser?.id;
  const isOwner = group.currentUserRole === 'Owner' || group.ownerId === currentUserId;

  const currentMember = group.members.find(
    (m) => currentUserId && m.userId === currentUserId
  );

  // Metrics (Calculate current calendar month/year only)
  const totalSharedExpenseThisMonth = useMemo(() => {
    const now = new Date();
    const currentMonth = now.getMonth();
    const currentYear = now.getFullYear();
    return group.expenses.reduce((sum, e) => {
      if (!e.date) return sum;
      const d = new Date(e.date);
      if (isNaN(d.getTime())) return sum;
      if (d.getMonth() === currentMonth && d.getFullYear() === currentYear) {
        return sum + e.amount;
      }
      return sum;
    }, 0);
  }, [group.expenses]);
  const myContribution = currentMember?.totalContributed || 0;

  // Active Fund Rounds and user's pending share
  const fundRounds = group.fundRounds || [];
  const activeRounds = fundRounds.filter((r) => r.status === 'Active');

  const myPendingRounds = useMemo(() => {
    if (!currentUserId) return [];
    return activeRounds
      .map((r) => {
        const m = r.members.find((mem) => mem.userId === currentUserId);
        const remaining = m ? Math.max(0, m.requiredAmount - m.paidAmount) : 0;
        return { round: r, member: m, remaining };
      })
      .filter((item) => item.member && item.remaining > 0);
  }, [activeRounds, currentUserId]);

  const totalMyPendingRoundObligation = useMemo(() => {
    return myPendingRounds.reduce((sum, item) => sum + item.remaining, 0);
  }, [myPendingRounds]);

  // Attention items based on Fund Rounds and Fund Balance (NO Debt terminology)
  const attentionItems: {
    id: string;
    text: string;
    type: 'warning' | 'info' | 'success';
    actionText?: string;
    onAction?: () => void;
  }[] = [];

  myPendingRounds.forEach(({ round, remaining }) => {
    attentionItems.push({
      id: `attn-round-${round.id}`,
      text:
        locale === 'vi-VN'
          ? `Bạn còn phải đóng ${formatCurrency(remaining)} cho ${round.title}.`
          : `You have ${formatCurrency(remaining)} remaining to contribute for ${round.title}.`,
      type: 'warning',
      actionText: locale === 'vi-VN' ? 'Đóng quỹ ngay' : 'Pay share',
      onAction: () => onSwitchTab('fund'),
    });
  });

  if (group.sharedFund.balance > 0) {
    attentionItems.push({
      id: 'fund-ok',
      text:
        locale === 'vi-VN'
          ? `Quỹ chung hiện có ${formatCurrency(group.sharedFund.balance)} sẵn sàng cho các khoản chi sắp tới.`
          : `Shared fund balance has ${formatCurrency(group.sharedFund.balance)} ready for upcoming expenses.`,
      type: 'success',
      actionText: locale === 'vi-VN' ? 'Chi từ quỹ' : 'Spend from fund',
      onAction: onOpenAddExpense,
    });
  } else if (isOwner && activeRounds.length === 0 && onOpenCreateFundRound) {
    attentionItems.push({
      id: 'owner-suggest-round',
      text:
        locale === 'vi-VN'
          ? 'Quỹ nhóm hiện chưa có đợt đóng góp nào. Bạn có thể tạo đợt góp quỹ để chuẩn bị chi phí chung.'
          : 'No active fund rounds. You can create a Fund Round to prepare for shared expenses.',
      type: 'info',
      actionText: locale === 'vi-VN' ? 'Tạo đợt quỹ' : 'Create round',
      onAction: onOpenCreateFundRound,
    });
  }

  return (
    <div className="space-y-6 animate-fadeIn">
      {/* 1. FUND STATUS FIRST HERO PANEL */}
      <div className="rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/90 dark:border-slate-800 shadow-sm p-6 sm:p-7 space-y-6">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
          {/* Left: Dominant Fund Balance & Financial Context (7 cols) */}
          <div className="lg:col-span-7 flex flex-col justify-center space-y-4">
            <div className="space-y-3">
              <div className="flex items-center gap-2.5 flex-wrap">
                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-xl bg-emerald-50 dark:bg-emerald-950/70 border border-emerald-500/20 text-emerald-700 dark:text-emerald-300 text-xs font-bold">
                  <Wallet className="h-3.5 w-3.5" />
                  {locale === 'vi-VN' ? 'Quỹ chung của nhóm' : 'Group Shared Fund'}
                </span>
              </div>

              <div>
                <span className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 block">
                  {locale === 'vi-VN' ? 'Số dư khả dụng hiện tại' : 'Available Balance'}
                </span>
                <div className="flex items-baseline gap-2 mt-1">
                  <span className="text-3xl sm:text-4xl lg:text-5xl font-black text-slate-900 dark:text-white tracking-tight tabular-nums font-mono">
                    {isBalanceHidden ? maskValue(group.sharedFund.balance) : formatCurrency(group.sharedFund.balance)}
                  </span>
                </div>
              </div>

              {/* Compact financial context metrics */}
              <div className="grid grid-cols-3 gap-3 pt-3 border-t border-slate-100 dark:border-slate-800 text-xs">
                <div className="space-y-0.5">
                  <span className="text-[11px] text-slate-500 dark:text-slate-400 block font-medium">
                    {locale === 'vi-VN' ? 'Chi tiêu tháng này' : 'This Month Spent'}
                  </span>
                  <span className="text-sm font-bold text-slate-800 dark:text-slate-200 tabular-nums font-mono">
                    {isBalanceHidden ? maskValue(totalSharedExpenseThisMonth) : formatCurrency(totalSharedExpenseThisMonth)}
                  </span>
                </div>
                <div className="space-y-0.5 border-l border-slate-200/60 dark:border-slate-800 pl-3">
                  <span className="text-[11px] text-slate-500 dark:text-slate-400 block font-medium">
                    {locale === 'vi-VN' ? 'Bạn đã đóng góp' : 'You Contributed'}
                  </span>
                  <span className="text-sm font-bold text-emerald-600 dark:text-emerald-400 tabular-nums font-mono">
                    {isBalanceHidden ? maskValue(myContribution) : formatCurrency(myContribution)}
                  </span>
                </div>
                <div className="space-y-0.5 border-l border-slate-200/60 dark:border-slate-800 pl-3">
                  <span className="text-[11px] text-slate-500 dark:text-slate-400 block font-medium">
                    {locale === 'vi-VN' ? 'Cần đóng thêm' : 'Remaining Share'}
                  </span>
                  <span className={cn(
                    "text-sm font-bold tabular-nums font-mono",
                    totalMyPendingRoundObligation > 0
                      ? "text-amber-600 dark:text-amber-400"
                      : "text-slate-600 dark:text-slate-400"
                  )}>
                    {isBalanceHidden
                      ? maskValue(totalMyPendingRoundObligation)
                      : totalMyPendingRoundObligation > 0
                      ? formatCurrency(totalMyPendingRoundObligation)
                      : (locale === 'vi-VN' ? 'Đã hoàn tất' : 'All Settled')}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* Right: Fund Status Card & Context (5 cols) */}
          <div className="lg:col-span-5 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200/70 dark:border-slate-700/60 p-5 flex flex-col justify-between space-y-4">
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400">
                  {locale === 'vi-VN' ? 'Thông tin quỹ' : 'Fund Information'}
                </span>
              </div>

              <p className="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
                {locale === 'vi-VN'
                  ? `Quỹ chung hiện có ${isBalanceHidden ? maskValue(group.sharedFund.balance) : formatCurrency(group.sharedFund.balance)} sẵn sàng cho các khoản chi chung của nhóm.`
                  : `Shared fund balance has ${isBalanceHidden ? maskValue(group.sharedFund.balance) : formatCurrency(group.sharedFund.balance)} ready for upcoming shared group expenses.`}
              </p>

              <div className="space-y-2 pt-2 border-t border-slate-200/60 dark:border-slate-700/50">
                <div className="flex items-center justify-between text-xs py-1">
                  <span className="text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Đợt góp đang chạy' : 'Active Fund Rounds'}
                  </span>
                  <span className="font-bold text-slate-800 dark:text-slate-200">
                    {activeRounds.length} {locale === 'vi-VN' ? 'đợt' : 'rounds'}
                  </span>
                </div>
                <div className="flex items-center justify-between text-xs py-1 border-t border-slate-200/40 dark:border-slate-700/30">
                  <span className="text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Nghĩa vụ cá nhân' : 'Your Obligation'}
                  </span>
                  <span className={cn(
                    "font-bold flex items-center gap-1",
                    totalMyPendingRoundObligation > 0
                      ? "text-amber-600 dark:text-amber-400"
                      : "text-emerald-600 dark:text-emerald-400"
                  )}>
                    {totalMyPendingRoundObligation > 0 ? (
                      formatCurrency(totalMyPendingRoundObligation)
                    ) : (
                      <>
                        <CheckCircle2 className="h-3.5 w-3.5" />
                        <span>{locale === 'vi-VN' ? 'Đã hoàn tất' : 'Completed'}</span>
                      </>
                    )}
                  </span>
                </div>
              </div>
            </div>

            <div className="pt-2">
              <button
                onClick={() => onSwitchTab('fund')}
                className="w-full py-2 px-3 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 hover:border-emerald-500/50 text-slate-700 dark:text-slate-300 text-xs font-semibold flex items-center justify-between transition-colors cursor-pointer"
              >
                <span>{locale === 'vi-VN' ? 'Xem chi tiết sổ quỹ & lịch sử' : 'View Fund Ledger & History'}</span>
                <ChevronRight className="h-3.5 w-3.5 text-slate-400" />
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* 2. RECENT ACTIVITY & INTEGRATED MEMBER ROSTER SPLIT */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left: Recent Activity Feed (7 cols) */}
        <div className="lg:col-span-7 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/90 dark:border-slate-800 shadow-sm p-6 space-y-4">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
            <div className="flex items-center gap-2.5">
              <div className="p-2 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                <Clock className="h-4 w-4" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  {locale === 'vi-VN' ? 'Hoạt động quỹ & chi tiêu gần đây' : 'Recent Group Activity'}
                </h3>
                <span className="text-[11px] text-slate-400">
                  {locale === 'vi-VN' ? 'Minh bạch dòng tiền trong nhóm' : 'Transparent group cashflow'}
                </span>
              </div>
            </div>
            <button
              onClick={() => onSwitchTab('expenses')}
              className="text-xs font-semibold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
            >
              {locale === 'vi-VN' ? 'Tất cả chi phí →' : 'All Expenses →'}
            </button>
          </div>

          {group.activities.length === 0 ? (
            /* Empty state with clear context */
            <div className="py-8 text-center space-y-3">
              <div className="h-12 w-12 rounded-2xl bg-slate-100 dark:bg-slate-800/80 text-slate-400 dark:text-slate-500 flex items-center justify-center mx-auto">
                <Receipt className="h-6 w-6" />
              </div>
              <div className="space-y-1 max-w-sm mx-auto">
                <p className="text-xs font-bold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN'
                    ? 'Chưa có giao dịch chi tiêu hoặc đóng góp nào'
                    : 'No expenses or contributions recorded yet'}
                </p>
                <p className="text-[11px] text-slate-400 leading-relaxed">
                  {locale === 'vi-VN'
                    ? 'Mọi khoản thu quỹ và hóa đơn chi tiêu chung sẽ được ghi nhận và hiển thị tự động tại đây.'
                    : 'All fund contributions and shared expense invoices will be logged automatically here.'}
                </p>
              </div>
              <button
                type="button"
                onClick={onOpenAddExpense}
                className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 text-xs font-semibold text-slate-700 dark:text-slate-300 transition-colors cursor-pointer"
              >
                <span>+ {locale === 'vi-VN' ? 'Ghi nhận chi tiêu đầu tiên' : 'Record First Expense'}</span>
              </button>
            </div>
          ) : (
            <div className="space-y-3">
              {group.activities.slice(0, 8).map((act) => {
                const locAct = getLocalizedGroupActivity(act, locale, t);
                return (
                  <div
                    key={act.id}
                    className="flex items-start justify-between gap-3 p-3 rounded-xl bg-slate-50/70 dark:bg-slate-950/50 border border-slate-100 dark:border-slate-800 text-xs"
                  >
                    <div className="space-y-0.5">
                      <p className="font-semibold text-slate-900 dark:text-slate-200">{locAct.title}</p>
                      <p className="text-slate-500 dark:text-slate-400">{locAct.description}</p>
                    </div>
                    <span className="text-[11px] font-mono text-slate-400 shrink-0">
                      {act.timestamp}
                    </span>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Right: Integrated Member Roster (5 cols) */}
        <div className="lg:col-span-5 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/90 dark:border-slate-800 shadow-sm p-6 space-y-4">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
            <div className="flex items-center gap-2">
              <div className="p-2 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                <Users className="h-4 w-4" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900 dark:text-white flex items-center gap-2">
                  {locale === 'vi-VN' ? 'Thành viên nhóm' : 'Group Members'}
                  <span className="px-2 py-0.2 rounded-full text-[10px] font-bold bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400">
                    {group.members.length}
                  </span>
                </h3>
                <span className="text-[11px] text-slate-400">
                  {locale === 'vi-VN' ? 'Đồng quản lý chi phí' : 'Co-managing shared costs'}
                </span>
              </div>
            </div>
            <button
              onClick={() => onSwitchTab('members')}
              className="text-xs font-semibold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
            >
              {locale === 'vi-VN' ? 'Quản lý →' : 'Manage →'}
            </button>
          </div>

          {/* Members List from group.members */}
          <div className="space-y-2.5">
            {group.members.map((m) => {
              const isMe = m.isCurrentUser || (currentUserId && m.userId === currentUserId);
              const isMemberOwner = m.groupRole === 'Owner';

              return (
                <div
                  key={m.id}
                  className={cn(
                    "p-2.5 rounded-2xl border flex items-center justify-between transition-colors",
                    isMe
                      ? "bg-emerald-50/50 dark:bg-emerald-950/20 border-emerald-500/20"
                      : "bg-slate-50 dark:bg-slate-800/40 border-slate-100 dark:border-slate-800"
                  )}
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <div className={cn(
                      "h-8 w-8 rounded-full font-bold text-xs flex items-center justify-center shrink-0 shadow-xs",
                      isMe
                        ? "bg-emerald-600 text-white"
                        : "bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-200"
                    )}>
                      {m.avatarInitials || m.name.charAt(0).toUpperCase()}
                    </div>
                    <div className="min-w-0">
                      <div className="flex items-center gap-1.5">
                        <p className="text-xs font-bold text-slate-900 dark:text-white truncate">
                          {m.name}
                        </p>
                        {isMe && (
                          <span className="text-[9px] font-bold px-1.5 py-0.2 rounded-full bg-emerald-100 dark:bg-emerald-900/60 text-emerald-700 dark:text-emerald-300">
                            {locale === 'vi-VN' ? 'Bạn' : 'You'}
                          </span>
                        )}
                      </div>
                      <p className="text-[10px] text-slate-500 dark:text-slate-400 truncate">
                        {m.email || (locale === 'vi-VN' ? 'Thành viên nhóm' : 'Group member')}
                      </p>
                    </div>
                  </div>

                  <span className={cn(
                    "px-2 py-0.5 rounded-full text-[10px] font-bold shrink-0",
                    isMemberOwner
                      ? "bg-purple-100 text-purple-700 dark:bg-purple-950 dark:text-purple-300"
                      : "bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400"
                  )}>
                    {isMemberOwner
                      ? (locale === 'vi-VN' ? 'Chủ nhóm' : 'Owner')
                      : (locale === 'vi-VN' ? 'Thành viên' : 'Member')}
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
};
