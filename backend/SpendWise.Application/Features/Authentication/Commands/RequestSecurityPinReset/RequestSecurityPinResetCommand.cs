using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.RequestSecurityPinReset;

public sealed record RequestSecurityPinResetCommand(
    ForgotSecurityPinRequest Request) : IRequest;
