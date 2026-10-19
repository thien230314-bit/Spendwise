using MediatR;

namespace SpendWise.Application.Features.Authentication.Commands.Logout;

// Command thu hồi refresh token để kết thúc phiên đăng nhập hiện tại.

public sealed record LogoutCommand(
    Guid UserId,
    Guid SessionId,
    string? RefreshToken) : IRequest;
