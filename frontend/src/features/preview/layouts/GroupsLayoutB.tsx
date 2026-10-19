import React, { useState, useMemo } from 'react';
import {
  Users,
  Plus,
  Receipt,
  ChevronRight,
  DollarSign,
  PieChart,
  Wallet,
  Shield,
  ArrowRight,
  TrendingUp,
  Building,
  Palmtree,
  HeartHandshake,
  Coffee,
  Calendar,
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

/**
 * GROUPS LAYOUT B — EFFICIENT WORKSPACE (High Density / Quick Workflow)
 * Triết lý bố cục:
 * 1. Danh sách nhóm (Group List View):
 *    - Tối ưu 2 cột chính (8 cols & 4 cols):
 *      + Cột trái (8 cols): Danh sách thẻ nhóm dạng ngang rút gọn (Row/Card hybrid), hiển thị ngay số dư quỹ, tiến độ đợt góp và các thao tác nhanh.
 *      + Cột phải (4 cols): Widget tổng hợp quỹ tích lũy đa nhóm + Bảng hoạt động gần nhất (Activity stream) ghim cố định giúp quản trị viên không bị cuộn dài.
 * 2. Chi tiết nhóm (Group Detail View):
 *    - Bố cục 2 cột song song (8 cols & 4 cols):
 *      + Cột chính (8 cols): Nội dung tab làm việc (Chi tiết các khoản chi, đợt đóng góp quỹ, phân bổ dòng tiền).
 *      + Cột bên phải (4 cols): Thẻ hồ sơ nhóm, số dư quỹ khả dụng, danh sách thành viên trực diện và nút đóng góp nhanh.
 */
export const GroupsLayoutB: React.FC = () => {
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();
  const { groups: contextGroups, createGroup } = useGroup();
  const { currentUser } = useAuth();
  const currentUserId = currentUser?.id;

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

  // Fallback demo group if context groups is empty
  const activeGroup = useMemo(() => {
    if (selectedGroupId) {
      const found = contextGroups.find((g) => g.id === selectedGroupId);
      if (found) return found;
    }
    if (contextGroups.length > 0) return contextGroups[0];

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

  const metrics = useMemo(() => {
    const list = contextGroups.length > 0 ? contextGroups : [activeGroup];
    let totalFund = 0;
    let activeRounds = 0;
    let pendingMe = 0;
    const expenses: { id: string; groupName: string; title: string; payer: string; amount: number; time: string }[] = [];

    list.forEach((g) => {
      totalFund += g.sharedFund?.balance || 0;
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
      totalFund,
      activeRounds,
      pendingMe,
      recentExpenses: expenses.slice(0, 6),
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
              {locale === 'vi-VN' ? '1. Danh sách nhóm (2-Col Efficient)' : '1. Group List (2-Col)'}
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
              {locale === 'vi-VN' ? '2. Chi tiết nhóm (Split Sidebar)' : '2. Group Detail (Split)'}
            </button>
          </div>
        </div>

        <div className="text-xs text-slate-500 dark:text-slate-400 flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-emerald-500 inline-block animate-pulse" />
          <span>Layout B: Bố cục tinh gọn (Efficient Workspace) — Mật độ cao & Thao tác tức thì</span>
        </div>
      </div>

      {/* VIEW 1: GROUP LIST VIEW (2 COLUMNS: 8 / 4) */}
      {viewMode === 'list' && (
        <div className="space-y-6">
          {/* Header */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <h2 className="text-xl sm:text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
                {t('groups.pageTitle')}
              </h2>
              <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-0.5">
                {locale === 'vi-VN'
                  ? 'Quản lý quỹ nhóm và chi tiêu chung tối ưu năng suất'
                  : 'High-efficiency group fund & expense hub'}
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

          {/* 2-Column Grid Layout: 8 cols (Group rows) + 4 cols (Pinned Fund KPI & Quick Activity) */}
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
            {/* Left Column: 8 Columns — Horizontal Streamlined Group Cards */}
            <div className="lg:col-span-8 space-y-4">
              <div className="flex items-center justify-between px-1">
                <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">
                  {locale === 'vi-VN' ? 'Danh sách các nhóm đang tham gia' : 'Active Groups Directory'} ({contextGroups.length > 0 ? contextGroups.length : 1})
                </span>
                <span className="text-xs text-emerald-600 font-semibold">
                  {locale === 'vi-VN' ? 'Sắp xếp theo hoạt động mới' : 'Sorted by recent activity'}
                </span>
              </div>

              {(contextGroups.length > 0 ? contextGroups : [activeGroup]).map((group) => {
                const { icon: CategoryIcon, iconBg, iconColor } = getCategoryMeta(group.type);
                return (
                  <div
                    key={group.id}
                    className="bg-white dark:bg-slate-900 rounded-3xl p-5 border border-slate-100 dark:border-slate-800 shadow-sm hover:border-emerald-500/40 transition-all flex flex-col sm:flex-row sm:items-center justify-between gap-4"
                  >
                    <div className="flex items-start sm:items-center gap-4 min-w-0">
                      <div className={`h-12 w-12 rounded-2xl ${iconBg} ${iconColor} flex items-center justify-center shrink-0`}>
                        <CategoryIcon className="h-6 w-6" />
                      </div>

                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <h4 className="text-sm font-bold text-slate-900 dark:text-white truncate">
                            {group.name}
                          </h4>
                          <span className="px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 text-[10px] font-bold shrink-0">
                            {group.members.length} {locale === 'vi-VN' ? 'thành viên' : 'members'}
                          </span>
                        </div>

                        <p className="text-xs text-slate-400 mt-0.5 truncate max-w-md">
                          {group.description || (locale === 'vi-VN' ? 'Quỹ chi tiêu tập thể' : 'Shared group fund')}
                        </p>

                        <div className="flex items-center gap-3 mt-2 text-xs text-slate-500">
                          <span>
                            {t('groups.groupFundBalance')}:{' '}
                            <strong className="text-slate-900 dark:text-white tabular-nums">
                              {isHidden ? '••••••••' : formatMoney(group.sharedFund?.balance || 0, locale)}
                            </strong>
                          </span>
                        </div>
                      </div>
                    </div>

                    <div className="flex items-center gap-2 shrink-0">
                      <Button
                        size="sm"
                        onClick={() => {
                          setSelectedGroupId(group.id);
                          setViewMode('detail');
                        }}
                        className="gap-1 text-xs"
                      >
                        <span>{t('groups.details')}</span>
                        <ChevronRight className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Right Column: 4 Columns — Sticky Summary Card & Activity Feed */}
            <div className="lg:col-span-4 space-y-6">
              {/* Fund Master Summary */}
              <div className="bg-white dark:bg-[#0F172A] rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-sm space-y-4">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                    {locale === 'vi-VN' ? 'Tổng quỹ tích lũy' : 'Total Group Funds'}
                  </span>
                  <div className="p-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 text-emerald-600">
                    <Wallet className="h-4 w-4" />
                  </div>
                </div>

                <div>
                  <p className="text-2xl font-black text-emerald-600 dark:text-emerald-400 tabular-nums">
                    {isHidden ? '••••••••' : formatMoney(metrics.totalFund, locale)}
                  </p>
                  <p className="text-xs text-slate-400 mt-1">
                    {metrics.totalGroups} {locale === 'vi-VN' ? 'nhóm đang đồng quản lý' : 'groups co-managed'}
                  </p>
                </div>

                <div className="pt-3 border-t border-slate-100 dark:border-slate-800 space-y-2 text-xs">
                  <div className="flex justify-between items-center">
                    <span className="text-slate-500">{locale === 'vi-VN' ? 'Đợt góp đang chạy:' : 'Active Rounds:'}</span>
                    <span className="font-bold text-slate-800 dark:text-slate-200">{metrics.activeRounds}</span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-slate-500">{locale === 'vi-VN' ? 'Phần bạn cần đóng:' : 'Your Pending:'}</span>
                    <span className={cn('font-bold', metrics.pendingMe > 0 ? 'text-indigo-600' : 'text-emerald-600')}>
                      {metrics.pendingMe === 0
                        ? (locale === 'vi-VN' ? 'Đã hoàn tất' : 'Settled')
                        : formatMoney(metrics.pendingMe, locale)}
                    </span>
                  </div>
                </div>
              </div>

              {/* Compact Recent Activity Feed */}
              {metrics.recentExpenses.length > 0 && (
                <div className="bg-white dark:bg-slate-900 rounded-3xl p-5 border border-slate-100 dark:border-slate-800 shadow-sm space-y-3">
                  <div className="flex items-center justify-between">
                    <h3 className="text-xs font-bold text-slate-800 dark:text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
                      <Receipt className="h-3.5 w-3.5 text-emerald-500" />
                      <span>{t('groups.recentSharedExpenses')}</span>
                    </h3>
                  </div>

                  <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
                    {metrics.recentExpenses.map((exp) => (
                      <div key={exp.id} className="py-2.5 flex items-center justify-between text-xs">
                        <div className="min-w-0 pr-2">
                          <p className="font-semibold text-slate-800 dark:text-slate-200 truncate">
                            {exp.title}
                          </p>
                          <span className="text-[10px] text-slate-400">
                            {exp.groupName} • {exp.time}
                          </span>
                        </div>
                        <span className="font-bold text-slate-900 dark:text-white tabular-nums shrink-0">
                          {formatMoney(exp.amount, locale)}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* VIEW 2: GROUP DETAIL WORKSPACE (SPLIT 8 / 4 LAYOUT) */}
      {viewMode === 'detail' && (
        <div className="space-y-6">
          {/* Header Card */}
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl p-5 shadow-sm flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="p-3 rounded-2xl bg-emerald-50 dark:bg-emerald-950/80 text-emerald-600 dark:text-emerald-400">
                <Users className="h-6 w-6" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h1 className="text-xl font-black text-slate-900 dark:text-slate-100">
                    {activeGroup.name}
                  </h1>
                  <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-purple-50 text-purple-700 dark:bg-purple-950/80 dark:text-purple-300">
                    {isOwner ? 'Owner' : 'Member'}
                  </span>
                </div>
                <p className="text-xs text-slate-400 mt-0.5">{activeGroup.description || 'Chi tiêu nhóm'}</p>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={() => setIsFundContribModalOpen(true)}
                className="px-3.5 py-2 rounded-xl border border-emerald-600/30 text-emerald-700 dark:text-emerald-300 text-xs font-semibold hover:bg-emerald-50 dark:hover:bg-emerald-950/40"
              >
                {locale === 'vi-VN' ? 'Góp quỹ' : 'Contribute'}
              </button>
              <button
                onClick={() => setIsAddExpenseModalOpen(true)}
                className="px-3.5 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold"
              >
                {locale === 'vi-VN' ? '+ Khoản chi' : '+ Expense'}
              </button>
            </div>
          </div>

          {/* 2-Column Split: 8 cols (Active Tab Work area) + 4 cols (Group Ledger & Members Sidebar) */}
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
            <div className="lg:col-span-8 space-y-4">
              {/* Sub-tab pills */}
              <div className="flex items-center gap-2 border-b border-slate-200 dark:border-slate-800 pb-2">
                {[
                  { id: 'overview' as const, label: t('groups.tabOverview') },
                  { id: 'fund' as const, label: locale === 'vi-VN' ? 'Quỹ nhóm' : 'Fund' },
                  { id: 'expenses' as const, label: t('groups.tabExpenses') },
                  { id: 'members' as const, label: t('groups.tabMembers') },
                ].map((tab) => (
                  <button
                    key={tab.id}
                    onClick={() => setActiveTab(tab.id)}
                    className={cn(
                      'px-3.5 py-1.5 rounded-xl text-xs font-semibold transition-all',
                      activeTab === tab.id
                        ? 'bg-emerald-600 text-white'
                        : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'
                    )}
                  >
                    {tab.label}
                  </button>
                ))}
              </div>

              {/* Tab Content */}
              <div>
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

            {/* Right Column: 4 Cols Group Ledger & Fast Member Roster */}
            <div className="lg:col-span-4 space-y-5">
              <div className="bg-white dark:bg-slate-900 rounded-3xl p-5 border border-slate-100 dark:border-slate-800 shadow-sm space-y-4">
                <h4 className="text-xs font-bold uppercase tracking-wider text-slate-500">
                  {locale === 'vi-VN' ? 'Thông số quỹ nhóm' : 'Fund Overview'}
                </h4>
                <div>
                  <span className="text-xs text-slate-400">{t('groups.groupFundBalance')}</span>
                  <p className="text-xl font-bold text-slate-900 dark:text-white tabular-nums">
                    {isHidden ? '••••••••' : formatMoney(activeGroup.sharedFund?.balance || 0, locale)}
                  </p>
                </div>
                <div className="pt-2 border-t border-slate-100 dark:border-slate-800 text-xs space-y-2">
                  <div className="flex justify-between">
                    <span className="text-slate-500">Mục tiêu quỹ:</span>
                    <span className="font-semibold text-slate-800 dark:text-slate-200">
                      {formatMoney(activeGroup.sharedFund?.targetAmount || 0, locale)}
                    </span>
                  </div>
                </div>
              </div>

              {/* Members Quick List */}
              <div className="bg-white dark:bg-slate-900 rounded-3xl p-5 border border-slate-100 dark:border-slate-800 shadow-sm space-y-3">
                <div className="flex items-center justify-between">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-slate-500">
                    {t('groups.tabMembers')} ({activeGroup.members.length})
                  </h4>
                  <button
                    onClick={() => setIsInviteModalOpen(true)}
                    className="text-xs font-semibold text-emerald-600 hover:underline"
                  >
                    + Mời
                  </button>
                </div>

                <div className="divide-y divide-slate-100 dark:divide-slate-800">
                  {activeGroup.members.map((m) => (
                    <div key={m.id} className="py-2 flex items-center justify-between text-xs">
                      <div className="flex items-center gap-2">
                        <div className="h-6 w-6 rounded-full bg-slate-200 dark:bg-slate-700 text-[10px] font-bold flex items-center justify-center">
                          {m.avatarInitials || m.name.slice(0, 1)}
                        </div>
                        <span className="font-medium text-slate-800 dark:text-slate-200">{m.name}</span>
                      </div>
                      <span className="text-[10px] text-slate-400">{m.role}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modals */}
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
