using MediatR;

namespace SpendWise.Application.Features.Users.Commands.RevokeSessions;

// Command thu hồi các phiên đăng nhập cũ của người dùng hiện tại.

public sealed record RevokeSessionsCommand(Guid UserId) : IRequest;
