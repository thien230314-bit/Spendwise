using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Users.Commands.ConfirmChangeEmail;

public sealed record ConfirmChangeEmailRequest(string Otp);

public sealed record ConfirmChangeEmailCommand(
    Guid UserId,
    ConfirmChangeEmailRequest Request,
    string? IpAddress) : IRequest<UserProfile>;

public sealed class ConfirmChangeEmailCommandHandler : IRequestHandler<ConfirmChangeEmailCommand, UserProfile>
{
    public Task<UserProfile> Handle(
        ConfirmChangeEmailCommand command,
        CancellationToken ct)
    {
        throw new AppException(
            "legacy_email_change_deprecated",
            "Email change requires 3-stage verification. Please use POST /api/users/me/email/new/confirm with ticket.",
            400);
    }
}
