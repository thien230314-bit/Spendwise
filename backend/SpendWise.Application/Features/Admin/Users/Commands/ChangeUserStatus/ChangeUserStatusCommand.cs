using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Users.Commands.ChangeUserStatus;

// Command thay đổi trạng thái tài khoản theo quyền và workflow quản trị.

public sealed record ChangeUserStatusCommand(
    Guid AdminId,
    Guid UserId,
    ChangeUserStatusRequest Request) : IRequest<AdminUserDto>;
