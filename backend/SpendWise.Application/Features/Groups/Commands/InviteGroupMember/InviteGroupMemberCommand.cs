using MediatR;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.Groups.Commands.InviteGroupMember;

// Command mời người dùng vào nhóm sau khi kiểm tra quyền quản lý.

public sealed record InviteGroupMemberCommand(
    Guid OwnerId,
    Guid GroupId,
    InviteGroupRequest Request) : IRequest<GroupInviteDto>;
