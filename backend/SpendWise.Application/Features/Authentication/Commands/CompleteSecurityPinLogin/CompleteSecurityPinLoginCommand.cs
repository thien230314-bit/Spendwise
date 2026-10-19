using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.CompleteSecurityPinLogin;

// Xác minh PIN của tài khoản đã enrollment trước khi cấp session.
public sealed record CompleteSecurityPinLoginCommand(
    CompleteSecurityPinLoginRequest Request) : IRequest<AuthResponse>;
