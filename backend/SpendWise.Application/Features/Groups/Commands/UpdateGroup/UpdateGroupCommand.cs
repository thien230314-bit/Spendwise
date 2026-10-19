using MediatR;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.Groups.Commands.UpdateGroup;

// Command sửa metadata nhóm sau khi kiểm tra quyền quản lý.

public sealed record UpdateGroupCommand(
    Guid OwnerId,
    Guid GroupId,
    UpdateGroupRequest Request) : IRequest<GroupDto>;
