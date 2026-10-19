using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.Register;

// Command tạo tài khoản mới với mật khẩu đã băm và định danh duy nhất.

public sealed record RegisterCommand(
    RegisterRequest Request,
    string? IpAddress,
    string? UserAgent) : IRequest<EmailVerificationPendingResponse>;
