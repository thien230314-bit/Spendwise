using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Categories.Commands.CreateCategory;

// Command tạo danh mục mới theo phạm vi hệ thống hoặc người dùng được phép.

public sealed record CreateCategoryCommand(
    Guid UserId,
    CreateCategoryRequest Request) : IRequest<CategoryDto>;
