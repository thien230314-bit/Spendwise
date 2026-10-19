using MediatR;

namespace SpendWise.Application.Features.Authentication.Commands.RevokeSession;

public sealed record RevokeSessionCommand(
    Guid UserId,
    Guid SessionId) : IRequest;
