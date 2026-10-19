using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Categories.Commands.CreateCategory;

// Handler tạo danh mục mới theo phạm vi hệ thống hoặc người dùng được phép.

public sealed class CreateCategoryCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    public async Task<CategoryDto> Handle(
        CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        // Kiểm tra loại, tên trùng và quyền sở hữu trước khi tạo danh mục cá nhân.
        if (!Enum.TryParse<CategoryKind>(command.Request.Kind, true, out var categoryKind)
            || !Enum.IsDefined(categoryKind))
        {
            throw new AppException(
                "invalid_category_kind",
                "Category kind must be Income or Expense.");
        }

        if (string.IsNullOrWhiteSpace(command.Request.Name)
            || command.Request.Name.Trim().Length > 80
            || command.Request.Icon?.Length > 50
            || command.Request.Color?.Length > 20)
        {
            throw new AppException(
                "invalid_category_name",
                "Category name is required and limited to 80 characters.");
        }

        var normalizedName = command.Request.Name.Trim();
        var categoryExists = await db.Categories.AnyAsync(
            category => category.OwnerUserId == command.UserId
                && category.Kind == categoryKind
                && category.Name == normalizedName
                && category.IsActive,
            cancellationToken);
        if (categoryExists)
        {
            throw new ConflictException("category_exists", "Category already exists.");
        }

        var category = new Category
        {
            OwnerUserId = command.UserId,
            Key = $"custom:{Guid.NewGuid():N}",
            Name = normalizedName,
            Kind = categoryKind,
            Icon = command.Request.Icon,
            Color = command.Request.Color
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return CategoryMappings.ToDto(category);
    }
}
