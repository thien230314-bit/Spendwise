using MediatR;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.ChangePassword;

// Handler xác minh mật khẩu cũ rồi băm và lưu mật khẩu mới.

public sealed class ChangePasswordCommandHandler(
    ISpendWiseDbContext db,
    IPasswordService passwords,
    AuthenticationSupport authentication) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(
        ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        // Kiểm tra input mật khẩu hiện tại trước transaction thay đổi credential.
        if (string.IsNullOrEmpty(command.Request.CurrentPassword)
            || command.Request.CurrentPassword.Length > 128)
        {
            throw new AppException("invalid_password", "Current password is incorrect.", 400);
        }
        AuthenticationSupport.ValidatePassword(command.Request.NewPassword);
        await db.ExecuteInTransactionAsync(async token =>
        {
            var user = await db.Users.FindAsync([command.UserId], token)
                ?? throw new NotFoundException("user_not_found", "User not found.");
            // Xác minh mật khẩu hiện tại qua dịch vụ hash trước khi thay đổi.
            if (!passwords.Verify(command.Request.CurrentPassword, user.PasswordHash))
            {
                throw new AppException(
                    "invalid_password",
                    "Current password is incorrect.",
                    400);
            }

            // Password mới và việc revoke credential phải commit hoặc rollback cùng nhau.
            user.PasswordHash = passwords.Hash(command.Request.NewPassword);
            user.MustChangePassword = false;
            user.PasswordFailedAttempts = 0;
            user.PasswordLockoutUntil = null;
            await authentication.RevokeAllSessionsAsync(command.UserId, token);
            await db.SaveChangesAsync(token);
            return true;
        }, cancellationToken);
    }
}
