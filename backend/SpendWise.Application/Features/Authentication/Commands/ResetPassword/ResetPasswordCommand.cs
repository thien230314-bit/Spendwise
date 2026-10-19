using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.ResetPassword;

// Command dùng OTP RESET_PASSWORD single-use để đặt mật khẩu mới và thu hồi credential cũ.
public sealed record ResetPasswordCommand(
    ResetPasswordRequest Request) : IRequest;
