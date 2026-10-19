using MediatR;

namespace SpendWise.Application.Features.Notifications.Commands.MarkAllNotificationsRead;

// Command đánh dấu mọi thông báo của người dùng hiện tại là đã đọc.

public sealed record MarkAllNotificationsReadCommand(Guid UserId) : IRequest;
