using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.ResetSecurityPin;

public sealed record ResetSecurityPinCommand(
    ResetSecurityPinRequest Request) : IRequest;
