using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups.Commands.RemoveGroupMember;

// Handler xóa quan hệ thành viên khỏi nhóm chứ không xóa tài khoản người dùng.

public sealed class RemoveGroupMemberCommandHandler(
    ISpendWiseDbContext db,
    GroupAccess access) : IRequestHandler<RemoveGroupMemberCommand>
{
    public async Task Handle(
        RemoveGroupMemberCommand command,
        CancellationToken cancellationToken)
    {
        // Chỉ xóa membership khi thành viên không còn nghĩa vụ nợ chưa giải quyết.
        // Chỉ Owner của nhóm mới được xóa quan hệ thành viên.
        await access.RequireOwnerAsync(command.OwnerId, command.GroupId, cancellationToken);
        await access.EnsureMutableAsync(command.GroupId, cancellationToken);
        if (command.OwnerId == command.MemberUserId)
        {
            throw new AppException(
                "owner_removal",
                "Transfer ownership before leaving the group.");
        }

        var member = await db.GroupMembers.SingleOrDefaultAsync(
            item => item.GroupId == command.GroupId
                && item.UserId == command.MemberUserId,
            cancellationToken)
            ?? throw new NotFoundException("member_not_found", "Member not found.");
        // Chỉ các nghĩa vụ quỹ hiện hành mới chặn xóa thành viên; Debt cũ đã được nghỉ hưu.
        var hasRequestObligation = await db.FundRequestRecipients.AnyAsync(item => item.UserId == command.MemberUserId
            && item.Status == FundRequestRecipientStatus.Pending
            && item.FundRequest.GroupId == command.GroupId, cancellationToken);
        var hasRoundObligation = await db.GroupFundRoundMembers.AnyAsync(item => item.UserId == command.MemberUserId
            && item.Round.GroupId == command.GroupId
            && item.Round.Status == GroupFundRoundStatus.Active
            && item.Status != GroupFundRoundMemberStatus.Paid, cancellationToken);
        var hasRemainingShare = await db.GroupFundShares.AnyAsync(item => item.GroupId == command.GroupId
            && item.UserId == command.MemberUserId && item.RemainingAmount > 0, cancellationToken);
        var hasObligations = hasRequestObligation || hasRoundObligation || hasRemainingShare;
        if (hasObligations)
        {
            throw new ConflictException(
                "member_has_obligations",
                "Member has unresolved financial obligations.");
        }

        // RemoveMember chỉ xóa quan hệ nhóm, tuyệt đối không xóa tài khoản User.
        db.GroupMembers.Remove(member);
        db.GroupActivities.Add(new GroupActivity
        {
            GroupId = command.GroupId,
            ActorUserId = command.OwnerId,
            Type = GroupActivityType.MemberRemoved,
            DataJson = System.Text.Json.JsonSerializer.Serialize(new { command.MemberUserId })
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
