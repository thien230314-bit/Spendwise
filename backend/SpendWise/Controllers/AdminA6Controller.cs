using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.API.Infrastructure;
using SpendWise.Application.Features.Administration;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize(Policy = V1Authorization.OperationsRead)]
[Route("api/v1/admin/notifications")]
public sealed class AdminA6Controller(AdminA6Service service) : ControllerBase
{
    // A6.1 - Thông báo nghiệp vụ
    [HttpPost("search")]
    public Task<A2Page<A6NotificationItem>> Notifications(A6NotificationQuery query, CancellationToken ct) =>
        service.NotificationsAsync(User, query, ct);

    // A6.2 - Lịch sử gửi (Delivery History)
    [HttpPost("delivery-history")]
    public Task<A2Page<A6DeliveryHistoryItem>> DeliveryHistory(A6DeliveryHistoryQuery query, CancellationToken ct) =>
        service.DeliveryHistoryAsync(User, query, ct);

    // A6.3 - Lỗi gửi thông báo (Failed Deliveries Feed)
    [HttpPost("failures")]
    public Task<A2Page<A6DeliveryFailureItem>> DeliveryFailures(A6DeliveryFailureQuery query, CancellationToken ct) =>
        service.DeliveryFailuresAsync(User, query, ct);

    // A6.4 - Danh mục mẫu thông báo (Templates)
    [HttpPost("templates")]
    public Task<IReadOnlyList<A6NotificationTemplateItem>> Templates(OperatorDirectoryRequest request, CancellationToken ct) =>
        service.GetTemplatesAsync(User, request.Reason, ct);

    // A6.4 - Tổng quan bảo trì (Maintenance Overview)
    [HttpPost("maintenance-overview")]
    public Task<A6MaintenanceOverviewResult> MaintenanceOverview(OperatorDirectoryRequest request, CancellationToken ct) =>
        service.GetMaintenanceOverviewAsync(User, request.Reason, ct);
}
