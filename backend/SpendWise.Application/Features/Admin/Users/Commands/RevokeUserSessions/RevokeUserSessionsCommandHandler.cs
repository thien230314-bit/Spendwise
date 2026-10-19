using MediatR;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;

namespace SpendWise.Application.Features.Admin.Users.Commands.RevokeUserSessions;

// Handler thu hồi phiên của một người dùng theo quyền quản trị.

public sealed class RevokeUserSessionsCommandHandler(
    ISpendWiseDbContext db,
    AuthenticationSupport authentication,
    AdminAuditWriter audit) : IRequestHandler<RevokeUserSessionsCommand>
{
    public async Task Handle(
        RevokeUserSessionsCommand command,
        CancellationToken cancellationToken)
    {
        // Thu hồi toàn bộ session của người dùng và ghi dấu thao tác quản trị.
        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new AppException("reason_required", "Reason is required.");
        }

        await db.ExecuteInTransactionAsync(async token =>
        {
            await AdminSecurityGuard.RequireOrdinaryTargetAsync(db, command.UserId, token);
            await authentication.RevokeAllSessionsAsync(command.UserId, token);
            audit.Add(command.AdminId, "REVOKE_SESSIONS", "User", command.UserId.ToString(), command.Reason, null);
            await db.SaveChangesAsync(token);
            return true;
        }, cancellationToken);
    }
}
