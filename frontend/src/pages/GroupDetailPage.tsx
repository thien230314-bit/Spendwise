import React, { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useI18n } from '../i18n/I18nContext';
import { useAuth } from '../providers/AuthProvider';
import { useGroup } from '../providers/GroupProvider';
import { GroupFundRound, FundContributionRequest } from '../types/groups';
import {
  Users,
  ArrowLeft,
  Wallet,
  Receipt,
  PieChart,
  Settings,
  Plus,
  Shield,
  Sparkles,
} from 'lucide-react';
import { cn } from '../utils/cn';

// Tab components
import { GroupOverviewTab } from '../features/groups/tabs/GroupOverviewTab';
import { GroupMembersTab } from '../features/groups/tabs/GroupMembersTab';
import { GroupFundTab } from '../features/groups/tabs/GroupFundTab';
import { GroupExpensesTab } from '../features/groups/tabs/GroupExpensesTab';

// Modals
import { InviteMemberModal } from '../features/groups/modals/InviteMemberModal';
import { FundContributionModal } from '../features/groups/modals/FundContributionModal';
import { CreateFundRoundModal } from '../features/groups/modals/CreateFundRoundModal';
import { PayFundRoundModal } from '../features/groups/modals/PayFundRoundModal';
import { FundRequestModal } from '../features/groups/modals/FundRequestModal';
import { AddSharedExpenseModal } from '../features/groups/modals/AddSharedExpenseModal';
import { OwnerSettingsModal } from '../features/groups/modals/OwnerSettingsModal';
import { GroupDeletionBanner } from '../features/groups/components/GroupDeletionBanner';

type GroupTabType = 'overview' | 'fund' | 'expenses' | 'members';

