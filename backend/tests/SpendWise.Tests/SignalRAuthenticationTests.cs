using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using SpendWise.Application.Identity;
using Xunit;

namespace SpendWise.Tests;

// Kiểm tra Hub nhận JWT từ query string giống transport WebSocket của trình duyệt.
public sealed class SignalRAuthenticationTests
{
    [Fact]
    public async Task NotificationsHub_accepts_valid_query_token_and_completes_handshake()
    {
        // Arrange: đăng ký qua API để access token có UserSession thật trong database test.
        using var factory = new SpendWiseApiFactory();
        using var http = factory.CreateHttpsClient();
        var auth = await RegisterAsync(http);
        var socketClient = factory.Server.CreateWebSocketClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act: AccessTokenProvider của SignalR browser tạo query access_token tương đương URL này.
        using var socket = await socketClient.ConnectAsync(
            new Uri($"ws://localhost/hubs/notifications?access_token={Uri.EscapeDataString(auth.AccessToken)}"),
            timeout.Token);
        var handshake = Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e");
        await socket.SendAsync(
            handshake,
            WebSocketMessageType.Text,
            endOfMessage: true,
            timeout.Token);
        var responseBuffer = new byte[256];
        var response = await socket.ReceiveAsync(responseBuffer, timeout.Token);

        // Assert: server chấp nhận JWT, trả handshake thành công và giữ kết nối mở.
        Assert.Equal(WebSocketMessageType.Text, response.MessageType);
        Assert.Contains(
            "{}\u001e",
            Encoding.UTF8.GetString(responseBuffer, 0, response.Count),
            StringComparison.Ordinal);
        Assert.Equal(WebSocketState.Open, socket.State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-valid-jwt")]
    public async Task NotificationsHub_rejects_missing_or_invalid_token(string? accessToken)
    {
        // Arrange: khởi động đầy đủ pipeline nhưng không tạo credential hợp lệ.
        using var factory = new SpendWiseApiFactory();
        using var http = factory.CreateHttpsClient();
        var socketClient = factory.Server.CreateWebSocketClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var suffix = accessToken is null
            ? string.Empty
            : $"?access_token={Uri.EscapeDataString(accessToken)}";

        // Act và Assert: middleware [Authorize] từ chối trước khi Hub được kết nối.
        await Assert.ThrowsAnyAsync<Exception>(() => socketClient.ConnectAsync(
            new Uri($"ws://localhost/hubs/notifications{suffix}"),
            timeout.Token));
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client)
    {
        // Tạo danh tính riêng cho test Hub và không dùng token giả tự ký.
        client.DefaultRequestHeaders.Authorization = null;
        return await IdentityTestData.RegisterAndSetupPinAsync(
            client,
            "SignalR browser user");
    }
}
