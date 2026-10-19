import React, { useState } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { Group, GroupMember } from '../../../types/groups';
import {
  Users,
  UserPlus,
  Shield,
  UserCheck,
  MoreVertical,
  Trash2,
  Settings,
  Mail,
  Phone,
  Clock,
  AlertCircle,
  CheckCircle,
} from 'lucide-react';
import { cn } from '../../../utils/cn';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';

interface GroupMembersTabProps {
  group: Group;
  onOpenInvite: () => void;
  onOpenSettings: () => void;
  onRemoveMember: (memberId: string) => { success: boolean; error?: string };
}

export const GroupMembersTab: React.FC<GroupMembersTabProps> = ({
  group,
  onOpenInvite,
  onOpenSettings,
  onRemoveMember,
}) => {
  const { t, formatCurrency, locale } = useI18n();
  const { isBalanceHidden, maskValue } = useBalanceVisibility();
  const { currentUser } = useAuth();
  const currentUserId = currentUser?.id;

  const [selectedMember, setSelectedMember] = useState<GroupMember | null>(null);
  const [deleteConfirmMember, setDeleteConfirmMember] = useState<GroupMember | null>(null);
  const [actionError, setActionError] = useState('');

  const isCurrentOwner = group.currentUserRole === 'Owner';

  const handleConfirmRemove = () => {
    if (!deleteConfirmMember) return;
    const res = onRemoveMember(deleteConfirmMember.id);
    if (!res.success) {
      setActionError(getLocalizedErrorMessage(res.error, locale, t));
    } else {
      setDeleteConfirmMember(null);
      setActionError('');
    }
  };

  return (
    <div className="space-y-6 animate-fadeIn">
      {/* Header with Member Count & Owner Actions */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h3 className="text-base font-bold text-slate-900 dark:text-slate-100 flex items-center gap-2">
            <Users className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
            <span>
              {locale === 'vi-VN' ? 'Danh sách thành viên' : 'Group Members'} ({group.members.length})
            </span>
          </h3>
          <p className="text-xs text-slate-500 dark:text-slate-400">
            {locale === 'vi-VN'
              ? 'Thành viên cùng tham gia chia sẻ chi phí và đồng quản lý quỹ.'
              : 'Members collaborating in sharing expenses and managing fund.'}
          </p>
        </div>

        <div className="flex items-center gap-2">
          {isCurrentOwner && (
            <button
              onClick={onOpenSettings}
              className="px-3.5 py-2 rounded-xl border border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 text-xs font-semibold transition-all flex items-center gap-1.5"
            >
              <Settings className="h-4 w-4" />
              <span>{t('groups.ownerSettings')}</span>
            </button>
          )}

          <button
            onClick={onOpenInvite}
            className="px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-sm transition-all flex items-center gap-1.5"
          >
            <UserPlus className="h-4 w-4" />
            <span>{t('groups.inviteMemberBtn')}</span>
          </button>
        </div>
      </div>

      {actionError && (
        <div className="p-3.5 text-xs text-rose-700 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 rounded-xl flex items-center gap-2">
          <AlertCircle className="h-4 w-4 shrink-0" />
          <span>{actionError}</span>
        </div>
      )}

      {/* Members Grid / List */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3.5">
        {group.members.map((member) => {
          const isOwner = Boolean(
            (member.userId && member.userId === group.ownerId) ||
              member.groupRole === 'Owner'
          );
          const isCurrentUser = Boolean(
            currentUserId && member.userId === currentUserId
          );

          // Active Fund Rounds for this member in this group
          const activeRounds = (group.fundRounds || []).filter((r) => r.status === 'Active');
          const memberActiveRounds = activeRounds
            .map((r) => ({
              round: r,
              membership: r.members.find((m) => m.userId === member.userId),
            }))
            .filter((item) => Boolean(item.membership));

          const memberPendingRounds = memberActiveRounds.filter(
            (item) =>
              item.membership &&
              item.membership.status !== 'Paid' &&
              item.membership.requiredAmount - item.membership.paidAmount > 0
          );
          const memberPendingTotal = memberPendingRounds.reduce(
            (sum, item) =>
              sum +
              Math.max(
                0,
                (item.membership?.requiredAmount || 0) - (item.membership?.paidAmount || 0)
              ),
            0
          );

          return (
            <div
              key={member.id}
              className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-4.5 flex flex-col justify-between hover:border-slate-300 dark:hover:border-slate-700 transition-all"
            >
              <div>
                {/* Top Row: Avatar, Name, Role */}
                <div className="flex items-start justify-between gap-3 mb-3">
                  <div className="flex items-center gap-3">
                    <div
                      className={cn(
                        'h-10 w-10 rounded-full flex items-center justify-center font-bold text-sm text-white',
                        isCurrentUser
                          ? 'bg-emerald-600'
                          : isOwner
                          ? 'bg-purple-600'
                          : 'bg-slate-600 dark:bg-slate-700'
                      )}
                    >
                      {member.avatarInitials}
                    </div>

                    <div>
                      <div className="flex items-center gap-2">
                        <h4 className="font-bold text-slate-900 dark:text-slate-100 text-sm">
                          {member.name}
                        </h4>
                        {isCurrentUser && (
                          <span className="text-[10px] font-bold px-1.5 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300">
                            {locale === 'vi-VN' ? 'Bạn' : 'You'}
                          </span>
                        )}
                      </div>
                      {(member.email || member.phone) && (
                        <span className="text-xs text-slate-400 truncate max-w-[140px] block">
                          {member.email || member.phone}
                        </span>
                      )}
                    </div>
                  </div>

                  <div className="flex items-center gap-1.5">
                    <span
                      className={cn(
                        'inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold border',
                        isOwner
                          ? 'bg-purple-50 text-purple-700 dark:bg-purple-950/70 dark:text-purple-300 border-purple-200/60 dark:border-purple-800/60'
                          : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300 border-slate-200/80 dark:border-slate-700'
                      )}
                    >
                      {isOwner ? <Shield className="h-3 w-3" /> : <UserCheck className="h-3 w-3" />}
                      {isOwner
                        ? locale === 'vi-VN'
                          ? 'Chủ nhóm'
                          : 'Owner'
                        : locale === 'vi-VN'
                        ? 'Thành viên'
                        : 'Member'}
                    </span>

                    {member.status === 'pending_invitation' && (
                      <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-amber-50 text-amber-700 border border-amber-200">
                        {locale === 'vi-VN' ? 'Chờ nhận' : 'Pending'}
                      </span>
                    )}
                  </div>
                </div>

                {/* Contribution / Fund Obligation Status */}
                <div className="grid grid-cols-2 gap-2 p-2.5 bg-slate-50 dark:bg-slate-950/70 rounded-xl border border-slate-100 dark:border-slate-800 text-xs mb-3">
                  <div>
                    <span className="text-slate-400 block text-[10px]">{t('groups.yourContribution')}</span>
                    <span className="font-bold text-slate-900 dark:text-slate-100 font-mono">
                      {isBalanceHidden ? maskValue(member.totalContributed) : formatCurrency(member.totalContributed)}
                    </span>
                  </div>

                  <div>
                    <span className="text-slate-400 block text-[10px]">
                      {locale === 'vi-VN' ? 'Đợt góp đang tham gia' : 'Active Fund Rounds'}
                    </span>
                    <span
                      className={cn(
                        'font-bold font-mono',
                        memberActiveRounds.length > 0 && memberPendingTotal > 0
                          ? 'text-indigo-600 dark:text-indigo-400'
                          : memberActiveRounds.length > 0
                          ? 'text-emerald-600 dark:text-emerald-400'
                          : 'text-slate-500 dark:text-slate-400'
                      )}
                    >
                      {memberActiveRounds.length > 0 && memberPendingTotal > 0
                        ? `${locale === 'vi-VN' ? 'Còn: ' : 'Pending: '}${
                            isBalanceHidden ? maskValue(memberPendingTotal) : formatCurrency(memberPendingTotal)
                          }`
                        : memberActiveRounds.length > 0
                        ? locale === 'vi-VN'
                          ? 'Đã hoàn thành'
                          : 'Completed'
                        : activeRounds.length > 0
                        ? locale === 'vi-VN'
                          ? 'Không tham gia đợt đang mở'
                          : 'Not participating'
                        : locale === 'vi-VN'
                        ? 'Không có đợt'
                        : 'None active'}
                    </span>
                  </div>
                </div>
              </div>

              {/* Card Footer: Joined Date & Owner Remove Action */}
              <div className="pt-2 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs text-slate-400">
                <span>
                  {locale === 'vi-VN' ? 'Tham gia:' : 'Joined:'} {member.joinedDate}
                </span>

                {isCurrentOwner && !isCurrentUser && (
                  <button
                    onClick={() => {
                      setActionError('');
                      setDeleteConfirmMember(member);
                    }}
                    className="text-rose-600 hover:text-rose-700 dark:text-rose-400 font-semibold hover:underline flex items-center gap-1"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                    <span>{t('groups.removeMemberBtn')}</span>
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* Remove Member Confirmation Modal */}
      {deleteConfirmMember && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fadeIn">
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl w-full max-w-md shadow-2xl p-6 space-y-4 animate-scaleUp">
            <div className="flex items-center gap-2.5 text-rose-600">
              <div className="p-2 rounded-xl bg-rose-50 dark:bg-rose-950/80">
                <Trash2 className="h-5 w-5" />
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-slate-100">
                {locale === 'vi-VN' ? 'Xác nhận xóa thành viên' : 'Confirm Remove Member'}
              </h3>
            </div>

            <p className="text-xs text-slate-600 dark:text-slate-400">
              {locale === 'vi-VN'
                ? `Bạn có chắc chắn muốn xóa thành viên "${deleteConfirmMember.name}" khỏi nhóm?`
                : `Are you sure you want to remove "${deleteConfirmMember.name}" from the group?`}
            </p>

            <div className="p-3 bg-slate-50 dark:bg-slate-800/60 rounded-xl border border-slate-200 dark:border-slate-700 text-[11px] text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN'
                ? 'SpendWise sẽ kiểm tra nghĩa vụ góp quỹ đang hoạt động trước khi xóa thành viên này.'
                : 'SpendWise will check active fund obligations before removing this member.'}
            </div>

            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => {
                  setDeleteConfirmMember(null);
                  setActionError('');
                }}
                className="px-4 py-2 text-xs font-medium text-slate-600 dark:text-slate-400 hover:text-slate-900"
              >
                {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
              </button>
              <button
                type="button"
                onClick={handleConfirmRemove}
                className="px-4 py-2 rounded-xl bg-rose-600 hover:bg-rose-700 text-white text-xs font-bold shadow-sm transition-all"
              >
                {locale === 'vi-VN' ? 'Xóa khỏi nhóm' : 'Remove'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
