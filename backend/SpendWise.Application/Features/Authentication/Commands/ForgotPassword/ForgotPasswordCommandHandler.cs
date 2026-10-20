using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.ForgotPassword;

// Handler tìm tài khoản một cách trung lập và xếp email OTP reset vào outbox.
public sealed class ForgotPasswordCommandHandler(
    ISpendWiseDbContext db,
    EmailVerificationSupport emailVerification)
    : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken)
    {
        // Identifier sai hoặc không tồn tại vẫn kết thúc như thành công để chống dò tài khoản.
        var identifier = command.Request.Identifier?.Trim();
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 256)
            return;

        User? user;
        if (InputBoundary.IsValidEmail(identifier))
        {
            var normalizedEmail = identifier.ToUpperInvariant();
            user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.NormalizedEmail == normalizedEmail
                    && (candidate.Status == UserStatus.Active || candidate.Status == UserStatus.Restricted)
                    && candidate.EmailVerifiedAt != null,
                cancellationToken);
        }
        else if (InputBoundary.TryNormalizePhone(identifier, out var normalizedPhone))
        {
            user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.Phone == normalizedPhone
                    && (candidate.Status == UserStatus.Active || candidate.Status == UserStatus.Restricted)
                    && candidate.EmailVerifiedAt != null,
                cancellationToken);
        }
        else
        {
            return;
        }

        if (user is null)
            return;

        // Dù tra bằng số điện thoại, OTP chỉ được xếp gửi tới email đã xác minh.
        await db.ExecuteInTransactionAsync(async token =>
        {
            await emailVerification.IssuePasswordResetOtpAsync(
                user,
                command.IpAddress,
                token);
            return true;
        }, cancellationToken);
    }
}
