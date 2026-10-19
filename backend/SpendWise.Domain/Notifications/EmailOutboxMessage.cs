using SpendWise.Domain.Common;

namespace SpendWise.Domain.Notifications;

// Trạng thái gửi của email được lưu bền vững để worker có thể thử lại an toàn.
public enum EmailDeliveryStatus
{
    Pending,
    Processing,
    Sent,
    Failed
}

// Bản ghi email chờ gửi sau khi nghiệp vụ đã commit thành công.
public sealed class EmailOutboxMessage : BaseEntity
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public string DeduplicationKey { get; set; } = string.Empty;
    public EmailDeliveryStatus Status { get; set; } = EmailDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? ContentExpiresAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
}
