using MediatR;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.Groups.Commands.CreateGroup;

// Command tạo nhóm mới và gán người dùng hiện tại làm chủ sở hữu.

public sealed record CreateGroupCommand(
    Guid UserId,
    CreateGroupRequest Request) : IRequest<GroupDto>;
