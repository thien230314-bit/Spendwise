using MediatR;

namespace SpendWise.Application.Features.Notifications.Commands.MarkNotificationRead;

// Command đánh dấu một thông báo thuộc người dùng hiện tại là đã đọc.

public sealed record MarkNotificationReadCommand(
    Guid UserId,
    Guid NotificationId) : IRequest;
