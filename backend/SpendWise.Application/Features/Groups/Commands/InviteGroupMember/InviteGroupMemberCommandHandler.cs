using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Groups;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups.Commands.InviteGroupMember;

// Handler mời người dùng vào nhóm sau khi kiểm tra quyền quản lý.

public sealed class InviteGroupMemberCommandHandler(
    ISpendWiseDbContext db,
    IClock clock,
    INotificationService notifications,
    GroupAccess access) : IRequestHandler<InviteGroupMemberCommand, GroupInviteDto>
{
    public async Task<GroupInviteDto> Handle(
        InviteGroupMemberCommand command,
        CancellationToken cancellationToken)
    {
        // Chỉ Owner được mời tài khoản hợp lệ chưa là thành viên của nhóm.
        await access.RequireOwnerAsync(command.OwnerId, command.GroupId, cancellationToken);
        await access.EnsureMutableAsync(command.GroupId, cancellationToken);
        var group = await db.Groups.FindAsync([command.GroupId], cancellationToken)
            ?? throw new NotFoundException("group_not_found", "Group not found.");
        if (!await db.Users.AnyAsync(user => user.Id == command.Request.UserId
                && user.Status == SpendWise.Domain.Identity.UserStatus.Active
                && user.SystemRole == SpendWise.Domain.Identity.SystemRole.User, cancellationToken))
        {
            throw new NotFoundException("user_not_found", "User not found.");
        }
        await access.RequireAcceptedFriendAsync(command.OwnerId, command.Request.UserId, cancellationToken);
        await GroupAdmission.RequireCapacityAsync(db, command.Request.UserId, cancellationToken);
        if (await db.GroupMembers.AnyAsync(
            member => member.GroupId == command.GroupId
                && member.UserId == command.Request.UserId,
            cancellationToken))
        {
            throw new ConflictException("already_member", "User is already a member.");
        }

        var existing = await db.GroupInvites.SingleOrDefaultAsync(item => item.GroupId == command.GroupId
            && item.InvitedUserId == command.Request.UserId && item.Status == InviteStatus.Pending, cancellationToken);
        if (existing is not null)
        {
            return new GroupInviteDto(existing.Id, command.GroupId, group.Name, existing.InvitedUserId,
                existing.Status.ToString(), existing.ExpiresAt, existing.Token);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var invite = new GroupInvite
        {
            GroupId = command.GroupId,
            InvitedUserId = command.Request.UserId,
            InvitedByUserId = command.OwnerId,
            Token = token,
            ExpiresAt = clock.UtcNow.AddDays(7)
        };
        db.GroupInvites.Add(invite);
        db.GroupActivities.Add(new GroupActivity
        {
            GroupId = command.GroupId,
            ActorUserId = command.OwnerId,
            Type = GroupActivityType.MemberInvited,
            DataJson = System.Text.Json.JsonSerializer.Serialize(new { invite.InvitedUserId })
        });
        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateAsync(
            invite.InvitedUserId,
            "GroupInvite",
            "Group invitation",
            $"You were invited to join {group.Name}.",
            new { invite.Id, invite.GroupId, invite.Token, invite.ExpiresAt },
            cancellationToken);

        return new GroupInviteDto(
            invite.Id,
            command.GroupId,
            group.Name,
            invite.InvitedUserId,
            invite.Status.ToString(),
            invite.ExpiresAt,
            token);
    }
}
