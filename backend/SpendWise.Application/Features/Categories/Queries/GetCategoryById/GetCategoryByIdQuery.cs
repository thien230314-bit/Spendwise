using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Categories.Queries.GetCategoryById;

// Lấy một danh mục hệ thống hoặc danh mục thuộc người dùng hiện tại.
public sealed record GetCategoryByIdQuery(Guid UserId, Guid CategoryId) : IRequest<CategoryDto>;
