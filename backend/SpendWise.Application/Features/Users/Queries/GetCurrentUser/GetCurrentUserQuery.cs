using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Users.Queries.GetCurrentUser;

// Query lấy hồ sơ từ UserId trong phiên đăng nhập.

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<UserProfile>;
