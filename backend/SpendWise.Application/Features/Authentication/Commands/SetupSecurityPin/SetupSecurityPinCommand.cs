using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.SetupSecurityPin;

// Hoàn tất challenge đầu tiên bằng cách thiết lập PIN rồi mới cấp session.
public sealed record SetupSecurityPinCommand(
    SetupSecurityPinRequest Request) : IRequest<AuthResponse>;
