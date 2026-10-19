using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Queries.GetSecurityPinStatus;

public sealed record GetSecurityPinStatusQuery(Guid UserId) : IRequest<SecurityPinStatusDto>;
