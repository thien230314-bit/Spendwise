using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Categories.Commands.ArchiveCategory;

// Command ngừng sử dụng danh mục cho giao dịch mới nhưng giữ tham chiếu lịch sử.

public sealed record ArchiveCategoryCommand(
    Guid UserId,
    Guid CategoryId) : IRequest<CategoryDeleteResult>;
