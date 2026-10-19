namespace SpendWise.Application.Features.Admin.Audit.Queries.GetAuditLogs;

// DTO công khai cấu trúc audit log rõ ràng cho OpenAPI và client quản trị.
public sealed record AuditLogDto(
    Guid Id,
    Guid? ActorUserId,
    string Action,
    string TargetType,
    string? TargetId,
    string? Reason,
    string? MetadataJson,
    DateTime CreatedAt);
