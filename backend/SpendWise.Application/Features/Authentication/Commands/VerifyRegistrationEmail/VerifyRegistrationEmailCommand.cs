using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.VerifyRegistrationEmail;

public sealed record VerifyRegistrationEmailCommand(
    VerifyRegistrationEmailRequest Request,
    string? IpAddress,
    string? UserAgent) : IRequest<LoginChallengeResponse>;
