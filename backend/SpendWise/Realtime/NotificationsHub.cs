using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SpendWise.Application.Notifications;

namespace SpendWise.API.Realtime;

// SignalR Hub này gắn kết nối realtime với UserId đã xác thực.

[Authorize]
public sealed class NotificationsHub : Hub;

public sealed class SignalRRealtimeNotifier(IHubContext<NotificationsHub> hub) : IRealtimeNotifier
{
    public Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken) =>
        hub.Clients.User(userId.ToString()).SendAsync(eventName, payload, cancellationToken);

    public Task SendAllAsync(string eventName, object payload, CancellationToken cancellationToken) =>
        hub.Clients.All.SendAsync(eventName, payload, cancellationToken);
}
