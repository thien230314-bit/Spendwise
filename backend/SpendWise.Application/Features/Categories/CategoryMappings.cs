using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Categories;

// Lớp này chuyển Category sang dữ liệu trả về cho API.

internal static class CategoryMappings
{
    public static CategoryDto ToDto(Category category)
    {
        // Chuyển metadata và trạng thái archive của Category sang DTO.
        return new CategoryDto(
            category.Id,
            category.Key,
            category.Name,
            category.Kind.ToString(),
            category.Icon,
            category.Color,
            category.IsSystem,
            category.IsActive);
    }
}
