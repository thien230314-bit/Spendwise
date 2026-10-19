using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Categories.Commands.UpdateCategory;

// Yêu cầu sửa tên và thông tin hiển thị của danh mục cá nhân.
public sealed record UpdateCategoryCommand(
    Guid UserId,
    Guid CategoryId,
    UpdateCategoryRequest Request) : IRequest<CategoryDto>;
