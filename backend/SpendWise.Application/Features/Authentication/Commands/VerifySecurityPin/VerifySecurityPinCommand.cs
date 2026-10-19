using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.VerifySecurityPin;

public sealed record VerifySecurityPinCommand(
    Guid UserId,
    VerifySecurityPinRequest Request) : IRequest;
