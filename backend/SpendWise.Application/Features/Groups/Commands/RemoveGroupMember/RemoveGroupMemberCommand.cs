using MediatR;

namespace SpendWise.Application.Features.Groups.Commands.RemoveGroupMember;

// Command xóa quan hệ thành viên khỏi nhóm chứ không xóa tài khoản người dùng.

public sealed record RemoveGroupMemberCommand(
    Guid OwnerId,
    Guid GroupId,
    Guid MemberUserId) : IRequest;
