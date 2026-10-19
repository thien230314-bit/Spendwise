using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Users.Queries.GetCurrentUser;

// Handler lấy hồ sơ từ UserId trong phiên đăng nhập.

public sealed class GetCurrentUserQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetCurrentUserQuery, UserProfile>
{
    public async Task<UserProfile> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        // Tải hồ sơ của người dùng đang đăng nhập từ UserId trong access token.
        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == query.UserId, cancellationToken)
            ?? throw new NotFoundException("user_not_found", "User not found.");

        return AuthenticationSupport.MapProfile(user);
    }
}
