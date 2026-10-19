using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.ChangeSecurityPin;

public sealed record ChangeSecurityPinCommand(
    Guid UserId,
    ChangeSecurityPinRequest Request) : IRequest;
