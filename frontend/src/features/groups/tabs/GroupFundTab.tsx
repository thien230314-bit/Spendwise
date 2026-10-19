import React, { useState, useMemo } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { Group, GroupFundRound, FundTransaction, FundContributionRequest } from '../../../types/groups';
import {
  Wallet,
  Plus,
  Sparkles,
  History,
  CheckCircle2,
  Clock,
  ArrowDownLeft,
  ArrowUpRight,
  ShieldCheck,
  Calendar,
  AlertCircle,
  Users,
  ChevronDown,
  ChevronUp,
  Send,
} from 'lucide-react';
import { cn } from '../../../utils/cn';
import { getLocalDateString } from '../../../utils/inputValidation';

interface GroupFundTabProps {
  group: Group;
  onOpenContribute: () => void;
  onOpenCreateRound?: () => void;
  onPayRound?: (round: GroupFundRound) => void;
  onOpenRequest?: () => void;
  onPayRequest?: (request: FundContributionRequest) => void;
  onOpenSettings?: () => void;
}

export const GroupFundTab: React.FC<GroupFundTabProps> = ({
  group,
  onOpenContribute,
  onOpenCreateRound,
  onPayRound,
  onOpenRequest,
  onPayRequest,
  onOpenSettings,
}) => {
  const { t, formatCurrency, locale } = useI18n();
  const { isBalanceHidden, maskValue } = useBalanceVisibility();
  const { currentUser } = useAuth();
  const currentUserId = currentUser?.id;
  const isOwner = group.currentUserRole === 'Owner' || group.ownerId === currentUserId;
  const isFrozen = group.deletionRequest?.status === 'Pending' || group.deletionRequest?.status === 'Ready';

  const [activeSubTab, setActiveSubTab] = useState<'rounds' | 'requests' | 'history'>('rounds');
  const [expandedBreakdownRoundIds, setExpandedBreakdownRoundIds] = useState<Set<string>>(new Set());
  const [expandedBreakdownRequestIds, setExpandedBreakdownRequestIds] = useState<Set<string>>(new Set());

  const toggleBreakdown = (roundId: string) => {
    setExpandedBreakdownRoundIds((prev) => {
      const next = new Set(prev);
      if (next.has(roundId)) {
        next.delete(roundId);
      } else {
        next.add(roundId);
      }
      return next;
    });
  };

  const toggleRequestBreakdown = (reqId: string) => {
    setExpandedBreakdownRequestIds((prev) => {
      const next = new Set(prev);
      if (next.has(reqId)) {
        next.delete(reqId);
      } else {
        next.add(reqId);
      }
      return next;
    });
  };

  const today = getLocalDateString();
  const isOverdue = (dueDate: string) => Boolean(dueDate && dueDate < today);

  const fundRounds = useMemo(() => {
    return group.fundRounds || [];
  }, [group.fundRounds]);

  const allFundRequests = useMemo(() => {
    return group.fundRequests || [];
  }, [group.fundRequests]);

  const myFundRequests = useMemo(() => {
    if (!currentUserId) return [];
    return allFundRequests.filter((req) =>
      req.recipients.some((r) => r.userId === currentUserId)
    );
  }, [allFundRequests, currentUserId]);

  const visibleRequestsCount = isOwner ? allFundRequests.length : myFundRequests.length;

  const myPendingFundRequestsTotal = useMemo(() => {
    if (!currentUserId || isOwner) return 0;
    return myFundRequests.reduce((sum, req) => {
      const rec = req.recipients.find((r) => r.userId === currentUserId);
      if (rec && rec.status === 'Pending' && !isOverdue(req.dueDate)) {
        return sum + req.amountPerMember;
      }
      return sum;
    }, 0);
  }, [myFundRequests, currentUserId, isOwner, today]);

  const activeRounds = useMemo(() => {
    return fundRounds.filter((r) => r.status === 'Active');
  }, [fundRounds]);

  const completedRounds = useMemo(() => {
    return fundRounds.filter((r) => r.status === 'Completed');
  }, [fundRounds]);

  // Current user's pending obligation across all active rounds
  const myPendingTotal = useMemo(() => {
    if (!currentUserId) return 0;
    let sum = 0;
    activeRounds.forEach((r) => {
      const m = r.members.find((mem) => mem.userId === currentUserId);
      if (m && m.status !== 'Paid') {
        sum += Math.max(0, m.requiredAmount - m.paidAmount);
      }
    });
    return sum;
  }, [activeRounds, currentUserId]);

  const totalContributions = group.fundTransactions
    .filter((tx) => tx.type === 'contribution')
    .reduce((sum, tx) => sum + tx.amount, 0);

  const totalFundExpenses = group.fundTransactions
    .filter((tx) => tx.type === 'expense')
    .reduce((sum, tx) => sum + tx.amount, 0);

  return (
    <div className="space-y-6 animate-fadeIn">
      {/* Hero Fund Summary Card */}
      <div className="p-6 rounded-2xl bg-gradient-to-br from-emerald-500/10 via-emerald-500/5 to-transparent border border-emerald-500/20 dark:border-emerald-500/30">
        <div className="flex items-start justify-between flex-wrap gap-4">
          <div>
            <div className="flex items-center gap-2 mb-2">
              <div className="p-2 rounded-xl bg-emerald-500 text-white shadow-sm">
                <Wallet className="h-5 w-5" />
              </div>
              <span className="text-xs font-bold text-slate-700 dark:text-slate-300">
                {locale === 'vi-VN' ? 'Số dư Quỹ chung' : 'Group Fund Balance'}
              </span>
            </div>

            <p className="text-3xl lg:text-4xl font-extrabold text-slate-900 dark:text-slate-100 font-mono">
              {isBalanceHidden ? maskValue(group.sharedFund.balance) : formatCurrency(group.sharedFund.balance)}
            </p>

            <p className="text-xs text-slate-500 dark:text-slate-400 mt-2 max-w-md">
              {locale === 'vi-VN'
                ? 'Quỹ nhóm minh bạch được tạo từ các khoản góp tự nguyện và các đợt đóng quỹ có mục đích chung.'
                : 'Transparent group fund formed by voluntary contributions and purpose-driven fund rounds.'}
            </p>
          </div>

          {/* Action Buttons */}
          <div className="flex items-center gap-2.5 flex-wrap">
            <button
              id="btn-open-voluntary-contribute"
              onClick={onOpenContribute}
              className="px-4 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-sm transition-all flex items-center gap-1.5"
            >
              <Plus className="h-4 w-4" />
              <span>{locale === 'vi-VN' ? 'Góp quỹ' : 'Contribute'}</span>
            </button>

            {isOwner && onOpenCreateRound && (
              <button
                id="btn-open-create-fund-round"
                onClick={onOpenCreateRound}
                className="px-4 py-2.5 rounded-xl border border-emerald-600/40 dark:border-emerald-500/40 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 text-xs font-semibold transition-all flex items-center gap-1.5 shadow-sm"
              >
                <Sparkles className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                <span>{locale === 'vi-VN' ? 'Tạo đợt đóng quỹ' : 'Create Fund Round'}</span>
              </button>
            )}

            {isOwner && onOpenRequest && (
              <button
                id="btn-open-create-fund-request"
                onClick={isFrozen ? undefined : onOpenRequest}
                disabled={isFrozen}
                title={isFrozen ? (locale === 'vi-VN' ? 'Nhóm đang trong quá trình giải thể/xóa' : 'Group is frozen for deletion') : undefined}
                className={cn(
                  'px-4 py-2.5 rounded-xl border text-xs font-semibold transition-all flex items-center gap-1.5 shadow-sm',
                  isFrozen
                    ? 'border-slate-200 dark:border-slate-800 text-slate-400 dark:text-slate-600 cursor-not-allowed bg-slate-50 dark:bg-slate-900/40'
                    : 'border-amber-600/40 dark:border-amber-500/40 hover:bg-amber-50 dark:hover:bg-amber-950/40 text-amber-700 dark:text-amber-300'
                )}
              >
                <Send className="h-4 w-4 text-amber-600 dark:text-amber-400" />
                <span>{locale === 'vi-VN' ? 'Tạo yêu cầu đóng quỹ' : 'Create Fund Request'}</span>
              </button>
            )}
          </div>
        </div>

        {/* 3 Stats Pills */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mt-6 pt-4 border-t border-emerald-500/20">
          <div className="p-3 bg-white/70 dark:bg-slate-900/60 rounded-xl border border-emerald-500/10 flex items-center justify-between text-xs">
            <span className="text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN' ? 'Tổng nạp vào quỹ:' : 'Total Contributed:'}
            </span>
            <span className="font-bold text-emerald-600 dark:text-emerald-400 font-mono">
              +{formatCurrency(totalContributions)}
            </span>
          </div>

          <div className="p-3 bg-white/70 dark:bg-slate-900/60 rounded-xl border border-emerald-500/10 flex items-center justify-between text-xs">
            <span className="text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN' ? 'Tổng đã chi từ quỹ:' : 'Total Spent:'}
            </span>
            <span className="font-bold text-slate-700 dark:text-slate-300 font-mono">
              -{formatCurrency(totalFundExpenses)}
            </span>
          </div>

          <div className="p-3 bg-white/70 dark:bg-slate-900/60 rounded-xl border border-emerald-500/10 flex items-center justify-between text-xs">
            <span className="text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN' ? 'Đợt quỹ đang mở:' : 'Active Rounds:'}
            </span>
            <span className="font-bold text-slate-800 dark:text-slate-200">
              {activeRounds.length} {locale === 'vi-VN' ? 'đợt' : 'rounds'}
            </span>
          </div>
        </div>
      </div>

      {/* Sub tabs: Rounds vs Requests vs History */}
      <div className="flex items-center gap-2 border-b border-slate-200 dark:border-slate-800 pb-2">
        <button
          id="tab-sub-fund-rounds"
          onClick={() => setActiveSubTab('rounds')}
          className={cn(
            'px-3.5 py-1.5 rounded-xl text-xs font-semibold transition-all flex items-center gap-1.5',
            activeSubTab === 'rounds'
              ? 'bg-slate-900 text-white dark:bg-slate-100 dark:text-slate-900 shadow-sm'
              : 'text-slate-500 hover:text-slate-800 dark:hover:text-slate-200'
          )}
        >
          <Sparkles className="h-3.5 w-3.5" />
          <span>
            {locale === 'vi-VN' ? 'Đợt đóng quỹ' : 'Fund Rounds'} ({fundRounds.length})
          </span>
        </button>

        <button
          id="tab-sub-fund-requests"
          onClick={() => setActiveSubTab('requests')}
          className={cn(
            'px-3.5 py-1.5 rounded-xl text-xs font-semibold transition-all flex items-center gap-1.5',
            activeSubTab === 'requests'
              ? 'bg-slate-900 text-white dark:bg-slate-100 dark:text-slate-900 shadow-sm'
              : 'text-slate-500 hover:text-slate-800 dark:hover:text-slate-200'
          )}
        >
          <Send className="h-3.5 w-3.5" />
          <span>
            {locale === 'vi-VN' ? 'Yêu cầu đóng quỹ' : 'Fund Requests'} ({visibleRequestsCount})
          </span>
        </button>

        <button
          id="tab-sub-fund-history"
          onClick={() => setActiveSubTab('history')}
          className={cn(
            'px-3.5 py-1.5 rounded-xl text-xs font-semibold transition-all flex items-center gap-1.5',
            activeSubTab === 'history'
              ? 'bg-slate-900 text-white dark:bg-slate-100 dark:text-slate-900 shadow-sm'
              : 'text-slate-500 hover:text-slate-800 dark:hover:text-slate-200'
          )}
        >
          <History className="h-3.5 w-3.5" />
          <span>
            {locale === 'vi-VN' ? 'Lịch sử biến động quỹ' : 'Fund History'} ({group.fundTransactions.length})
          </span>
        </button>
      </div>

      {/* View 1: Fund Rounds */}
      {activeSubTab === 'rounds' && (
        <div className="space-y-4">
          {/* User Pending Obligation Alert Banner */}
          {myPendingTotal > 0 && (
            <div
              id="banner-my-pending-fund-obligation"
              className="p-4 rounded-2xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900/50 flex items-center justify-between flex-wrap gap-3"
            >
              <div className="flex items-center gap-3">
                <div className="p-2 rounded-xl bg-amber-500 text-white shadow-sm shrink-0">
                  <AlertCircle className="h-5 w-5" />
                </div>
                <div>
                  <h4 className="text-xs font-bold text-amber-900 dark:text-amber-200">
                    {locale === 'vi-VN' ? 'Phần của bạn cần đóng' : 'Your Outstanding Share'}
                  </h4>
                  <p className="text-xs text-amber-700 dark:text-amber-300">
                    {locale === 'vi-VN'
                      ? `Bạn còn phải đóng tổng cộng ${formatCurrency(myPendingTotal)} trong các đợt quỹ đang hoạt động.`
                      : `You have a remaining obligation of ${formatCurrency(myPendingTotal)} across active rounds.`}
                  </p>
                </div>
              </div>
              <span className="font-mono font-extrabold text-base text-amber-800 dark:text-amber-300">
                {formatCurrency(myPendingTotal)}
              </span>
            </div>
          )}

          {/* Active Fund Rounds List */}
          {activeRounds.length > 0 ? (
            <div className="space-y-4">
              <h3 className="text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
                {locale === 'vi-VN' ? 'Đợt góp đang thực hiện' : 'Active Fund Rounds'} ({activeRounds.length})
              </h3>

              {activeRounds.map((round) => {
                const myMember = round.members.find((m) => m.userId === currentUserId);
                const isMemberInRound = Boolean(myMember);
                const myRemaining = myMember ? Math.max(0, myMember.requiredAmount - myMember.paidAmount) : 0;
                const isMySharePaid = myMember?.status === 'Paid';
                const percentCollected = Math.min(100, Math.round((round.collectedAmount / round.targetAmount) * 100));
                const paidMembersCount = round.members.filter((m) => m.status === 'Paid').length;
                const isBreakdownExpanded = expandedBreakdownRoundIds.has(round.id);

                return (
                  <div
                    key={round.id}
                    id={`fund-round-card-${round.id}`}
                    className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-5 space-y-4 shadow-sm"
                  >
                    {/* Header */}
                    <div className="flex items-start justify-between flex-wrap gap-3">
                      <div>
                        <div className="flex items-center gap-2">
                          <h4 className="text-base font-bold text-slate-900 dark:text-slate-100">
                            {round.title}
                          </h4>
                          <span className="px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800">
                            {locale === 'vi-VN' ? 'Đang thu' : 'Active'}
                          </span>
                        </div>
                        {round.description && (
                          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                            {round.description}
                          </p>
                        )}
                        <p className="text-[11px] text-slate-400 mt-1 flex items-center gap-1.5">
                          <Calendar className="h-3 w-3" />
                          <span>{round.createdAt}</span>
                          <span>•</span>
                          <span>{round.members.length} {locale === 'vi-VN' ? 'thành viên tham gia' : 'members'}</span>
                        </p>
                      </div>

                      <div className="text-right">
                        <span className="text-xs text-slate-400 block">
                          {locale === 'vi-VN' ? 'Tổng cần góp:' : 'Target amount:'}
                        </span>
                        <span className="text-base font-extrabold text-slate-900 dark:text-slate-100 font-mono">
                          {formatCurrency(round.targetAmount)}
                        </span>
                      </div>
                    </div>

                    {/* Progress Bar */}
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between text-xs">
                        <span className="text-slate-500 dark:text-slate-400">
                          {locale === 'vi-VN' ? 'Đã thu:' : 'Collected:'}{' '}
                          <strong className="text-emerald-600 dark:text-emerald-400 font-mono">
                            {formatCurrency(round.collectedAmount)}
                          </strong>{' '}
                          / {formatCurrency(round.targetAmount)}
                        </span>
                        <span className="font-bold text-slate-700 dark:text-slate-300">
                          {percentCollected}% ({paidMembersCount}/{round.members.length} {locale === 'vi-VN' ? 'đã đóng đủ' : 'paid'})
                        </span>
                      </div>
                      <div className="w-full h-2.5 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
                        <div
                          className="h-full bg-emerald-500 rounded-full transition-all duration-500"
                          style={{ width: `${percentCollected}%` }}
                        />
                      </div>
                    </div>

                    {/* Current User Share Obligation Card */}
                    {isMemberInRound && myMember && (
                      <div className="p-4 rounded-xl border border-emerald-200 dark:border-emerald-900/50 bg-emerald-50/40 dark:bg-emerald-950/20 flex items-center justify-between flex-wrap gap-3">
                        <div className="space-y-1">
                          <span className="text-[11px] font-bold text-emerald-800 dark:text-emerald-300 uppercase tracking-wider block">
                            {locale === 'vi-VN' ? 'Phần của bạn trong đợt này' : 'Your Share in this Round'}
                          </span>
                          <div className="flex items-center gap-3 text-xs">
                            <span className="text-slate-600 dark:text-slate-400">
                              {locale === 'vi-VN' ? 'Mục tiêu:' : 'Required:'}{' '}
                              <strong className="text-slate-900 dark:text-white font-mono">
                                {formatCurrency(myMember.requiredAmount)}
                              </strong>
                            </span>
                            <span>•</span>
                            <span className="text-slate-600 dark:text-slate-400">
                              {locale === 'vi-VN' ? 'Đã đóng:' : 'Paid:'}{' '}
                              <strong className="text-emerald-600 dark:text-emerald-400 font-mono">
                                {formatCurrency(myMember.paidAmount)}
                              </strong>
                            </span>
                          </div>
                          {!isMySharePaid && (
                            <p className="text-xs font-bold text-amber-700 dark:text-amber-400">
                              {locale === 'vi-VN' ? 'Còn phải đóng:' : 'Remaining to pay:'}{' '}
                              <span className="font-mono">{formatCurrency(myRemaining)}</span>
                            </p>
                          )}
                        </div>

                        <div>
                          {isMySharePaid ? (
                            <span className="px-3.5 py-1.5 rounded-xl bg-emerald-100 dark:bg-emerald-900/50 text-emerald-800 dark:text-emerald-200 text-xs font-bold flex items-center gap-1.5 border border-emerald-300 dark:border-emerald-800">
                              <CheckCircle2 className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                              <span>{locale === 'vi-VN' ? 'Đã đóng đủ' : 'Paid in Full'}</span>
                            </span>
                          ) : (
                            onPayRound && (
                              <button
                                id={`btn-pay-my-share-${round.id}`}
                                onClick={() => onPayRound(round)}
                                className="px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold shadow-sm transition-all flex items-center gap-1.5"
                              >
                                <Wallet className="h-4 w-4" />
                                <span>{locale === 'vi-VN' ? 'Đóng phần của tôi' : 'Pay My Share'}</span>
                              </button>
                            )
                          )}
                        </div>
                      </div>
                    )}

                    {/* Expandable Member Breakdown Toggle */}
                    <div className="pt-2 border-t border-slate-100 dark:border-slate-800">
                      <button
                        type="button"
                        onClick={() => toggleBreakdown(round.id)}
                        className="text-xs font-semibold text-slate-500 hover:text-slate-800 dark:hover:text-slate-200 flex items-center gap-1 transition-colors"
                      >
                        <Users className="h-3.5 w-3.5" />
                        <span>
                          {locale === 'vi-VN' ? 'Chi tiết thành viên' : 'Member Breakdown'} ({round.members.length})
                        </span>
                        {isBreakdownExpanded ? (
                          <ChevronUp className="h-3.5 w-3.5" />
                        ) : (
                          <ChevronDown className="h-3.5 w-3.5" />
                        )}
                      </button>

                      {isBreakdownExpanded && (
                        <div className="mt-3 divide-y divide-slate-100 dark:divide-slate-800/80 bg-slate-50/50 dark:bg-slate-800/30 rounded-xl p-3">
                          {round.members.map((m) => {
                            const isMe = m.userId === currentUserId;
                            const isPaid = m.status === 'Paid';
                            const remaining = Math.max(0, m.requiredAmount - m.paidAmount);

                            return (
                              <div
                                key={m.userId}
                                className="py-2 flex items-center justify-between text-xs first:pt-0 last:pb-0"
                              >
                                <div className="flex items-center gap-2">
                                  <span className="font-semibold text-slate-900 dark:text-slate-100">
                                    {m.userName}
                                    {isMe && (
                                      <span className="ml-1 text-[11px] text-emerald-600 dark:text-emerald-400">
                                        ({locale === 'vi-VN' ? 'Bạn' : 'You'})
                                      </span>
                                    )}
                                  </span>
                                </div>

                                <div className="flex items-center gap-3">
                                  <span className="text-slate-500 font-mono">
                                    {formatCurrency(m.paidAmount)} / {formatCurrency(m.requiredAmount)}
                                  </span>
                                  {isPaid ? (
                                    <span className="px-2 py-0.5 rounded-md bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800 text-[10px] font-bold">
                                      {locale === 'vi-VN' ? 'Đã đóng' : 'Paid'}
                                    </span>
                                  ) : (
                                    <span className="px-2 py-0.5 rounded-md bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-300 border border-amber-200 dark:border-amber-800 text-[10px] font-bold">
                                      {locale === 'vi-VN' ? `Còn ${formatCurrency(remaining)}` : `Left ${formatCurrency(remaining)}`}
                                    </span>
                                  )}
                                </div>
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="p-8 text-center bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl space-y-3">
              <Sparkles className="h-8 w-8 text-slate-400 mx-auto" />
              <h4 className="text-xs font-bold text-slate-700 dark:text-slate-300">
                {locale === 'vi-VN' ? 'Chưa có đợt góp quỹ nào đang hoạt động' : 'No active fund rounds'}
              </h4>
              <p className="text-xs text-slate-400 max-w-sm mx-auto">
                {isOwner
                  ? (locale === 'vi-VN'
                      ? 'Bạn có thể tạo đợt đóng quỹ cho chuyến đi, sinh hoạt phòng trọ, tiệc tùng hoặc các chi phí chung.'
                      : 'You can create a fund round for trips, roommates, parties, or shared group plans.')
                  : (locale === 'vi-VN'
                      ? 'Chủ nhóm sẽ tạo các đợt đóng quỹ khi có kế hoạch chung cần đóng góp.'
                      : 'The owner will create fund rounds when shared plans require contributions.')}
              </p>
              {isOwner && onOpenCreateRound && (
                <button
                  onClick={onOpenCreateRound}
                  className="px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-sm transition-all inline-flex items-center gap-1.5"
                >
                  <Sparkles className="h-4 w-4" />
                  <span>{locale === 'vi-VN' ? 'Tạo đợt đóng quỹ' : 'Create Fund Round'}</span>
                </button>
              )}
            </div>
          )}

          {/* Completed Fund Rounds List */}
          {completedRounds.length > 0 && (
            <div className="space-y-3 pt-4">
              <h3 className="text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
                {locale === 'vi-VN' ? 'Đợt quỹ đã hoàn thành' : 'Completed Fund Rounds'} ({completedRounds.length})
              </h3>

              {completedRounds.map((round) => (
                <div
                  key={round.id}
                  className="bg-slate-50/80 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800 rounded-2xl p-4 flex items-center justify-between gap-4"
                >
                  <div>
                    <div className="flex items-center gap-2">
                      <h4 className="text-xs font-bold text-slate-800 dark:text-slate-200">
                        {round.title}
                      </h4>
                      <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800 flex items-center gap-1">
                        <CheckCircle2 className="h-3 w-3" />
                        <span>{locale === 'vi-VN' ? 'Quỹ đã hoàn thành' : 'Fund completed'}</span>
                      </span>
                    </div>
                    <p className="text-[11px] text-slate-400 mt-0.5">
                      {locale === 'vi-VN' ? 'Hoàn tất lúc:' : 'Completed at:'} {round.completedAt || round.createdAt} • {round.members.length} {locale === 'vi-VN' ? 'thành viên đã đóng đủ 100%' : 'members fully paid'}
                    </p>
                  </div>

                  <div className="text-right">
                    <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400 font-mono">
                      {formatCurrency(round.targetAmount)}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* View 2: Fund Requests */}
      {activeSubTab === 'requests' && (
        <div className="space-y-4">
          {/* Member view: Pending Obligation Alert Banner */}
          {!isOwner && myPendingFundRequestsTotal > 0 && (
            <div
              id="banner-my-pending-fund-requests"
              className="p-4 rounded-2xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900/50 flex items-center justify-between flex-wrap gap-3"
            >
              <div className="flex items-center gap-3">
                <div className="p-2 rounded-xl bg-amber-500 text-white shadow-sm shrink-0">
                  <AlertCircle className="h-5 w-5" />
                </div>
                <div>
                  <h4 className="text-xs font-bold text-amber-900 dark:text-amber-200">
                    {locale === 'vi-VN' ? 'Yêu cầu đóng quỹ đang chờ bạn thanh toán' : 'Pending Fund Requests For You'}
                  </h4>
                  <p className="text-xs text-amber-700 dark:text-amber-300">
                    {locale === 'vi-VN'
                      ? `Bạn có yêu cầu đóng quỹ chưa hoàn tất với tổng số tiền ${formatCurrency(myPendingFundRequestsTotal)}.`
                      : `You have pending fund requests totaling ${formatCurrency(myPendingFundRequestsTotal)}.`}
                  </p>
                </div>
              </div>
              <span className="font-mono font-extrabold text-base text-amber-800 dark:text-amber-300">
                {formatCurrency(myPendingFundRequestsTotal)}
              </span>
            </div>
          )}

          {isOwner ? (
            /* OWNER VIEW */
            allFundRequests.length > 0 ? (
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
                    {locale === 'vi-VN' ? 'Danh sách yêu cầu đóng quỹ' : 'Fund Requests List'} ({allFundRequests.length})
                  </h3>
                  {onOpenRequest && !isFrozen && (
                    <button
                      onClick={onOpenRequest}
                      className="text-xs font-semibold text-amber-600 dark:text-amber-400 hover:underline inline-flex items-center gap-1"
                    >
                      <Plus className="h-3.5 w-3.5" />
                      <span>{locale === 'vi-VN' ? 'Tạo yêu cầu mới' : 'Create new request'}</span>
                    </button>
                  )}
                </div>

                {allFundRequests.map((req) => {
                  const isExpanded = expandedBreakdownRequestIds.has(req.id);
                  const isPastDue = isOverdue(req.dueDate);
                  const paidCount = req.recipients.filter((r) => r.status === 'Paid').length;
                  const pendingCount = req.recipients.filter((r) => r.status === 'Pending' && !isPastDue).length;
                  const overdueCount = req.recipients.filter(
                    (r) => r.status === 'Overdue' || (r.status === 'Pending' && isPastDue)
                  ).length;
                  const totalExpected = req.amountPerMember * req.recipients.length;
                  const totalCollected = req.amountPerMember * paidCount;

                  return (
                    <div
                      key={req.id}
                      className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-5 shadow-sm space-y-4 transition-all"
                    >
                      <div className="flex items-start justify-between flex-wrap gap-3">
                        <div className="space-y-1">
                          <div className="flex items-center gap-2 flex-wrap">
                            <span className="p-1.5 rounded-lg bg-amber-50 dark:bg-amber-950/80 text-amber-600 dark:text-amber-400">
                              <Send className="h-4 w-4" />
                            </span>
                            <h4 className="text-sm font-bold text-slate-900 dark:text-slate-100">
                              {req.purpose}
                            </h4>
                            {isPastDue && pendingCount === 0 && overdueCount > 0 && (
                              <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-rose-100 dark:bg-rose-950 text-rose-700 dark:text-rose-300 border border-rose-300 dark:border-rose-800">
                                {locale === 'vi-VN' ? 'Đã quá hạn' : 'Past due'}
                              </span>
                            )}
                            {paidCount === req.recipients.length && (
                              <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800 flex items-center gap-1">
                                <CheckCircle2 className="h-3 w-3" />
                                <span>{locale === 'vi-VN' ? 'Đủ 100%' : '100% Collected'}</span>
                              </span>
                            )}
                          </div>
                          <div className="flex items-center gap-3 text-xs text-slate-400 flex-wrap">
                            <span>
                              {locale === 'vi-VN' ? 'Tạo ngày:' : 'Created:'} {req.createdDate}
                            </span>
                            <span>•</span>
                            <span className={cn(isPastDue && overdueCount > 0 ? 'text-rose-600 dark:text-rose-400 font-semibold' : '')}>
                              {locale === 'vi-VN' ? 'Hạn nộp:' : 'Due date:'} {req.dueDate}
                              {isPastDue && overdueCount > 0 ? ` (${locale === 'vi-VN' ? 'Quá hạn' : 'Overdue'})` : ''}
                            </span>
                          </div>
                        </div>

                        <div className="text-right">
                          <div className="text-xs text-slate-400">
                            {locale === 'vi-VN' ? 'Số tiền mỗi người:' : 'Amount per member:'}
                          </div>
                          <div className="text-base font-extrabold text-amber-700 dark:text-amber-300 font-mono">
                            {formatCurrency(req.amountPerMember)}
                          </div>
                        </div>
                      </div>

                      {/* Recipient summary metrics */}
                      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
                        <div className="p-2.5 rounded-xl bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800">
                          <div className="text-[11px] text-slate-400">
                            {locale === 'vi-VN' ? 'Người nhận' : 'Recipients'}
                          </div>
                          <div className="text-xs font-bold text-slate-800 dark:text-slate-200 mt-0.5">
                            {req.recipients.length} {locale === 'vi-VN' ? 'thành viên' : 'members'}
                          </div>
                        </div>

                        <div className="p-2.5 rounded-xl bg-emerald-50/70 dark:bg-emerald-950/30 border border-emerald-200/50 dark:border-emerald-800/50">
                          <div className="text-[11px] text-emerald-700 dark:text-emerald-400">
                            {locale === 'vi-VN' ? 'Đã đóng' : 'Paid'}
                          </div>
                          <div className="text-xs font-bold text-emerald-700 dark:text-emerald-300 mt-0.5">
                            {paidCount}/{req.recipients.length} ({formatCurrency(totalCollected)})
                          </div>
                        </div>

                        <div className="p-2.5 rounded-xl bg-amber-50/70 dark:bg-amber-950/30 border border-amber-200/50 dark:border-amber-800/50">
                          <div className="text-[11px] text-amber-700 dark:text-amber-400">
                            {locale === 'vi-VN' ? 'Chờ đóng' : 'Pending'}
                          </div>
                          <div className="text-xs font-bold text-amber-700 dark:text-amber-300 mt-0.5">
                            {pendingCount} {locale === 'vi-VN' ? 'người' : 'members'}
                          </div>
                        </div>

                        <div className="p-2.5 rounded-xl bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800">
                          <div className="text-[11px] text-slate-400">
                            {locale === 'vi-VN' ? 'Quá hạn' : 'Overdue'}
                          </div>
                          <div className={cn('text-xs font-bold mt-0.5', overdueCount > 0 ? 'text-rose-600 dark:text-rose-400' : 'text-slate-700 dark:text-slate-300')}>
                            {overdueCount} {locale === 'vi-VN' ? 'người' : 'members'}
                          </div>
                        </div>
                      </div>

                      {/* Recipient breakdown accordion */}
                      <div className="pt-2 border-t border-slate-100 dark:border-slate-800">
                        <button
                          type="button"
                          onClick={() => toggleRequestBreakdown(req.id)}
                          className="w-full flex items-center justify-between text-xs font-semibold text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200 py-1"
                        >
                          <span className="flex items-center gap-1.5">
                            <Users className="h-3.5 w-3.5 text-slate-400" />
                            <span>
                              {locale === 'vi-VN' ? 'Chi tiết người nhận' : 'Recipient Details'} ({req.recipients.length})
                            </span>
                          </span>
                          {isExpanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
                        </button>

                        {isExpanded && (
                          <div className="space-y-1.5 mt-2.5 pt-2 border-t border-slate-100 dark:border-slate-800">
                            {req.recipients.map((rec) => {
                              const recEffectiveStatus =
                                rec.status === 'Pending' && isPastDue ? 'Overdue' : rec.status;
                              return (
                                <div
                                  key={rec.userId}
                                  className="flex items-center justify-between p-2.5 rounded-xl bg-slate-50 dark:bg-slate-950 text-xs"
                                >
                                  <div className="flex items-center gap-2">
                                    {recEffectiveStatus === 'Paid' ? (
                                      <CheckCircle2 className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                                    ) : recEffectiveStatus === 'Overdue' ? (
                                      <AlertCircle className="h-4 w-4 text-rose-500" />
                                    ) : (
                                      <Clock className="h-4 w-4 text-amber-500" />
                                    )}
                                    <span className="font-semibold text-slate-800 dark:text-slate-200">
                                      {rec.userName}
                                    </span>
                                  </div>

                                  <div className="flex items-center gap-2.5">
                                    <span className="font-mono text-slate-600 dark:text-slate-400">
                                      {formatCurrency(req.amountPerMember)}
                                    </span>
                                    <span
                                      className={cn(
                                        'px-2 py-0.5 rounded-full text-[10px] font-bold border',
                                        recEffectiveStatus === 'Paid'
                                          ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border-emerald-300 dark:border-emerald-800'
                                          : recEffectiveStatus === 'Overdue'
                                            ? 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300 border-rose-300 dark:border-rose-800'
                                            : 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 border-amber-300 dark:border-amber-800'
                                      )}
                                    >
                                      {recEffectiveStatus === 'Paid'
                                        ? locale === 'vi-VN' ? 'Đã đóng' : 'Paid'
                                        : recEffectiveStatus === 'Overdue'
                                          ? locale === 'vi-VN' ? 'Quá hạn' : 'Overdue'
                                          : locale === 'vi-VN' ? 'Chờ đóng' : 'Pending'}
                                    </span>
                                  </div>
                                </div>
                              );
                            })}
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div className="p-8 text-center bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl space-y-3">
                <div className="mx-auto w-12 h-12 rounded-2xl bg-amber-50 dark:bg-amber-950/50 flex items-center justify-center text-amber-600 dark:text-amber-400">
                  <Send className="h-6 w-6" />
                </div>
                <h4 className="text-xs font-bold text-slate-800 dark:text-slate-200">
                  {locale === 'vi-VN' ? 'Chưa có yêu cầu đóng quỹ nào' : 'No Fund Requests Yet'}
                </h4>
                <p className="text-xs text-slate-500 dark:text-slate-400 max-w-sm mx-auto">
                  {locale === 'vi-VN'
                    ? 'Chủ nhóm có thể tạo yêu cầu đóng góp đến từng thành viên cụ thể khi phát sinh chi phí hoặc mục đích riêng.'
                    : 'The Group Owner can create contribution requests targeting specific members when needed.'}
                </p>
                {onOpenRequest && !isFrozen && (
                  <button
                    onClick={onOpenRequest}
                    className="px-4 py-2 rounded-xl bg-amber-600 hover:bg-amber-700 text-white text-xs font-semibold shadow-sm transition-all inline-flex items-center gap-1.5"
                  >
                    <Send className="h-4 w-4" />
                    <span>{locale === 'vi-VN' ? 'Tạo yêu cầu đóng quỹ' : 'Create Fund Request'}</span>
                  </button>
                )}
              </div>
            )
          ) : (
            /* MEMBER VIEW */
            myFundRequests.length > 0 ? (
              <div className="space-y-4">
                <h3 className="text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
                  {locale === 'vi-VN' ? 'Yêu cầu đóng quỹ gửi đến bạn' : 'Fund Requests Assigned To You'} ({myFundRequests.length})
                </h3>

                {myFundRequests.map((req) => {
                  const myRec = req.recipients.find((r) => r.userId === currentUserId);
                  if (!myRec) return null;
                  const isPastDue = isOverdue(req.dueDate);
                  const effectiveStatus =
                    myRec.status === 'Pending' && isPastDue ? 'Overdue' : myRec.status;

                  return (
                    <div
                      key={req.id}
                      className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-5 shadow-sm space-y-4"
                    >
                      <div className="flex items-start justify-between flex-wrap gap-3">
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <span className="p-1.5 rounded-lg bg-amber-50 dark:bg-amber-950/80 text-amber-600 dark:text-amber-400">
                              <Send className="h-4 w-4" />
                            </span>
                            <h4 className="text-sm font-bold text-slate-900 dark:text-slate-100">
                              {req.purpose}
                            </h4>
                          </div>
                          <div className="flex items-center gap-3 text-xs text-slate-400 flex-wrap">
                            <span>
                              {locale === 'vi-VN' ? 'Tạo ngày:' : 'Created:'} {req.createdDate}
                            </span>
                            <span>•</span>
                            <span className={cn(effectiveStatus === 'Overdue' ? 'text-rose-600 dark:text-rose-400 font-semibold' : '')}>
                              {locale === 'vi-VN' ? 'Hạn chót:' : 'Due date:'} {req.dueDate}
                              {effectiveStatus === 'Overdue' ? ` (${locale === 'vi-VN' ? 'Đã quá hạn' : 'Overdue'})` : ''}
                            </span>
                          </div>
                        </div>

                        <div className="text-right">
                          <span className="text-base font-extrabold text-slate-900 dark:text-slate-100 font-mono">
                            {formatCurrency(req.amountPerMember)}
                          </span>
                        </div>
                      </div>

                      {/* Status & Action */}
                      <div className="flex items-center justify-between pt-3 border-t border-slate-100 dark:border-slate-800 flex-wrap gap-2">
                        <div>
                          {effectiveStatus === 'Paid' && (
                            <span className="px-3 py-1 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800 flex items-center gap-1.5">
                              <CheckCircle2 className="h-3.5 w-3.5" />
                              <span>{locale === 'vi-VN' ? 'Đã đóng hoàn tất' : 'Paid'}</span>
                            </span>
                          )}

                          {effectiveStatus === 'Pending' && (
                            <span className="px-3 py-1 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 border border-amber-300 dark:border-amber-800 flex items-center gap-1.5">
                              <Clock className="h-3.5 w-3.5" />
                              <span>{locale === 'vi-VN' ? 'Chờ đóng quỹ' : 'Pending Payment'}</span>
                            </span>
                          )}

                          {effectiveStatus === 'Overdue' && (
                            <span className="px-3 py-1 rounded-full text-xs font-semibold bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300 border border-rose-300 dark:border-rose-800 flex items-center gap-1.5">
                              <AlertCircle className="h-3.5 w-3.5" />
                              <span>{locale === 'vi-VN' ? 'Quá hạn thanh toán' : 'Overdue'}</span>
                            </span>
                          )}

                          {effectiveStatus === 'Cancelled' && (
                            <span className="px-3 py-1 rounded-full text-xs font-semibold bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-300 dark:border-slate-700">
                              <span>{locale === 'vi-VN' ? 'Đã hủy' : 'Cancelled'}</span>
                            </span>
                          )}
                        </div>

                        {/* Pay action if pending and payable */}
                        {effectiveStatus === 'Pending' && onPayRequest && (
                          <button
                            type="button"
                            onClick={() => onPayRequest(req)}
                            className="px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-sm transition-all flex items-center gap-1.5"
                          >
                            <Wallet className="h-4 w-4" />
                            <span>{locale === 'vi-VN' ? 'Đóng quỹ' : 'Pay Fund Request'}</span>
                          </button>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div className="p-8 text-center bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl space-y-2">
                <div className="mx-auto w-12 h-12 rounded-2xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center text-slate-400">
                  <Send className="h-6 w-6" />
                </div>
                <h4 className="text-xs font-bold text-slate-800 dark:text-slate-200">
                  {locale === 'vi-VN' ? 'Bạn không có yêu cầu đóng quỹ nào' : 'No Fund Requests For You'}
                </h4>
                <p className="text-xs text-slate-500 dark:text-slate-400 max-w-sm mx-auto">
                  {locale === 'vi-VN'
                    ? 'Hiện không có yêu cầu đóng quỹ nào gửi đến bạn.'
                    : 'There are currently no fund requests assigned to you.'}
                </p>
              </div>
            )
          )}
        </div>
      )}

      {/* View 3: Fund History */}
      {activeSubTab === 'history' && (
        <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl overflow-hidden shadow-sm">
          {group.fundTransactions.length > 0 ? (
            <div className="divide-y divide-slate-100 dark:divide-slate-800">
              {group.fundTransactions.map((tx) => {
                const isDeposit = tx.type === 'contribution';
                return (
                  <div
                    key={tx.id}
                    className="p-4 flex items-center justify-between gap-4 hover:bg-slate-50/60 dark:hover:bg-slate-950/40 transition-colors"
                  >
                    <div className="flex items-center gap-3">
                      <div
                        className={cn(
                          'p-2 rounded-xl text-white shadow-sm',
                          isDeposit ? 'bg-emerald-500' : 'bg-slate-600 dark:bg-slate-700'
                        )}
                      >
                        {isDeposit ? <ArrowDownLeft className="h-4 w-4" /> : <ArrowUpRight className="h-4 w-4" />}
                      </div>

                      <div>
                        <h4 className="text-xs font-bold text-slate-900 dark:text-slate-100">
                          {tx.description}
                        </h4>
                        <p className="text-[11px] text-slate-400 mt-0.5">
                          {tx.userName} • <span className="font-mono">{tx.referenceCode}</span> • {tx.date}
                          {tx.sourceWalletName && ` • ${tx.sourceWalletName}`}
                        </p>
                      </div>
                    </div>

                    <div className="text-right">
                      <span
                        className={cn(
                          'font-mono font-bold text-sm',
                          isDeposit ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-900 dark:text-slate-100'
                        )}
                      >
                        {isDeposit ? '+' : '-'}{formatCurrency(tx.amount)}
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="p-8 text-center text-xs text-slate-400">
              {locale === 'vi-VN' ? 'Chưa có biến động quỹ nào.' : 'No fund transactions yet.'}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
