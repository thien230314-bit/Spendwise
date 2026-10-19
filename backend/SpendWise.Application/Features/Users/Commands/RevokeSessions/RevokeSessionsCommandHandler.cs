using MediatR;
using SpendWise.Application.Features.Authentication;

namespace SpendWise.Application.Features.Users.Commands.RevokeSessions;

// Handler thu hồi các phiên đăng nhập cũ của người dùng hiện tại.

public sealed class RevokeSessionsCommandHandler(
    AuthenticationSupport authentication) : IRequestHandler<RevokeSessionsCommand>
{
    public async Task Handle(
        RevokeSessionsCommand command,
        CancellationToken cancellationToken)
    {
        // Thu hồi các session của chính người dùng theo yêu cầu bảo mật.
        await authentication.RevokeAllSessionsAsync(command.UserId, cancellationToken);
    }
}
