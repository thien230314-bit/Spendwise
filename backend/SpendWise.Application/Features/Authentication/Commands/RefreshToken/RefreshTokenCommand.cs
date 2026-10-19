using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.RefreshToken;

// Command xoay vòng refresh token hợp lệ và thu hồi token cũ.

public sealed record RefreshTokenCommand(
    RefreshRequest Request,
    string? IpAddress,
    string? UserAgent) : IRequest<AuthResponse>;
