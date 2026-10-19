using MediatR;
namespace SpendWise.Application.Features.Authentication.Commands.Logout;

// Handler thu hồi session hiện tại và mọi refresh token thuộc session đó.

public sealed class LogoutCommandHandler(
    AuthenticationSupport authentication) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(
        LogoutCommand command,
        CancellationToken cancellationToken)
    {
        // SessionId lấy từ JWT đã xác thực, không tin refresh token trong request body.
        await authentication.RevokeSessionAsync(
            command.UserId,
            command.SessionId,
            cancellationToken);
    }
}
