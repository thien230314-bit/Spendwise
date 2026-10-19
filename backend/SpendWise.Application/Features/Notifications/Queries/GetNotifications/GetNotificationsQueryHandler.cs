using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Notifications;

namespace SpendWise.Application.Features.Notifications.Queries.GetNotifications;

// Handler lấy thông báo thuộc người dùng đang đăng nhập.

public sealed class GetNotificationsQueryHandler(
    ISpendWiseDbContext db,
    BudgetAlertEvaluator budgetAlerts,
    FinancialAlertEvaluator financialAlerts)
    : IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    public async Task<IReadOnlyList<NotificationDto>> Handle(
        GetNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        // Đồng bộ cảnh báo từ trạng thái nghiệp vụ trước khi trả danh sách persisted.
        await budgetAlerts.EvaluateAsync(query.UserId, cancellationToken);
        await financialAlerts.EvaluateAsync(query.UserId, cancellationToken);
        // Lấy thông báo của người dùng theo thứ tự mới nhất và trạng thái đã đọc.
        var notifications = db.Notifications
            .AsNoTracking()
            .Where(item => item.UserId == query.UserId);
        if (query.UnreadOnly)
        {
            notifications = notifications.Where(item => item.ReadAt == null);
        }

        var items = await notifications
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return items.Select(NotificationMappings.ToDto).ToList();
    }
}
