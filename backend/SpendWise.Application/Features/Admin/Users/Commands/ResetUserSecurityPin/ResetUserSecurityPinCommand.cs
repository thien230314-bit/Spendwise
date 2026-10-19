using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Users.Commands.ResetUserSecurityPin;

public sealed record ResetUserSecurityPinCommand(
    Guid AdminId,
    Guid UserId,
    AdminCredentialResetRequest Request)
    : IRequest<AdminSecurityPinResetResponse>;
