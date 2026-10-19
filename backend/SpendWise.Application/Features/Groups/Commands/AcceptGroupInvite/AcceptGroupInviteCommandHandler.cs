using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Groups;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups.Commands.AcceptGroupInvite;

// Handler chấp nhận lời mời hợp lệ và tạo quan hệ thành viên nhóm.

public sealed class AcceptGroupInviteCommandHandler(
    ISpendWiseDbContext db,
    IClock clock,
    GroupAccess access) : IRequestHandler<AcceptGroupInviteCommand, GroupDto>
{
    public async Task<GroupDto> Handle(
        AcceptGroupInviteCommand command,
        CancellationToken cancellationToken)
    {
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await GroupAdmission.LockUsersAsync(db, [command.UserId], token);
            // Kiểm tra token, hạn dùng và người nhận trước khi tạo membership nhóm.
            var invite = await db.GroupInvites.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == command.InviteId
                    && item.InvitedUserId == command.UserId
                    && item.Token == command.Token,
                cancellationToken)
                ?? throw new NotFoundException("invite_not_found", "Invite not found.");
            await GroupFinancialConcurrency.SerializeAsync(db, invite.GroupId, token);
            invite = await db.GroupInvites.AsNoTracking().SingleAsync(item => item.Id == invite.Id, token);
            if (invite.Status != InviteStatus.Pending || invite.ExpiresAt <= clock.UtcNow)
            {
                throw new ConflictException(
                    "invite_unavailable",
                    "Invite is expired or unavailable.");
            }

            await access.EnsureMutableAsync(invite.GroupId, cancellationToken);
            if (!await db.Groups.AsNoTracking().AnyAsync(g => g.Id == invite.GroupId && g.Status == GroupStatus.Active, token))
                throw new ConflictException("group_unavailable", "Group is not active.");
            await GroupAdmission.RequireCapacityAsync(db, command.UserId, token);
            await access.RequireAcceptedFriendAsync(invite.InvitedByUserId, command.UserId, cancellationToken);
            var eligible = await db.Users.AnyAsync(item => item.Id == command.UserId
                && item.Status == SpendWise.Domain.Identity.UserStatus.Active
                && item.SystemRole == SpendWise.Domain.Identity.SystemRole.User, cancellationToken);
            if (!eligible)
            {
                throw new ForbiddenException("group_member_ineligible", "Only an active normal user can join a group.");
            }

            if (await db.GroupMembers.AnyAsync(m => m.GroupId == invite.GroupId && m.UserId == command.UserId, token))
                throw new ConflictException("already_member", "User is already a member.");
            if (!await GroupAdmission.ResolveInviteAsync(db, invite.Id, InviteStatus.Accepted, token))
                throw new ConflictException("invite_unavailable", "Invite is no longer pending.");
            db.GroupMembers.Add(new GroupMember
            {
                GroupId = invite.GroupId,
                UserId = command.UserId,
                Role = GroupRole.Member,
                JoinedAt = clock.UtcNow
            });
            db.GroupActivities.Add(new GroupActivity
            {
                GroupId = invite.GroupId,
                ActorUserId = command.UserId,
                Type = GroupActivityType.MemberJoined,
                DataJson = System.Text.Json.JsonSerializer.Serialize(new { command.UserId })
            });
            await db.SaveChangesAsync(cancellationToken);

            var group = await db.Groups.FindAsync([invite.GroupId], cancellationToken)
                ?? throw new NotFoundException("group_not_found", "Group not found.");
            return await access.ToDtoAsync(group, command.UserId, cancellationToken);
        }, cancellationToken);
    }
}
