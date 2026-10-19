using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Queries.ValidateSecurityPinResetToken;

public sealed record ValidateSecurityPinResetTokenQuery(
    ValidateSecurityPinResetTokenRequest Request)
    : IRequest<SecurityPinResetTokenStatusDto>;
