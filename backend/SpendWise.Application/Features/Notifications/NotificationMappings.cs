using SpendWise.Application.Notifications;
using SpendWise.Domain.Notifications;

namespace SpendWise.Application.Features.Notifications;

// Lớp này chuyển Notification sang DTO và tính trạng thái đã đọc.

public static class NotificationMappings
{
    public static NotificationDto ToDto(Notification notification)
    {
        // Tính IsRead từ ReadAt rồi tạo NotificationDto.
        return new NotificationDto(
            notification.Id,
            notification.Type.ToString(),
            notification.Title,
            notification.Message,
            notification.DataJson,
            notification.ReadAt is not null,
            notification.CreatedAt,
            notification.ReadAt);
    }
}
