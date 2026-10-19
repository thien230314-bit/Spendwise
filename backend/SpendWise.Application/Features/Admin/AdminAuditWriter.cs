using System.Text.Json;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.Admin;

// Lớp này ghi lý do và metadata của thao tác quản trị vào AuditLog.

public sealed class AdminAuditWriter(ISpendWiseDbContext db)
{
    public void Add(
        Guid actorUserId,
        string action,
        string targetType,
        string? targetId,
        string reason,
        object? metadata)
    {
        // Thêm AuditLog mô tả admin, hành động, đối tượng và lý do quản trị.
        RequireReason(reason);
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason.Trim(),
            MetadataJson = metadata is null
                ? null
                : JsonSerializer.Serialize(metadata)
        });
    }

    public static void RequireReason(string reason)
    {
        // Thao tác quản trị nhạy cảm phải có lý do đủ rõ để lưu vào AuditLog.
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 1000)
        {
            throw new AppException(
                "reason_required",
                "A meaningful reason of 5-1000 characters is required.");
        }
    }
}
