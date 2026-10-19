using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.ResendRegistrationEmail;

public sealed class ResendRegistrationEmailCommandHandler(
    ISpendWiseDbContext db,
    EmailVerificationSupport emailVerification,
    IEmailVerificationPolicy policy,
    IClock clock) : IRequestHandler<ResendRegistrationEmailCommand, EmailVerificationPendingResponse>
{
    public async Task<EmailVerificationPendingResponse> Handle(
        ResendRegistrationEmailCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = InputBoundary.NormalizeEmail(command.Request.Email);
        var user = await db.Users.SingleOrDefaultAsync(
            item => item.NormalizedEmail == normalizedEmail,
            cancellationToken);

        // Không tiết lộ email không tồn tại hoặc đã xác minh; chỉ tài khoản pending mới xếp email.
        if (user is null || user.EmailVerifiedAt is not null)
        {
            return new EmailVerificationPendingResponse(
                "EMAIL_VERIFICATION_REQUEST_ACCEPTED",
                clock.UtcNow.Add(policy.OtpLifetime),
                (int)policy.ResendCooldown.TotalSeconds);
        }

        return await emailVerification.IssueRegistrationOtpAsync(
            user,
            command.IpAddress,
            cancellationToken);
    }
}
