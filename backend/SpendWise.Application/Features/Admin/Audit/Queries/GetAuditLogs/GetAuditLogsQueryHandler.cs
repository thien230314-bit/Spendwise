using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Admin.Audit.Queries.GetAuditLogs;

// Handler đọc nhật ký kiểm toán bất biến theo bộ lọc quản trị.

public sealed class GetAuditLogsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetAuditLogsQuery, IReadOnlyList<AuditLogDto>>
{
    public async Task<IReadOnlyList<AuditLogDto>> Handle(
        GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        // Đọc nhật ký quản trị theo bộ lọc thời gian mà không sửa dữ liệu audit.
        var logs = await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Take(500)
            .Select(item => new AuditLogDto(
                item.Id,
                item.ActorUserId,
                item.Action,
                item.TargetType,
                item.TargetId,
                item.Reason,
                item.MetadataJson,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        return logs;
    }
}
