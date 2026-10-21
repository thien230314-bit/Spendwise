using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Admin.Users.Queries.SearchAdminUsers;

// Handler tìm người dùng theo bộ lọc dành cho quản trị viên.

public sealed class SearchAdminUsersQueryHandler(
    ISpendWiseDbContext db)
    : IRequestHandler<SearchAdminUsersQuery, IReadOnlyList<AdminUserDto>>
{
    public async Task<IReadOnlyList<AdminUserDto>> Handle(
        SearchAdminUsersQuery query,
        CancellationToken cancellationToken)
    {
        // Tìm người dùng theo bộ lọc quản trị và phân trang kết quả.
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchValue = query.Search.Trim();
            users = users.Where(user => user.FullName.Contains(searchValue)
                || user.Email.Contains(searchValue)
                || user.SpendId.Contains(searchValue));
        }

        return await users
            .OrderByDescending(user => user.CreatedAt)
            .Take(100)
            .Select(user => new AdminUserDto(
                user.Id,
                user.FullName,
                user.Email,
                user.SpendId,
                user.Status.ToString(),
                user.SystemRole.ToString(),
                user.MustChangePassword,
                user.SecurityPinHash != null,
                user.CreatedAt,
                user.LastLoginAt,
                user.AvatarUrl,
                user.EmailVerifiedAt != null))
            .ToListAsync(cancellationToken);
    }
}
