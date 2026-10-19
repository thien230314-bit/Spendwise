using MediatR;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.Groups.Commands.AcceptGroupInvite;

// Command chấp nhận lời mời hợp lệ và tạo quan hệ thành viên nhóm.

public sealed record AcceptGroupInviteCommand(
    Guid UserId,
    Guid InviteId,
    string Token) : IRequest<GroupDto>;
