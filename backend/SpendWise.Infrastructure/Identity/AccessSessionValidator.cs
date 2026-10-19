using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Infrastructure.Identity;

// Validator này từ chối access token khi tài khoản hoặc phiên không còn hoạt động.
public sealed class AccessSessionValidator(
    SpendWiseDbContext db) : IAccessSessionValidator
{
    public Task<bool> IsValidAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        // Một query kiểm tra đồng thời chủ phiên, trạng thái thu hồi và trạng thái tài khoản.
        return db.UserSessions
            .AsNoTracking()
            .AnyAsync(
                session => session.Id == sessionId
                    && session.UserId == userId
                    && session.RevokedAt == null
                    && session.User.Id == userId
                    && session.User.Status == UserStatus.Active
                    && session.User.SystemRole != SystemRole.SuperAdmin
                    && session.User.EmailVerifiedAt != null,
                cancellationToken);
    }
}
