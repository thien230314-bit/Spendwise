using MediatR;
using SpendWise.Application.Notifications;

namespace SpendWise.Application.Features.Notifications.Queries.GetNotifications;

// Query lấy thông báo thuộc người dùng đang đăng nhập.

public sealed record GetNotificationsQuery(
    Guid UserId,
    bool UnreadOnly) : IRequest<IReadOnlyList<NotificationDto>>;
