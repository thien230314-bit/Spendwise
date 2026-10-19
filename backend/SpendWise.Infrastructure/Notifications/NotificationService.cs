using System.Text.Json;
using SpendWise.Application.Features.Notifications;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Notifications;
using SpendWise.Domain.MobilePush;
using Microsoft.EntityFrameworkCore;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Infrastructure.Notifications;

// Lưu thông báo phát sinh từ các nghiệp vụ của Application.

public sealed class NotificationService(
    SpendWiseDbContext db) : INotificationService
{
    public async Task<NotificationDto> CreateAsync(
        Guid userId,
        string type,
        string title,
        string message,
        object? data,
        CancellationToken ct)
    {
        // Dùng chung batch implementation để một thông báo vẫn giữ hành vi cũ.
        var created = await CreateManyAsync(
            [new CreateNotificationRequest(userId, type, title, message, data)],
            ct);
        return created[0];
    }

    public Task<IReadOnlyList<NotificationDto>> CreateManyAsync(
        IReadOnlyCollection<CreateNotificationRequest> requests,
        CancellationToken ct)
    {
        // Kiểm tra toàn bộ loại thông báo trước khi mở transaction hoặc thêm entity.
        var prepared = requests.Select(request =>
        {
            if (!Enum.TryParse<NotificationType>(request.Type, true, out var type))
            {
                throw new ArgumentException("Unknown notification type.", nameof(requests));
            }

            return (Request: request, Type: type);
        }).ToList();

        // Batch Notification và Outbox trong một transaction; SignalR không chạy tại đây.
        return db.ExecuteInTransactionAsync(async token =>
        {
            var items = prepared.Select(item => new Notification
                {
                    UserId = item.Request.UserId,
                    Type = item.Type,
                    Title = item.Request.Title,
                    Message = item.Request.Message,
                    DataJson = item.Request.Data is null
                        ? null
                        : JsonSerializer.Serialize(item.Request.Data)
                })
                .ToList();
            db.Notifications.AddRange(items);
            // Save theo batch để chốt CreatedAt trước khi tạo payload realtime.
            await db.SaveChangesAsync(token);

            // Một batch Outbox tương ứng bảo đảm mỗi Notification có đúng một sự kiện sau commit.
            var notificationResults = items
                .Select(item => (Item: item, Dto: NotificationMappings.ToDto(item)))
                .ToList();
            db.OutboxMessages.AddRange(notificationResults.Select(result => new OutboxMessage
                {
                    UserId = result.Item.UserId,
                    EventName = "notification",
                    PayloadJson = JsonSerializer.Serialize(result.Dto)
                }));

            var userIds = notificationResults.Select(result => result.Item.UserId).Distinct().ToList();
            var installations = await db.MobilePushInstallations.AsNoTracking()
                .Where(item => item.IsActive
                    && item.PushToken != null
                    && userIds.Contains(item.UserId))
                .Select(item => new { item.Id, item.UserId })
                .ToListAsync(token);
            var installationsByUser = installations.ToLookup(item => item.UserId);
            db.MobilePushDeliveries.AddRange(notificationResults.SelectMany(result =>
                installationsByUser[result.Item.UserId].Select(installation =>
                    new MobilePushDelivery
                    {
                        NotificationId = result.Item.Id,
                        InstallationId = installation.Id
                    })));
            await db.SaveChangesAsync(token);

            return (IReadOnlyList<NotificationDto>)notificationResults
                .Select(result => result.Dto)
                .ToList();
        }, ct);
    }
}
