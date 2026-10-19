using MediatR;

namespace SpendWise.Application.Features.Admin.Users.Commands.RevokeUserSessions;

// Command thu hồi phiên của một người dùng theo quyền quản trị.

public sealed record RevokeUserSessionsCommand(
    Guid AdminId,
    Guid UserId,
    string Reason) : IRequest;
