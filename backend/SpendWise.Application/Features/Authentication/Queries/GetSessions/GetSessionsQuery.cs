using MediatR;

namespace SpendWise.Application.Features.Authentication.Queries.GetSessions;

public sealed record SessionDto(
    Guid SessionId,
    string DeviceName,
    DateTime CreatedAt,
    DateTime LastSeenAt,
    DateTime? ExpiresAt,
    bool IsCurrent);

public sealed record GetSessionsQuery(
    Guid UserId,
    Guid CurrentSessionId) : IRequest<IReadOnlyList<SessionDto>>;
