import React, { useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Users,
  Plus,
  Receipt,
  ChevronRight,
  DollarSign,
  PieChart,
  Wallet,
  Shield,
  Sparkles,
  ArrowRight,
  ArrowUpRight,
  Building,
  Palmtree,
  HeartHandshake,
  Coffee,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { useGroup } from '../../../providers/GroupProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { formatMoney } from '../../../utils/money';
import { Button } from '../../../components/ui/Button';
import { Group, GroupCategory } from '../../../types/groups';
import { cn } from '../../../utils/cn';

// Sub-tabs in Group Detail
import { GroupOverviewTab } from '../../groups/tabs/GroupOverviewTab';
import { GroupMembersTab } from '../../groups/tabs/GroupMembersTab';
import { GroupFundTab } from '../../groups/tabs/GroupFundTab';
import { GroupExpensesTab } from '../../groups/tabs/GroupExpensesTab';

// Modals
import { CreateGroupModal } from '../../groups/modals/CreateGroupModal';
import { InviteMemberModal } from '../../groups/modals/InviteMemberModal';
import { FundContributionModal } from '../../groups/modals/FundContributionModal';
import { CreateFundRoundModal } from '../../groups/modals/CreateFundRoundModal';
import { AddSharedExpenseModal } from '../../groups/modals/AddSharedExpenseModal';
import { OwnerSettingsModal } from '../../groups/modals/OwnerSettingsModal';

/**
 * GROUPS LAYOUT A — BALANCED WORKSPACE
 * Triết lý bố cục:
 * 1. Danh sách nhóm (Group List View):
 *    - Hàng trên: Banner Hero tổng quan tài chính quỹ nhóm (Tổng số nhóm, Tổng quỹ tích lũy, Đợt góp đang chạy, Phần cần đóng).
 *    - Khu vực chính: Grid 3 cột các thẻ nhóm với phân cấp rõ ràng (Avatar thành viên, số dư quỹ, trạng thái đợt góp).
 *    - Dưới cùng: Bảng theo dõi hoạt động chi tiêu chung mới nhất dàn đều trang.
 * 2. Chi tiết nhóm (Group Detail View):
 *    - Header nhóm với định danh phân quyền (Owner / Member badge) và nút tác vụ chính.
 *    - Tab chuyển đổi mạch lạc: Tổng quan, Quỹ nhóm, Khoản chi, Thành viên.
 *    - Tích hợp sẵn bộ chuyển đổi giữa Danh sách và Nhóm mẫu để duyệt trực tiếp trong DEV Preview.
 */
export const GroupsLayoutA: React.FC = () => {
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();
  const { groups: contextGroups, createGroup } = useGroup();
  const { currentUser } = useAuth();
  const currentUserId = currentUser?.id;
  const navigate = useNavigate();

  // Mode switcher for preview: 'list' | 'detail'
  const [viewMode, setViewMode] = useState<'list' | 'detail'>('list');
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);

  // Group Detail active tab
  const [activeTab, setActiveTab] = useState<'overview' | 'fund' | 'expenses' | 'members'>('overview');

  // Modal states
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isInviteModalOpen, setIsInviteModalOpen] = useState(false);
  const [isFundContribModalOpen, setIsFundContribModalOpen] = useState(false);
  const [isCreateFundRoundOpen, setIsCreateFundRoundOpen] = useState(false);
  const [isAddExpenseModalOpen, setIsAddExpenseModalOpen] = useState(false);
  const [isOwnerSettingsOpen, setIsOwnerSettingsOpen] = useState(false);

  // Fallback demo group if context groups is empty
  const activeGroup = useMemo(() => {
    if (selectedGroupId) {
      const found = contextGroups.find((g) => g.id === selectedGroupId);
      if (found) return found;
    }
    if (contextGroups.length > 0) return contextGroups[0];

    // Mock fallback group for rich preview
    return {
      id: 'demo-group-1',
      name: 'Gia đình nhỏ & Chuyến đi Đà Lạt',
      type: 'Trip' as GroupCategory,
      description: 'Quỹ tiết kiệm và chia sẻ chi tiêu cho kỳ nghỉ chung',
      ownerId: currentUserId || 'owner-1',
      currentUserRole: 'Owner',
      members: [
        { id: 'm1', userId: currentUserId || 'user-1', name: 'Bạn (Quản trị)', role: 'Owner', avatarInitials: 'ME' },
        { id: 'm2', userId: 'user-2', name: 'Minh Trang', role: 'Member', avatarInitials: 'MT' },
        { id: 'm3', userId: 'user-3', name: 'Đức Anh', role: 'Member', avatarInitials: 'DA' },
      ],
      sharedFund: { balance: 14500000, targetAmount: 20000000 },
      expenses: [
        { id: 'e1', title: 'Khách sạn Colline Đà Lạt 3N2Đ', amount: 5400000, date: '2026-10-12', payer: 'Bạn', paidByType: 'fund' },
        { id: 'e2', title: 'Ăn tối lẩu gà lá é Tao Ngộ', amount: 820000, date: '2026-10-11', payer: 'Minh Trang', paidByType: 'user' },
      ],
      fundRounds: [
        {
          id: 'fr1',
          name: 'Đợt 1: Đặt cọc phòng & Vé xe',
          status: 'Active',
          members: [
            { userId: currentUserId || 'user-1', requiredAmount: 2000000, paidAmount: 2000000, status: 'Paid' },
            { userId: 'user-2', requiredAmount: 2000000, paidAmount: 1500000, status: 'Partial' },
          ],
        },
      ],
    } as unknown as Group;
  }, [selectedGroupId, contextGroups, currentUserId]);

  // Aggregated metrics from groups
  const metrics = useMemo(() => {
    const list = contextGroups.length > 0 ? contextGroups : [activeGroup];
    let totalFund = 0;
    let activeRounds = 0;
    let pendingMe = 0;
    const memberSet = new Set<string>();
    const expenses: { id: string; groupName: string; title: string; payer: string; amount: number; time: string }[] = [];

    list.forEach((g) => {
      totalFund += g.sharedFund?.balance || 0;
      (g.members || []).forEach((m) => {
        if (m.userId) memberSet.add(m.userId);
      });
      (g.fundRounds || []).forEach((r) => {
        if (r.status === 'Active') {
          activeRounds++;
          const mem = r.members.find((m) => m.userId === currentUserId);
          if (mem && mem.status !== 'Paid') {
            pendingMe += Math.max(0, mem.requiredAmount - mem.paidAmount);
          }
        }
      });
      (g.expenses || []).forEach((e) => {
        expenses.push({
          id: e.id,
          groupName: g.name,
          title: e.title,
          payer: e.paidByType === 'fund' ? (locale === 'vi-VN' ? 'Quỹ chung' : 'Shared Fund') : 'Thành viên',
          amount: e.amount,
          time: e.date,
        });
      });
    });

    return {
      totalGroups: list.length,
      totalMembers: memberSet.size || list.reduce((acc, g) => acc + (g.members?.length || 0), 0),
      totalFund,
      activeRounds,
      pendingMe,
      recentExpenses: expenses.slice(0, 5),
    };
  }, [contextGroups, activeGroup, currentUserId, locale]);

  const getCategoryMeta = (type: GroupCategory) => {
    switch (type) {
      case 'Trip':
        return { icon: Palmtree, iconBg: 'bg-teal-100 dark:bg-teal-950/60', iconColor: 'text-teal-600 dark:text-teal-400' };
      case 'Roommates':
        return { icon: Building, iconBg: 'bg-indigo-100 dark:bg-indigo-950/60', iconColor: 'text-indigo-600 dark:text-indigo-400' };
      case 'Family':
        return { icon: HeartHandshake, iconBg: 'bg-rose-100 dark:bg-rose-950/60', iconColor: 'text-rose-600 dark:text-rose-400' };
      default:
        return { icon: Coffee, iconBg: 'bg-amber-100 dark:bg-amber-950/60', iconColor: 'text-amber-600 dark:text-amber-400' };
    }
  };

  const isOwner = activeGroup.currentUserRole === 'Owner' || activeGroup.ownerId === currentUserId;

  return (
    <div className="space-y-6">
      {/* View Switcher Top Toolbar */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 p-4 rounded-2xl bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">
            {locale === 'vi-VN' ? 'Chế độ xem Preview:' : 'Preview Sub-view:'}
          </span>
          <div className="inline-flex p-1 rounded-xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
            <button
              onClick={() => setViewMode('list')}
              className={cn(
                'px-3 py-1.5 rounded-lg text-xs font-bold transition-all',
                viewMode === 'list'
                  ? 'bg-emerald-600 text-white shadow-xs'
                  : 'text-slate-600 dark:text-slate-300 hover:text-emerald-600'
              )}
            >
              {locale === 'vi-VN' ? '1. Danh sách nhóm (List)' : '1. Group List'}
            </button>
            <button
              onClick={() => setViewMode('detail')}
              className={cn(
                'px-3 py-1.5 rounded-lg text-xs font-bold transition-all',
                viewMode === 'detail'
                  ? 'bg-emerald-600 text-white shadow-xs'
                  : 'text-slate-600 dark:text-slate-300 hover:text-emerald-600'
              )}
            >
              {locale === 'vi-VN' ? '2. Chi tiết nhóm (Detail Tab)' : '2. Group Detail'}
            </button>
          </div>
        </div>

        <div className="text-xs text-slate-500 dark:text-slate-400 flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-emerald-500 inline-block animate-pulse" />
          <span>Layout A: Bố cục cân đối (Balanced Workspace) — Giữ 100% bản sắc SpendWise</span>
        </div>
      </div>

      {/* VIEW 1: GROUP LIST VIEW */}
      {viewMode === 'list' && (
        <div className="space-y-6">
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

            <Button
              onClick={() => setIsCreateModalOpen(true)}
              className="gap-1.5 shadow-sm"
            >
              <Plus className="h-4 w-4" />
              <span>{t('groups.createGroup')}</span>
            </Button>
          </div>

          {/* KPI Summary Card - Balanced 4 Columns */}
          <div className="bg-white dark:bg-[#0F172A] text-slate-900 dark:text-white rounded-3xl p-6 sm:p-7 border border-slate-100 dark:border-slate-800 shadow-sm">
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 items-center">
              <div>
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {t('groups.groupsJoined')}
                </span>
                <p className="text-2xl sm:text-3xl font-bold text-slate-900 dark:text-white mt-1">
                  {metrics.totalGroups}
                </p>
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                  {metrics.totalMembers} {locale === 'vi-VN' ? 'thành viên kết nối' : 'connected members'}
                </p>
              </div>

              <div>
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {t('groups.totalSharedFund')}
                </span>
                <p className="text-2xl sm:text-3xl font-bold text-emerald-600 dark:text-emerald-400 mt-1 tabular-nums">
                  {isHidden ? '••••••••' : formatMoney(metrics.totalFund, locale)}
                </p>
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">{t('groups.coManaged')}</p>
              </div>

              <div>
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {locale === 'vi-VN' ? 'Đợt góp đang chạy' : 'Active Fund Rounds'}
                </span>
                <p className="text-2xl sm:text-3xl font-bold text-slate-900 dark:text-white mt-1 tabular-nums">
                  {metrics.activeRounds}
                </p>
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                  {locale === 'vi-VN' ? 'Đợt đóng quỹ tập thể' : 'Fund rounds active'}
                </p>
              </div>

              <div>
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {locale === 'vi-VN' ? 'Phần bạn cần đóng' : 'Pending Contribution'}
                </span>
                <p
                  className={cn(
                    'text-2xl sm:text-3xl font-bold mt-1 tabular-nums',
                    metrics.pendingMe > 0 ? 'text-indigo-600 dark:text-indigo-400' : 'text-emerald-600 dark:text-emerald-400'
                  )}
                >
                  {isHidden
                    ? '••••••••'
                    : metrics.pendingMe === 0
                    ? locale === 'vi-VN'
                      ? 'Đã hoàn tất'
                      : 'All Settled'
                    : formatMoney(metrics.pendingMe, locale)}
                </p>
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                  {metrics.pendingMe === 0
                    ? locale === 'vi-VN'
                      ? 'Mọi nghĩa vụ quỹ đã đủ'
                      : 'No pending rounds'
                    : locale === 'vi-VN'
                    ? 'Cần hoàn thành đợt góp'
                    : 'Awaiting your payment'}
                </p>
              </div>
            </div>
          </div>

          {/* Groups Grid - 3 Columns Balanced Cards */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {(contextGroups.length > 0 ? contextGroups : [activeGroup]).map((group) => {
              const { icon: CategoryIcon, iconBg, iconColor } = getCategoryMeta(group.type);
              return (
                <div
                  key={group.id}
                  className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-sm flex flex-col justify-between space-y-5 hover:border-emerald-500/30 transition-all group"
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
                          <h4 className="text-sm font-bold text-slate-900 dark:text-white leading-snug group-hover:text-emerald-600 transition-colors">
                            {group.name}
                          </h4>
                          <span className="text-[11px] text-slate-400">{group.type || 'Nhóm chung'}</span>
                        </div>
                      </div>

                      <span className="px-2.5 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 text-[10px] font-bold">
                        {group.members.length} {locale === 'vi-VN' ? 'thành viên' : 'members'}
                      </span>
                    </div>

                    {/* Member Avatars */}
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
                      </div>
                      <span className="text-[11px] text-slate-400 pl-2 truncate max-w-[180px]">
                        {group.members.map((m) => m.name).join(', ')}
                      </span>
                    </div>

                    {/* Funds & Status */}
                    <div className="space-y-2 text-xs bg-slate-50/70 dark:bg-slate-800/40 p-3.5 rounded-2xl border border-slate-100 dark:border-slate-800">
                      <div className="flex justify-between items-center">
                        <span className="text-slate-500">{t('groups.groupFundBalance')}:</span>
                        <span className="font-bold text-slate-900 dark:text-white tabular-nums">
                          {isHidden ? '••••••••' : formatMoney(group.sharedFund?.balance || 0, locale)}
                        </span>
                      </div>

                      <div className="flex justify-between items-center pt-1 border-t border-slate-200/60 dark:border-slate-700/60">
                        <span className="text-slate-500">{locale === 'vi-VN' ? 'Trạng thái quỹ:' : 'Status:'}</span>
                        <span className="text-emerald-600 dark:text-emerald-400 font-semibold text-[11px]">
                          {locale === 'vi-VN' ? 'Đang hoạt động tốt' : 'Healthy'}
                        </span>
                      </div>
                    </div>
                  </div>

                  <div className="pt-1">
                    <Button
                      size="sm"
                      onClick={() => {
                        setSelectedGroupId(group.id);
                        setViewMode('detail');
                      }}
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

          {/* Activity Feed */}
          {metrics.recentExpenses.length > 0 && (
            <div className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-sm space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-base font-bold text-slate-800 dark:text-white flex items-center gap-2">
                  <Receipt className="h-4 w-4 text-emerald-500" />
                  <span>{t('groups.recentSharedExpenses')}</span>
                </h3>
                <span className="text-xs font-semibold text-slate-400">
                  {metrics.recentExpenses.length} {locale === 'vi-VN' ? 'hoạt động gần đây' : 'recent events'}
                </span>
              </div>

              <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
                {metrics.recentExpenses.map((exp) => (
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
        </div>
      )}

      {/* VIEW 2: GROUP DETAIL WORKSPACE */}
      {viewMode === 'detail' && (
        <div className="space-y-6">
          {/* Header Card */}
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl p-6 shadow-sm">
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div className="flex items-start gap-4">
                <div className="p-3.5 rounded-2xl bg-emerald-50 dark:bg-emerald-950/80 text-emerald-600 dark:text-emerald-400 shadow-sm border border-emerald-500/20">
                  <Users className="h-7 w-7" />
                </div>

                <div>
                  <div className="flex items-center gap-2.5 flex-wrap">
                    <h1 className="text-xl lg:text-2xl font-black text-slate-900 dark:text-slate-100 tracking-tight">
                      {activeGroup.name}
                    </h1>

                    <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold border bg-purple-50 text-purple-700 dark:bg-purple-950/80 dark:text-purple-300 border-purple-200/80 dark:border-purple-800">
                      <Shield className="h-3 w-3" />
                      {isOwner ? (locale === 'vi-VN' ? 'Chủ nhóm' : 'Owner') : (locale === 'vi-VN' ? 'Thành viên' : 'Member')}
                    </span>
                  </div>

                  <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 max-w-xl">
                    {activeGroup.description || (locale === 'vi-VN' ? 'Quỹ chi tiêu tập thể của nhóm' : 'Group shared expense')}
                  </p>
                </div>
              </div>

              {/* Action Buttons */}
              <div className="flex items-center gap-2.5 flex-wrap">
                <button
                  onClick={() => setIsFundContribModalOpen(true)}
                  className="px-4 py-2.5 rounded-xl border border-emerald-600/30 dark:border-emerald-500/30 text-emerald-700 dark:text-emerald-300 text-xs font-semibold transition-all flex items-center gap-1.5 hover:bg-emerald-50 dark:hover:bg-emerald-950/40"
                >
                  <Wallet className="h-4 w-4" />
                  <span>{locale === 'vi-VN' ? 'Góp quỹ' : 'Contribute'}</span>
                </button>

                <button
                  onClick={() => setIsAddExpenseModalOpen(true)}
                  className="px-4 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-sm transition-all flex items-center gap-1.5"
                >
                  <Plus className="h-4 w-4" />
                  <span>{locale === 'vi-VN' ? 'Thêm khoản chi' : 'Add Expense'}</span>
                </button>
              </div>
            </div>

            {/* Sub-tab navigation */}
            <div className="flex items-center gap-2 mt-6 pt-4 border-t border-slate-100 dark:border-slate-800 overflow-x-auto no-scrollbar">
              {[
                { id: 'overview' as const, label: t('groups.tabOverview'), icon: <PieChart className="h-4 w-4" /> },
                { id: 'fund' as const, label: locale === 'vi-VN' ? 'Quỹ nhóm' : 'Group Fund', icon: <Wallet className="h-4 w-4" /> },
                { id: 'expenses' as const, label: t('groups.tabExpenses'), icon: <Receipt className="h-4 w-4" />, badge: activeGroup.expenses.length },
                { id: 'members' as const, label: t('groups.tabMembers'), icon: <Users className="h-4 w-4" />, badge: activeGroup.members.length },
              ].map((tab) => (
                <button
                  key={tab.id}
                  onClick={() => setActiveTab(tab.id)}
                  className={cn(
                    'flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold transition-all shrink-0',
                    activeTab === tab.id
                      ? 'bg-emerald-600 text-white shadow-xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:bg-slate-50 dark:hover:bg-slate-800'
                  )}
                >
                  {tab.icon}
                  <span>{tab.label}</span>
                  {tab.badge !== undefined && (
                    <span
                      className={cn(
                        'px-1.5 py-0.2 rounded-full text-[10px] font-bold',
                        activeTab === tab.id
                          ? 'bg-white/20 text-white'
                          : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300'
                      )}
                    >
                      {tab.badge}
                    </span>
                  )}
                </button>
              ))}
            </div>
          </div>

          {/* Active Tab Content */}
          <div className="bg-transparent">
            {activeTab === 'overview' && (
              <GroupOverviewTab
                group={activeGroup}
                onOpenFundRound={() => setIsCreateFundRoundOpen(true)}
                onOpenAddExpense={() => setIsAddExpenseModalOpen(true)}
              />
            )}
            {activeTab === 'fund' && (
              <GroupFundTab
                group={activeGroup}
                onOpenContribute={() => setIsFundContribModalOpen(true)}
                onOpenCreateRound={() => setIsCreateFundRoundOpen(true)}
                onSelectRoundForPay={() => {}}
                onRequestFund={() => {}}
                onSelectFundRequestForPay={() => {}}
              />
            )}
            {activeTab === 'expenses' && (
              <GroupExpensesTab
                group={activeGroup}
                onOpenAddExpense={() => setIsAddExpenseModalOpen(true)}
              />
            )}
            {activeTab === 'members' && (
              <GroupMembersTab
                group={activeGroup}
                onOpenInviteModal={() => setIsInviteModalOpen(true)}
                onRemoveMember={() => {}}
              />
            )}
          </div>
        </div>
      )}

      {/* Modals for full interactivity */}
      <CreateGroupModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        onSubmit={async (params) => {
          await createGroup(params);
          setIsCreateModalOpen(false);
        }}
      />
      <InviteMemberModal
        isOpen={isInviteModalOpen}
        onClose={() => setIsInviteModalOpen(false)}
        groupId={activeGroup.id}
      />
      <FundContributionModal
        isOpen={isFundContribModalOpen}
        onClose={() => setIsFundContribModalOpen(false)}
        groupId={activeGroup.id}
      />
      <AddSharedExpenseModal
        isOpen={isAddExpenseModalOpen}
        onClose={() => setIsAddExpenseModalOpen(false)}
        group={activeGroup}
      />
    </div>
  );
};
