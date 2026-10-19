using SpendWise.Domain.Common;
using SpendWise.Domain.Notifications;

namespace SpendWise.Domain.MobilePush;

public enum MobilePlatform { Android, Ios }
public enum MobilePushDeliveryStatus { Pending, Submitted, Completed, Failed }

public sealed class MobilePushInstallation : BaseEntity
{
    public Guid UserId { get; set; }
    public MobilePlatform Platform { get; set; }
    public string? PushToken { get; set; }
    public string? DeviceName { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class MobilePushDelivery : BaseEntity
{
    public Guid NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;
    public Guid InstallationId { get; set; }
    public MobilePushInstallation Installation { get; set; } = null!;
    public MobilePushDeliveryStatus Status { get; set; } = MobilePushDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ExpoReceiptId { get; set; }
    public string? LastError { get; set; }
}
