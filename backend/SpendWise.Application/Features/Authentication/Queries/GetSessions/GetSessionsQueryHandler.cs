using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Authentication.Queries.GetSessions;

public sealed class GetSessionsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetSessionsQuery, IReadOnlyList<SessionDto>>
{
    public async Task<IReadOnlyList<SessionDto>> Handle(
        GetSessionsQuery query,
        CancellationToken cancellationToken)
    {
        return await db.UserSessions
            .AsNoTracking()
            .Where(session => session.UserId == query.UserId && session.RevokedAt == null)
            .OrderByDescending(session => session.LastSeenAt)
            .ThenByDescending(session => session.CreatedAt)
            .Select(session => new SessionDto(
                session.Id,
                session.DeviceName,
                session.CreatedAt,
                session.LastSeenAt,
                session.RefreshTokens
                    .Where(token => token.RevokedAt == null)
                    .Select(token => (DateTime?)token.ExpiresAt)
                    .Max(),
                session.Id == query.CurrentSessionId))
            .ToListAsync(cancellationToken);
    }
}
