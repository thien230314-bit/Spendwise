using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Admin.Users.Commands.ResetUserSecurityPin;

public sealed class ResetUserSecurityPinCommandHandler(
    ISpendWiseDbContext db,
    AdminSecurityGuard security,
    AdminAuditWriter audit,
    SecurityPinResetSupport resets,
    AuthenticationSupport authentication,
    IEmailSender emailSender,
    ILogger<ResetUserSecurityPinCommandHandler> logger)
    : IRequestHandler<ResetUserSecurityPinCommand, AdminSecurityPinResetResponse>
{
    public async Task<AdminSecurityPinResetResponse> Handle(
        ResetUserSecurityPinCommand command,
        CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.AdminId, command.Request.AdminPin, cancellationToken);
        AdminAuditWriter.RequireReason(command.Request.Reason);
        if (command.AdminId == command.UserId)
        {
            throw new ForbiddenException("admin_self_reset_forbidden", "Administrators cannot reset their own PIN here.");
        }

        string rawToken = string.Empty;
        DateTime expiresAt = default;
        string targetEmail = string.Empty;
        await db.ExecuteInTransactionAsync(async token =>
        {
            var user = await AdminSecurityGuard.RequireOrdinaryTargetAsync(db, command.UserId, token);

            // Xóa hash PIN, thu hồi phiên và phát hành token enrollment mới trong cùng transaction.
            user.SecurityPinHash = null;
            user.SecurityPinFailedAttempts = 0;
            user.SecurityPinLockoutUntil = null;
            await authentication.RevokeAllSessionsAsync(user.Id, token);
            var issued = await resets.CreateAsync(
                user,
                SecurityPinResetPurpose.AdminEnrollment,
                command.AdminId,
                token);
            rawToken = issued.RawToken;
            expiresAt = issued.Token.ExpiresAt;
            targetEmail = user.Email;
            audit.Add(
                command.AdminId,
                "RESET_USER_SECURITY_PIN",
                "User",
                user.Id.ToString(),
                command.Request.Reason,
                new { ResetRequired = true });
            await db.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

        try
        {
            await emailSender.SendSecurityPinResetAsync(
                targetEmail,
                rawToken,
                expiresAt,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Admin PIN-reset delivery failed for UserId {UserId}", command.UserId);
        }

        return new AdminSecurityPinResetResponse(command.UserId, true);
    }
}
