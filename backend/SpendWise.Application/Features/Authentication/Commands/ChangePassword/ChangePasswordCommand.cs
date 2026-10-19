using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.ChangePassword;

// Command xác minh mật khẩu cũ rồi băm và lưu mật khẩu mới.

public sealed record ChangePasswordCommand(
    Guid UserId,
    ChangePasswordRequest Request) : IRequest;
