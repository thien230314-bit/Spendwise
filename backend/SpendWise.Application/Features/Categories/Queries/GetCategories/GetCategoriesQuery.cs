using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Categories.Queries.GetCategories;

// Query lấy các danh mục hệ thống và danh mục người dùng được phép sử dụng.

public sealed record GetCategoriesQuery(
    Guid UserId,
    string? Kind,
    bool IncludeArchived = false) : IRequest<IReadOnlyList<CategoryDto>>;
