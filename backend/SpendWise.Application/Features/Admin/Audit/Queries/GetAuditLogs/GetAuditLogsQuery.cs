using MediatR;

namespace SpendWise.Application.Features.Admin.Audit.Queries.GetAuditLogs;

// Query đọc nhật ký kiểm toán bất biến theo bộ lọc quản trị.

public sealed record GetAuditLogsQuery : IRequest<IReadOnlyList<AuditLogDto>>;
