using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.Categories.Commands.UpdateCategory;

// Sửa metadata danh mục cá nhân mà không đổi loại Thu/Chi của lịch sử cũ.
public sealed class UpdateCategoryCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    public async Task<CategoryDto> Handle(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        // Chuẩn hóa loại và các trường hiển thị theo giới hạn hiện có của database.
        var name = command.Request.Name.Trim();
        if (!Enum.TryParse<CategoryKind>(command.Request.Kind, true, out var kind) || !Enum.IsDefined(kind))
            throw new AppException("invalid_category_kind", "Category kind must be Income or Expense.");
        if (name.Length is < 1 or > 80
            || command.Request.Icon?.Length > 50
            || command.Request.Color?.Length > 20)
        {
            throw new AppException("invalid_category", "Category values are invalid.");
        }

        // Danh mục hệ thống không thuộc người dùng nên không được sửa qua API cá nhân.
        var category = await db.Categories.SingleOrDefaultAsync(
            candidate => candidate.Id == command.CategoryId
                && candidate.OwnerUserId == command.UserId
                && !candidate.IsSystem,
            cancellationToken)
            ?? throw new NotFoundException("category_not_found", "Custom category not found.");

        // Không cho trùng tên với danh mục cá nhân đang hoạt động cùng loại.
        var duplicate = await db.Categories.AnyAsync(
            candidate => candidate.Id != category.Id
                && candidate.OwnerUserId == command.UserId
                && candidate.Kind == kind
                && candidate.Name == name
                && candidate.IsActive,
            cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("category_exists", "Category already exists.");
        }

        // Lịch định kỳ đang hoạt động phải giữ cùng loại Thu/Chi với danh mục của nó.
        if (category.Kind != kind && await db.RecurringTransactions.AnyAsync(
                item => item.CategoryId == category.Id
                    && item.Status == RecurringStatus.Active
                    && item.Kind != kind,
                cancellationToken))
        {
            throw new ConflictException(
                "category_kind_in_use",
                "Pause or update active recurring transactions before changing the category kind.");
        }

        category.Name = name;
        category.Kind = kind;
        category.Icon = command.Request.Icon;
        category.Color = command.Request.Color;
        await db.SaveChangesAsync(cancellationToken);

        return CategoryMappings.ToDto(category);
    }
}
