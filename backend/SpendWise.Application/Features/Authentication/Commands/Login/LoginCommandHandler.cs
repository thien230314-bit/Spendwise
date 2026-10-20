using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.Login;

// Handler xác minh mật khẩu đã băm và phát hành token cho phiên hợp lệ.

public sealed class LoginCommandHandler(
    ISpendWiseDbContext db,
    IPasswordService passwords,
    LoginChallengeSupport challenges,
    AccountCredentialLockout lockout) : IRequestHandler<LoginCommand, LoginChallengeResponse>
{
    public async Task<LoginChallengeResponse> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        // Chuẩn hóa định danh, xác minh mật khẩu đã hash và kiểm tra trạng thái tài khoản.
        // Frontend dùng email hoặc phone; SpendID cũ chỉ được giữ để không phá đăng nhập Admin hiện có.
        var identifier = InputBoundary.RequiredText(command.Request.Identifier, 256, "Identifier");
        if (string.IsNullOrEmpty(command.Request.Password) || command.Request.Password.Length > 128)
        {
            throw new AppException("invalid_credentials", "Invalid credentials.", 401);
        }
        InputBoundary.OptionalText(command.Request.DeviceName, 200, "Device name");
        var normalizedIdentifier = identifier.ToUpperInvariant();
        var isPhone = InputBoundary.TryNormalizePhone(identifier, out var normalizedPhone);
        var user = await db.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == normalizedIdentifier
                || (isPhone && candidate.Phone == normalizedPhone)
                || candidate.NormalizedSpendId == normalizedIdentifier,
            cancellationToken)
            ?? throw new AppException("invalid_credentials", "Invalid credentials.", 401);

        // Tài khoản đang khóa tạm thời vẫn trả cùng lỗi chung để chống dò tài khoản.
        if (await lockout.IsPasswordLockedAsync(user.Id, cancellationToken))
        {
            throw new AppException("invalid_credentials", "Invalid credentials.", 401);
        }

        // Kiểm tra mật khẩu bằng dịch vụ hash, không so sánh plaintext.
        if (!passwords.Verify(command.Request.Password, user.PasswordHash))
        {
            await lockout.RecordPasswordFailureAsync(user.Id, cancellationToken);
            throw new AppException("invalid_credentials", "Invalid credentials.", 401);
        }
        if (await lockout.ResetPasswordAfterSuccessAsync(user.Id, cancellationToken) != 1)
        {
            // Một request đồng thời vừa khóa tài khoản thì password đúng cũng chưa được đi tiếp.
            throw new AppException("invalid_credentials", "Invalid credentials.", 401);
        }

        if (user.Status != UserStatus.Active && user.Status != UserStatus.Restricted)
        {
            throw new AppException(
                "account_unavailable",
                $"Account is {user.Status}.",
                403);
        }

        if (user.SystemRole == SystemRole.SuperAdmin)
        {
            throw new AppException(
                "account_unavailable",
                "SuperAdmin access is disabled in V1.0.",
                403);
        }

        // Password đúng nhưng email chưa xác minh vẫn không được nhận challenge PIN/session.
        if (user.EmailVerifiedAt is null)
        {
            throw new AppException(
                "email_verification_required",
                "Email verification is required before login.",
                403);
        }

        // Password đúng chỉ tạo challenge; JWT/session chỉ được tạo sau bước PIN.
        return await challenges.CreateAsync(
            user,
            command.Request.DeviceName?.Trim() ?? "Browser",
            command.IpAddress,
            command.UserAgent,
            cancellationToken);
    }
}