export const GroupDetailPage: React.FC = () => {
  const { groupId } = useParams<{ groupId: string }>();
  const navigate = useNavigate();
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const currentUserId = currentUser?.id;

  const {
    groups,
    inviteMember,
    removeMember,
    createSharedExpense,
    createFundRequest,
    updateGroupInfo,
    transferOwnership,
    closeGroup,
    requestGroupDeletion,
    cancelGroupDeletion,
    voteGroupDeletion,
    finalizeGroupDeletionAndRefund,
  } = useGroup();

  const group = groups.find((g) => g.id === groupId);

  const [activeTab, setActiveTab] = useState<GroupTabType>('overview');

  // Modals state
  const [isInviteModalOpen, setIsInviteModalOpen] = useState(false);
  const [isFundContribModalOpen, setIsFundContribModalOpen] = useState(false);
  const [isCreateFundRoundOpen, setIsCreateFundRoundOpen] = useState(false);
  const [isFundRequestModalOpen, setIsFundRequestModalOpen] = useState(false);
  const [selectedRoundForPay, setSelectedRoundForPay] = useState<GroupFundRound | null>(null);
  const [selectedFundRequestForPay, setSelectedFundRequestForPay] = useState<FundContributionRequest | null>(null);
  const [isAddExpenseModalOpen, setIsAddExpenseModalOpen] = useState(false);
  const [isOwnerSettingsOpen, setIsOwnerSettingsOpen] = useState(false);

  if (!group) {
    return (
      <div className="max-w-6xl mx-auto p-8 text-center space-y-4">
        <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100">
          {locale === 'vi-VN' ? 'Không tìm thấy nhóm' : 'Group Not Found'}
        </h2>
        <button
          onClick={() => navigate('/groups')}
          className="px-4 py-2 bg-emerald-600 text-white rounded-xl text-xs font-semibold"
        >
          {locale === 'vi-VN' ? '← Quay lại danh sách nhóm' : '← Back to Groups'}
        </button>
      </div>
    );
  }

  const isOwner = group.currentUserRole === 'Owner' || group.ownerId === currentUserId;
  const isFrozen =
    group.deletionRequest?.status === 'Pending' || group.deletionRequest?.status === 'Ready';
  const activeRoundsCount = (group.fundRounds || []).filter((r) => r.status === 'Active').length;

  const tabs: { id: GroupTabType; label: string; icon: React.ReactNode; badgeCount?: number }[] = [
    { id: 'overview', label: t('groups.tabOverview'), icon: <PieChart className="h-4 w-4" /> },
    {
      id: 'fund',
      label: locale === 'vi-VN' ? 'Quỹ nhóm' : 'Group Fund',
      icon: <Wallet className="h-4 w-4" />,
      badgeCount: activeRoundsCount > 0 ? activeRoundsCount : undefined,
    },
    { id: 'expenses', label: t('groups.tabExpenses'), icon: <Receipt className="h-4 w-4" />, badgeCount: group.expenses.length },
    { id: 'members', label: t('groups.tabMembers'), icon: <Users className="h-4 w-4" />, badgeCount: group.members.length },
  ];

  return (
    <div className="max-w-6xl mx-auto px-4 py-6 space-y-6">
      {/* Back button & Breadcrumb */}
      <div>
        <button
          onClick={() => navigate('/groups')}
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-500 hover:text-emerald-600 dark:text-slate-400 dark:hover:text-emerald-400 transition-colors"
        >
          <ArrowLeft className="h-4 w-4" />
          <span>{locale === 'vi-VN' ? 'Quay lại danh sách nhóm' : 'Back to Groups'}</span>
        </button>
      </div>

      {/* Workspace Header */}
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl p-6 shadow-sm">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-start gap-4">
            <div className="p-3.5 rounded-2xl bg-emerald-50 dark:bg-emerald-950/80 text-emerald-600 dark:text-emerald-400 shadow-sm border border-emerald-500/20">
              <Users className="h-7 w-7" />
            </div>

            <div>
              <div className="flex items-center gap-2.5 flex-wrap">
                <h1 className="text-xl lg:text-2xl font-black text-slate-900 dark:text-slate-100 tracking-tight">
                  {group.name}
                </h1>

                <span
                  className={cn(
                    'inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold border',
                    isOwner
                      ? 'bg-purple-50 text-purple-700 dark:bg-purple-950/80 dark:text-purple-300 border-purple-200/80 dark:border-purple-800'
                      : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300 border-slate-200/80 dark:border-slate-700'
                  )}
                >
                  <Shield className="h-3 w-3" />
                  {isOwner
                    ? locale === 'vi-VN'
                      ? 'Chủ nhóm'
                      : 'Owner'
                    : locale === 'vi-VN'
                    ? 'Thành viên'
                    : 'Member'}
                </span>
              </div>

              <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 max-w-xl">
                {group.description || (locale === 'vi-VN' ? 'Không có mô tả' : 'No description')}
              </p>
            </div>
          </div>

          {/* Quick Action Buttons */}
          <div className="flex items-center gap-2.5 flex-wrap">
            {isOwner && (
              <button
                id="btn-header-create-fund-round"
                onClick={() => setIsCreateFundRoundOpen(true)}
                disabled={isFrozen}
                title={isFrozen ? t('groups.groupFrozenNotice') : undefined}
                className={cn(
                  'px-4 py-2.5 rounded-xl border border-emerald-600/40 dark:border-emerald-500/40 text-emerald-700 dark:text-emerald-300 text-xs font-semibold transition-all flex items-center gap-1.5 shadow-sm',
                  isFrozen
                    ? 'opacity-40 cursor-not-allowed'
                    : 'hover:bg-emerald-50 dark:hover:bg-emerald-950/40'
                )}
              >
                <Sparkles className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                <span>{locale === 'vi-VN' ? 'Tạo đợt góp quỹ' : 'Fund Round'}</span>
              </button>
            )}

            <button
              id="btn-header-contribute-fund"
              onClick={() => setIsFundContribModalOpen(true)}
              disabled={isFrozen}
              title={isFrozen ? t('groups.groupFrozenNotice') : undefined}
              className={cn(
                'px-4 py-2.5 rounded-xl border border-emerald-600/30 dark:border-emerald-500/30 text-emerald-700 dark:text-emerald-300 text-xs font-semibold transition-all flex items-center gap-1.5',
                isFrozen
                  ? 'opacity-40 cursor-not-allowed'
                  : 'hover:bg-emerald-50 dark:hover:bg-emerald-950/40'
              )}
            >
              <Wallet className="h-4 w-4" />
              <span>{locale === 'vi-VN' ? 'Góp quỹ' : 'Contribute'}</span>
            </button>

            <button
              id="btn-header-add-expense"
              onClick={() => setIsAddExpenseModalOpen(true)}
              disabled={isFrozen}
              title={isFrozen ? t('groups.groupFrozenNotice') : undefined}
              className={cn(
                'px-4 py-2.5 rounded-xl bg-emerald-600 text-white text-xs font-semibold shadow-sm transition-all flex items-center gap-1.5',
                isFrozen
                  ? 'opacity-40 cursor-not-allowed'
                  : 'hover:bg-emerald-700'
              )}
            >
              <Plus className="h-4 w-4" />
              <span>{locale === 'vi-VN' ? 'Thêm khoản chi' : 'Add Expense'}</span>
            </button>

            {isOwner && (
              <button
                id="btn-header-owner-settings"
                onClick={() => setIsOwnerSettingsOpen(true)}
                className="p-2.5 rounded-xl border border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-600 dark:text-slate-300 transition-colors"
                title={t('groups.ownerSettings')}
              >
                <Settings className="h-4 w-4" />
              </button>
            )}
          </div>
        </div>

        {/* Tab Navigation */}
        <div className="flex items-center gap-2 mt-6 pt-4 border-t border-slate-100 dark:border-slate-800 overflow-x-auto no-scrollbar">
          {tabs.map((tab) => (
            <button
              key={tab.id}
              id={`tab-button-${tab.id}`}
              onClick={() => setActiveTab(tab.id)}
              className={cn(
                'px-4 py-2 rounded-xl text-xs font-semibold transition-all flex items-center gap-2 shrink-0',
                activeTab === tab.id
                  ? 'bg-slate-900 text-white dark:bg-slate-100 dark:text-slate-900 shadow-sm'
                  : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'
              )}
            >
              {tab.icon}
              <span>{tab.label}</span>
              {typeof tab.badgeCount === 'number' && (
                <span
                  className={cn(
                    'px-1.5 py-0.2 rounded-full text-[10px] font-bold font-mono',
                    activeTab === tab.id
                      ? 'bg-white/20 text-white dark:bg-slate-900/20 dark:text-slate-900'
                      : 'bg-slate-200 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                  )}
                >
                  {tab.badgeCount}
                </span>
              )}
            </button>
          ))}
        </div>
      </div>

      {/* Group Deletion & Read-Only Voting Banner */}
      <GroupDeletionBanner
        group={group}
        currentUserId={currentUserId}
        isOwner={isOwner}
        onVote={(decision) => voteGroupDeletion(group.id, decision)}
        onCancel={() => cancelGroupDeletion(group.id)}
        onFinalize={async () => {
          const res = await finalizeGroupDeletionAndRefund(group.id);
          if (res.success) {
            navigate('/groups');
          } else {
            throw new Error(
              res.error ||
                (locale === 'vi-VN'
                  ? 'Lỗi khi hoàn quỹ và xóa nhóm'
                  : 'Error refunding fund and deleting group')
            );
          }
        }}
      />

      {/* Tab Content Body */}
      <div>
        {activeTab === 'overview' && (
          <GroupOverviewTab
            group={group}
            onOpenContributeFund={() => setIsFundContribModalOpen(true)}
            onOpenAddExpense={() => setIsAddExpenseModalOpen(true)}
            onOpenCreateFundRound={() => setIsCreateFundRoundOpen(true)}
            onSwitchTab={(t) => setActiveTab(t)}
          />
        )}

        {activeTab === 'fund' && (
          <GroupFundTab
            group={group}
            onOpenContribute={() => setIsFundContribModalOpen(true)}
            onOpenCreateRound={() => setIsCreateFundRoundOpen(true)}
            onPayRound={(round) => setSelectedRoundForPay(round)}
            onOpenRequest={() => setIsFundRequestModalOpen(true)}
            onPayRequest={(req) => setSelectedFundRequestForPay(req)}
            onOpenSettings={() => setIsOwnerSettingsOpen(true)}
          />
        )}

        {activeTab === 'expenses' && (
          <GroupExpensesTab
            group={group}
            onOpenAddExpense={() => setIsAddExpenseModalOpen(true)}
          />
        )}

        {activeTab === 'members' && (
          <GroupMembersTab
            group={group}
            onOpenInvite={() => setIsInviteModalOpen(true)}
            onOpenSettings={() => setIsOwnerSettingsOpen(true)}
            onRemoveMember={(memberId) => removeMember(group.id, memberId)}
          />
        )}
      </div>

      {/* Modals */}
      {isInviteModalOpen && (
        <InviteMemberModal
          isOpen={isInviteModalOpen}
          onClose={() => setIsInviteModalOpen(false)}
          onInvite={(identifier) => inviteMember(group.id, identifier)}
          existingMembers={group.members}
        />
      )}

      {isCreateFundRoundOpen && (
        <CreateFundRoundModal
          isOpen={isCreateFundRoundOpen}
          onClose={() => setIsCreateFundRoundOpen(false)}
          group={group}
        />
      )}

      {selectedRoundForPay && (
        <PayFundRoundModal
          isOpen={!!selectedRoundForPay}
          onClose={() => setSelectedRoundForPay(null)}
          group={group}
          round={selectedRoundForPay}
        />
      )}

      {isFundRequestModalOpen && (
        <FundRequestModal
          isOpen={isFundRequestModalOpen}
          onClose={() => setIsFundRequestModalOpen(false)}
          group={group}
          onRequest={createFundRequest}
        />
      )}

      {selectedFundRequestForPay && (
        <FundContributionModal
          isOpen={!!selectedFundRequestForPay}
          onClose={() => setSelectedFundRequestForPay(null)}
          group={group}
          fundRequest={selectedFundRequestForPay}
          onOpenSettings={() => setIsOwnerSettingsOpen(true)}
        />
      )}

      {isFundContribModalOpen && (
        <FundContributionModal
          isOpen={isFundContribModalOpen}
          onClose={() => setIsFundContribModalOpen(false)}
          group={group}
          onOpenSettings={() => setIsOwnerSettingsOpen(true)}
        />
      )}

      {isAddExpenseModalOpen && (
        <AddSharedExpenseModal
          isOpen={isAddExpenseModalOpen}
          onClose={() => setIsAddExpenseModalOpen(false)}
          group={group}
          onAddExpense={createSharedExpense}
        />
      )}

      {isOwnerSettingsOpen && (
        <OwnerSettingsModal
          isOpen={isOwnerSettingsOpen}
          onClose={() => setIsOwnerSettingsOpen(false)}
          group={group}
          onUpdateInfo={updateGroupInfo}
          onTransferOwnership={transferOwnership}
          onRequestDeletion={requestGroupDeletion}
          onCancelDeletion={cancelGroupDeletion}
          onFinalizeDeletion={async (gid) => {
            const res = await finalizeGroupDeletionAndRefund(gid);
            if (res.success) {
              setIsOwnerSettingsOpen(false);
              navigate('/groups');
            }
            return res;
          }}
          onCloseGroup={(gid) => {
            return requestGroupDeletion(gid);
          }}
        />
      )}
    </div>
  );
};
