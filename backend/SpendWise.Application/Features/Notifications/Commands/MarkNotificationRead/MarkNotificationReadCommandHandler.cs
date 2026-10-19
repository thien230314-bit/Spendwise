using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Notifications.Commands.MarkNotificationRead;

// Handler đánh dấu một thông báo thuộc người dùng hiện tại là đã đọc.

public sealed class MarkNotificationReadCommandHandler(
    ISpendWiseDbContext db,
    IClock clock) : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(
        MarkNotificationReadCommand command,
        CancellationToken cancellationToken)
    {
        // Chỉ đánh dấu thông báo thuộc người dùng hiện tại là đã đọc.
        var notification = await db.Notifications.SingleOrDefaultAsync(
            item => item.Id == command.NotificationId
                && item.UserId == command.UserId,
            cancellationToken)
            ?? throw new NotFoundException(
                "notification_not_found",
                "Notification not found.");

        notification.ReadAt ??= clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
