using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Authentication.Commands.RevokeSession;

public sealed class RevokeSessionCommandHandler(
    ISpendWiseDbContext db,
    AuthenticationSupport authentication) : IRequestHandler<RevokeSessionCommand>
{
    public async Task Handle(
        RevokeSessionCommand command,
        CancellationToken cancellationToken)
    {
        var ownedSessionExists = await db.UserSessions
            .AsNoTracking()
            .AnyAsync(
                session => session.Id == command.SessionId
                    && session.UserId == command.UserId,
                cancellationToken);
        if (!ownedSessionExists)
            throw new NotFoundException("session_not_found", "Session not found.");

        await authentication.RevokeSessionAsync(
            command.UserId,
            command.SessionId,
            cancellationToken);
    }
}
