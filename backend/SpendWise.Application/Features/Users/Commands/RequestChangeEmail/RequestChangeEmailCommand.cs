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

public sealed class RequestChangeEmailCommandHandler : IRequestHandler<RequestChangeEmailCommand, EmailVerificationPendingResponse>
{
    public Task<EmailVerificationPendingResponse> Handle(
        RequestChangeEmailCommand command,
        CancellationToken ct)
    {
        throw new AppException(
            "legacy_email_change_deprecated",
            "Email change requires 3-stage verification. Please use POST /api/users/me/email/current/request.",
            400);
    }
}
