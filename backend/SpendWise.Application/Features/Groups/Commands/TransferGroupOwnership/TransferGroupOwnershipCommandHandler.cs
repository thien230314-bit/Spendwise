using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups.Commands.TransferGroupOwnership;

// Handler chuyển quyền sở hữu nhóm cho một thành viên hợp lệ.

public sealed class TransferGroupOwnershipCommandHandler(
    ISpendWiseDbContext db,
    GroupAccess access) : IRequestHandler<TransferGroupOwnershipCommand>
{
    public async Task Handle(
        TransferGroupOwnershipCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OwnerId == command.MemberUserId)
        {
            throw new ConflictException(
                "ownership_unchanged",
                "Choose another member as the new group owner.");
        }

        // Hai UPDATE có điều kiện chạy cùng transaction để chỉ một yêu cầu đồng thời thắng.
        await db.ExecuteInTransactionAsync(async token =>
        {
            await access.RequireOwnerAsync(command.OwnerId, command.GroupId, token);
            await access.EnsureMutableAsync(command.GroupId, token);
            var memberExists = await db.GroupMembers
                .AsNoTracking()
                .AnyAsync(
                    item => item.GroupId == command.GroupId
                        && item.UserId == command.MemberUserId
                        && item.Role == GroupRole.Member
                        && item.User.SystemRole == SpendWise.Domain.Identity.SystemRole.User
                        && item.User.Status == SpendWise.Domain.Identity.UserStatus.Active,
                    token);
            if (!memberExists)
            {
                throw new NotFoundException("member_not_found", "Member not found.");
            }

            // Điều kiện Role=Owner trở thành điểm nguyên tử: request thua cuộc đua cập nhật 0 dòng.
            var demotedOwners = await db.GroupMembers
                .Where(item => item.GroupId == command.GroupId
                    && item.UserId == command.OwnerId
                    && item.Role == GroupRole.Owner)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.Role, GroupRole.Member),
                    token);
            if (demotedOwners != 1)
            {
                throw new ConflictException(
                    "group_owner_changed",
                    "Group ownership changed concurrently.");
            }

            var promotedMembers = await db.GroupMembers
                .Where(item => item.GroupId == command.GroupId
                    && item.UserId == command.MemberUserId
                    && item.Role == GroupRole.Member)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.Role, GroupRole.Owner),
                    token);
            if (promotedMembers != 1)
            {
                throw new ConflictException(
                    "group_member_changed",
                    "The selected member changed concurrently.");
            }

            var receivingBank = await db.GroupReceivingBanks.SingleOrDefaultAsync(item => item.GroupId == command.GroupId, token);
            if (receivingBank is not null && receivingBank.OwnerUserId == command.OwnerId)
            {
                db.GroupReceivingBanks.Remove(receivingBank);
                await db.SaveChangesAsync(token);
            }

            return true;
        }, cancellationToken);
    }
}
