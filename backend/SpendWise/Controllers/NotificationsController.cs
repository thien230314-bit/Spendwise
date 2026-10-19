using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using SpendWise.Application.Features.Notifications.Commands.MarkNotificationRead;
using SpendWise.Application.Features.Notifications.Queries.GetNotifications;
using SpendWise.Application.Notifications;
using SpendWise.Application.Features.Notifications;

namespace SpendWise.API.Controllers;

// Cung cấp API đọc thông báo thuộc người dùng đang đăng nhập.

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<NotificationDto>> List(
        [FromQuery] bool unreadOnly,
        CancellationToken ct)
    {
        // Lấy UserId từ token rồi đọc các thông báo thuộc người dùng.
        var userId = UserId();

        return await mediator.Send(new GetNotificationsQuery(userId, unreadOnly), ct);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        // Gửi Command đánh dấu một thông báo thuộc người dùng là đã đọc.
        var userId = UserId();
        await mediator.Send(new MarkNotificationReadCommand(userId, id), ct);

        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        // Gửi Command đánh dấu toàn bộ thông báo của người dùng là đã đọc.
        var userId = UserId();
        await mediator.Send(new MarkAllNotificationsReadCommand(userId), ct);

        return NoContent();
    }

    [HttpGet("search")]
    public Task<NotificationPageDto> Search([FromQuery] NotificationSearchFilter filter, CancellationToken ct) =>
        mediator.Send(new SearchNotificationsQuery(UserId(), filter), ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await mediator.Send(new DeleteNotificationCommand(UserId(), id), ct); return NoContent(); }

    [HttpDelete]
    public Task<int> Clear(CancellationToken ct) => mediator.Send(new ClearNotificationsCommand(UserId()), ct);

    [HttpGet("preferences")]
    public Task<IReadOnlyList<NotificationPreferenceDto>> Preferences(CancellationToken ct) => mediator.Send(new GetNotificationPreferencesQuery(UserId()), ct);

    [HttpPut("preferences")]
    public Task<NotificationPreferenceDto> SavePreference(NotificationPreferenceRequest request, CancellationToken ct) => mediator.Send(new SaveNotificationPreferenceCommand(UserId(), request), ct);

    private Guid UserId()
    {
        // Lấy định danh từ phiên đã xác thực, không tin UserId do client gửi.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}
