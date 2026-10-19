using MediatR;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Admin.Users.Commands.ChangeUserStatus;

// Handler thay đổi trạng thái tài khoản theo quyền và workflow quản trị.

public sealed class ChangeUserStatusCommandHandler(
    ISpendWiseDbContext db,
    AuthenticationSupport authentication,
    AdminAuditWriter audit,
    AdminSecurityGuard security) : IRequestHandler<ChangeUserStatusCommand, AdminUserDto>
{
    public async Task<AdminUserDto> Handle(
        ChangeUserStatusCommand command,
        CancellationToken cancellationToken)
    {
        // Đổi trạng thái là thao tác nhạy cảm nên phải xác minh lại PIN của Admin.
        await security.VerifyAsync(
            command.AdminId,
            command.Request.AdminPin,
            cancellationToken);
        // Đổi trạng thái tài khoản theo quyền admin mà không sửa dữ liệu tài chính.
        var statusIsValid = Enum.TryParse<UserStatus>(
            command.Request.Status,
            true,
            out var status)
            && Enum.IsDefined(status);
        if (!statusIsValid || string.IsNullOrWhiteSpace(command.Request.Reason))
        {
            throw new AppException(
                "invalid_admin_action",
                "Valid status and reason are required.");
        }

        // Trạng thái, thu hồi phiên và audit phải commit hoặc rollback cùng nhau.
        return await db.ExecuteInTransactionAsync(async token =>
        {
            var user = await AdminSecurityGuard.RequireOrdinaryTargetAsync(db, command.UserId, token);

            var previousStatus = user.Status;
            user.Status = status;
            if (previousStatus != status
                && (previousStatus == UserStatus.Active || status == UserStatus.Active))
            {
                // Khóa hoặc kích hoạt lại đều thu hồi credential cũ để buộc đăng nhập mới.
                await authentication.RevokeAllSessionsAsync(user.Id, token);
            }

            audit.Add(
                command.AdminId,
                status == UserStatus.Active ? "UNLOCK_USER" : "LOCK_USER",
                "User",
                command.UserId.ToString(),
                command.Request.Reason,
                null);
            await db.SaveChangesAsync(token);

            return AdminMappings.ToDto(user);
        }, cancellationToken);
    }
}
