using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Groups;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups;

// Lớp này kiểm tra membership và vai trò Owner trước khi Handler truy cập dữ liệu Group.

public sealed class GroupAccess(ISpendWiseDbContext db)
{
    public IQueryable<GroupDto> ProjectGroupsForUser(Guid currentUserId)
    {
        // Một projection lấy nhóm, vai trò hiện tại và thành viên mà không tạo query N+1.
        return db.GroupMembers
            .AsNoTracking()
            .Where(membership => membership.UserId == currentUserId
                && membership.Group.Status != GroupStatus.Closed)
            .Select(membership => new GroupDto(
                membership.Group.Id,
                membership.Group.Name,
                membership.Group.Description,
                membership.Group.Type.ToString(),
                membership.Group.Status.ToString(),
                membership.Group.MembersCanCreateExpenses,
                membership.Role.ToString(),
                membership.Group.Members
                    .OrderBy(member => member.CreatedAt)
                    .Select(member => new GroupMemberDto(
                        member.UserId,
                        member.User.FullName,
                        member.User.SpendId,
                        member.Role.ToString(),
                        member.JoinedAt))
                    .ToList(),
                membership.Group.Currency,
                membership.Group.Members.Where(member => member.Role == GroupRole.Owner)
                    .Select(member => (Guid?)member.UserId).SingleOrDefault(),
                db.GroupReceivingBanks.Where(bank => bank.GroupId == membership.GroupId)
                    .Select(bank => new GroupReceivingBankDto(bank.Id, bank.OwnerUserId, bank.BankConnectionId,
                        bank.BankBrandSnapshot, bank.MaskedAccountSnapshot, bank.AccountHolderSnapshot))
                    .SingleOrDefault()));
    }

    public async Task<GroupMember> RequireMemberAsync(
        Guid userId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        // Kiểm tra người dùng là thành viên trước khi truy cập dữ liệu nhóm.
        return await db.GroupMembers.SingleOrDefaultAsync(
            member => member.GroupId == groupId
                && member.UserId == userId
                && member.User.SystemRole == SpendWise.Domain.Identity.SystemRole.User
                && member.User.Status == SpendWise.Domain.Identity.UserStatus.Active
                && (member.Group.Status == GroupStatus.Active
                    || member.Group.Status == GroupStatus.DeletionPending),
            cancellationToken)
            ?? throw new ForbiddenException(
                "group_membership_required",
                "Active group membership is required.");
    }

    public async Task<GroupMember> RequireOwnerAsync(
        Guid userId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        // Quyền Owner chỉ áp dụng cho tài nguyên nhóm, không mở quyền xem ví cá nhân thành viên.
        var member = await RequireMemberAsync(userId, groupId, cancellationToken);
        if (member.Role != GroupRole.Owner)
        {
            throw new ForbiddenException(
                "group_owner_required",
                "Group owner permission is required.");
        }

        return member;
    }

    public async Task EnsureMutableAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var frozen = await db.GroupDeletionRequests.AsNoTracking().AnyAsync(
            item => item.GroupId == groupId
                && (item.Status == GroupDeletionRequestStatus.Pending
                    || item.Status == GroupDeletionRequestStatus.Ready),
            cancellationToken);
        if (frozen)
        {
            throw new ConflictException("group_deletion_frozen", "Group changes are frozen during deletion voting and refunds.");
        }
    }

    public async Task RequireAcceptedFriendAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken)
    {
        var lower = firstUserId.CompareTo(secondUserId) < 0 ? firstUserId : secondUserId;
        var higher = firstUserId.CompareTo(secondUserId) < 0 ? secondUserId : firstUserId;
        if (!await db.Friendships.AsNoTracking().AnyAsync(item => item.LowerUserId == lower
                && item.HigherUserId == higher && item.EndedAt == null, cancellationToken))
        {
            throw new ConflictException("accepted_friend_required", "Only an accepted friend can be invited to a group.");
        }
    }

    public async Task<GroupDto> ToDtoAsync(
        Group group,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        // Chỉ tải hồ sơ thành viên của nhóm; không truy vấn Wallet hay LedgerAccount cá nhân.
        var members = await db.GroupMembers
            .AsNoTracking()
            .Include(member => member.User)
            .Where(member => member.GroupId == group.Id)
            .OrderBy(member => member.CreatedAt)
            .ToListAsync(cancellationToken);
        var currentRole = members.Single(member => member.UserId == currentUserId).Role;
        var ownerId = members.Single(member => member.Role == GroupRole.Owner).UserId;
        var receiving = await db.GroupReceivingBanks.AsNoTracking()
            .Where(item => item.GroupId == group.Id)
            .Select(item => new GroupReceivingBankDto(item.Id, item.OwnerUserId, item.BankConnectionId,
                item.BankBrandSnapshot, item.MaskedAccountSnapshot, item.AccountHolderSnapshot))
            .SingleOrDefaultAsync(cancellationToken);
        var memberDtos = members.Select(member => new GroupMemberDto(
            member.UserId,
            member.User.FullName,
            member.User.SpendId,
            member.Role.ToString(),
            member.JoinedAt)).ToList();

        return new GroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.Type.ToString(),
            group.Status.ToString(),
            group.MembersCanCreateExpenses,
            currentRole.ToString(),
            memberDtos,
            group.Currency,
            ownerId,
            receiving);
    }
}
