using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Users.Queries.SearchAdminUsers;

// Query tìm người dùng theo bộ lọc dành cho quản trị viên.

public sealed record SearchAdminUsersQuery(
    string? Search) : IRequest<IReadOnlyList<AdminUserDto>>;
