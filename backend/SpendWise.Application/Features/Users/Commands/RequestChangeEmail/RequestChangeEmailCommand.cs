using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Users.Commands.RequestChangeEmail;

public sealed record RequestChangeEmailRequest(string NewEmail);

public sealed record RequestChangeEmailCommand(
    Guid UserId,
    RequestChangeEmailRequest Request,
    string? IpAddress) : IRequest<EmailVerificationPendingResponse>;

public sealed class RequestChangeEmailCommandHandler(
    ISpendWiseDbContext db,
    EmailVerificationSupport emailVerification) : IRequestHandler<RequestChangeEmailCommand, EmailVerificationPendingResponse>
{
    public async Task<EmailVerificationPendingResponse> Handle(
        RequestChangeEmailCommand command,
        CancellationToken ct)
    {
        var normalizedNewEmail = InputBoundary.NormalizeEmail(command.Request.NewEmail);
        if (string.IsNullOrWhiteSpace(normalizedNewEmail))
            throw new AppException("invalid_email", "Email is required.", 400);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new NotFoundException("user_not_found", "User not found.");

        if (string.Equals(user.NormalizedEmail, normalizedNewEmail, StringComparison.OrdinalIgnoreCase))
            throw new AppException("email_unchanged", "New email cannot be the same as the current email.", 400);

        // Check if new email is already taken by another active/verified user
        var isDuplicate = await db.Users.AsNoTracking().AnyAsync(
            u => u.NormalizedEmail == normalizedNewEmail && u.Id != command.UserId,
            ct);

        if (isDuplicate)
            throw new AppException("duplicate_email", "Email address is already in use by another account.", 409);

        // Issue OTP and atomically update PendingNewEmail in the same transaction
        return await emailVerification.IssueChangeEmailOtpAsync(
            user,
            command.Request.NewEmail.Trim(),
            command.IpAddress,
            ct);
    }
}
