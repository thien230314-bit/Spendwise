using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Users.Commands.ResetUserPassword;

public sealed record ResetUserPasswordCommand(
    Guid AdminId,
    Guid UserId,
    AdminCredentialResetRequest Request)
    : IRequest<AdminTemporaryPasswordResponse>;
