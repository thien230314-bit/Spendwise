using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.Login;

// Command xác minh mật khẩu đã băm và phát hành token cho phiên hợp lệ.

public sealed record LoginCommand(
    LoginRequest Request,
    string? IpAddress,
    string? UserAgent) : IRequest<LoginChallengeResponse>;
