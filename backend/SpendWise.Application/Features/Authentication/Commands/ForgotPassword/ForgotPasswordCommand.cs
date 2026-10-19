using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.ForgotPassword;

// Command xếp OTP reset vào outbox nhưng luôn giữ response trung lập chống dò tài khoản.
public sealed record ForgotPasswordCommand(
    ForgotPasswordRequest Request,
    string? IpAddress = null) : IRequest;
