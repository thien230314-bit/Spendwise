using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.ResendRegistrationEmail;

public sealed record ResendRegistrationEmailCommand(
    ResendRegistrationEmailRequest Request,
    string? IpAddress) : IRequest<EmailVerificationPendingResponse>;
