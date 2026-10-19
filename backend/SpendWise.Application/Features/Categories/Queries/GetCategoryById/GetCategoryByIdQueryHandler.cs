using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Categories.Queries.GetCategoryById;

// Trả danh mục hệ thống hoặc danh mục cá nhân mà người dùng được phép xem.
public sealed class GetCategoryByIdQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetCategoryByIdQuery, CategoryDto>
{
    public async Task<CategoryDto> Handle(GetCategoryByIdQuery query, CancellationToken cancellationToken)
    {
        // Danh mục hệ thống dùng chung; danh mục cá nhân chỉ chủ sở hữu được xem.
        var category = await db.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == query.CategoryId
                    && (candidate.IsSystem || candidate.OwnerUserId == query.UserId),
                cancellationToken)
            ?? throw new NotFoundException("category_not_found", "Category not found.");

        return CategoryMappings.ToDto(category);
    }
}
