using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Notifications.Commands.MarkAllNotificationsRead;

// Handler đánh dấu mọi thông báo của người dùng hiện tại là đã đọc.

public sealed class MarkAllNotificationsReadCommandHandler(
    ISpendWiseDbContext db,
    IClock clock) : IRequestHandler<MarkAllNotificationsReadCommand>
{
    public async Task Handle(
        MarkAllNotificationsReadCommand command,
        CancellationToken cancellationToken)
    {
        // Đánh dấu toàn bộ thông báo chưa đọc của người dùng bằng một lần cập nhật.
        var readAt = clock.UtcNow;
        await db.Notifications
            .Where(item => item.UserId == command.UserId && item.ReadAt == null)
            .ExecuteUpdateAsync(
                changes => changes.SetProperty(item => item.ReadAt, readAt),
                cancellationToken);
    }
}
