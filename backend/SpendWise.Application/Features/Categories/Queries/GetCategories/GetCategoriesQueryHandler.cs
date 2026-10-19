using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Categories.Queries.GetCategories;

// Handler lấy các danh mục hệ thống và danh mục người dùng được phép sử dụng.

public sealed class GetCategoriesQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<IReadOnlyList<CategoryDto>> Handle(
        GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        // Lấy danh mục hệ thống và danh mục cá nhân còn hoạt động theo loại Thu/Chi.
        var categoriesQuery = db.Categories
            .AsNoTracking()
            .Where(category => (query.IncludeArchived || category.IsActive)
                && (category.IsSystem || category.OwnerUserId == query.UserId));
        if (!string.IsNullOrWhiteSpace(query.Kind))
        {
            if (!Enum.TryParse<CategoryKind>(query.Kind, true, out var categoryKind)
                || !Enum.IsDefined(categoryKind))
            {
                throw new AppException(
                    "invalid_category_kind",
                    "Category kind must be Income or Expense.");
            }

            categoriesQuery = categoriesQuery.Where(category => category.Kind == categoryKind);
        }

        var categories = await categoriesQuery
            .OrderBy(category => category.Kind)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);

        return categories.Select(CategoryMappings.ToDto).ToList();
    }
}
