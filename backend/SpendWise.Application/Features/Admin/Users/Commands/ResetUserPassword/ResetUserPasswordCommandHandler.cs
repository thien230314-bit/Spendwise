using MediatR;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Admin.Users.Commands.ResetUserPassword;

public sealed class ResetUserPasswordCommandHandler(
    ISpendWiseDbContext db,
    AdminSecurityGuard security,
    AdminAuditWriter audit,
    ITemporaryPasswordService temporaryPasswords,
    IPasswordService passwords,
    AuthenticationSupport authentication)
    : IRequestHandler<ResetUserPasswordCommand, AdminTemporaryPasswordResponse>
{
    public async Task<AdminTemporaryPasswordResponse> Handle(
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.AdminId, command.Request.AdminPin, cancellationToken);
        AdminAuditWriter.RequireReason(command.Request.Reason);
        if (command.AdminId == command.UserId)
        {
            throw new ForbiddenException("admin_self_reset_forbidden", "Administrators cannot reset their own password here.");
        }

        // Mật khẩu tạm chỉ tồn tại trong response; database và audit chỉ nhận hash/metadata.
        var temporaryPassword = temporaryPasswords.CreateStrongPassword();
        AuthenticationSupport.ValidatePassword(temporaryPassword);
        await db.ExecuteInTransactionAsync(async token =>
        {
            var user = await AdminSecurityGuard.RequireOrdinaryTargetAsync(db, command.UserId, token);

            user.PasswordHash = passwords.Hash(temporaryPassword);
            user.MustChangePassword = true;
            user.PasswordFailedAttempts = 0;
            user.PasswordLockoutUntil = null;
            await authentication.RevokeAllSessionsAsync(user.Id, token);
            audit.Add(
                command.AdminId,
                "RESET_USER_PASSWORD",
                "User",
                user.Id.ToString(),
                command.Request.Reason,
                new { MustChangePassword = true });
            await db.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

        return new AdminTemporaryPasswordResponse(command.UserId, temporaryPassword, true);
    }
}
