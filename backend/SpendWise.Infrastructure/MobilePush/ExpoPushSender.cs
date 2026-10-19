using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpendWise.Application.MobilePush;

namespace SpendWise.Infrastructure.MobilePush;

public sealed class ExpoPushSender(HttpClient httpClient) : IExpoPushSender
{
    // Official Expo Push Service HTTP endpoint; no credential is required by default.
    private static readonly Uri SendEndpoint = new("https://exp.host/--/api/v2/push/send");
    private static readonly Uri ReceiptsEndpoint = new("https://exp.host/--/api/v2/push/getReceipts");

    public async Task<ExpoPushSendResult> SendAsync(
        ExpoPushMessage message,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
        {
            Content = JsonContent.Create(new
            {
                to = message.PushToken,
                title = "SpendWise",
                body = "Bạn có thông báo mới trong SpendWise.",
                data = new
                {
                    notificationId = message.NotificationId.ToString(),
                    notificationType = message.NotificationType
                }
            })
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExpoPushTransientException();
        }
        catch (HttpRequestException)
        {
            throw new ExpoPushTransientException();
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests
                || response.StatusCode == HttpStatusCode.RequestTimeout
                || (int)response.StatusCode >= 500)
            {
                throw new ExpoPushTransientException();
            }

            if (!response.IsSuccessStatusCode)
                return new ExpoPushSendResult(ExpoPushSendStatus.PermanentFailure);

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (!document.RootElement.TryGetProperty("data", out var data))
                    throw new ExpoPushTransientException();
                var ticket = data.ValueKind == JsonValueKind.Array
                    ? data.EnumerateArray().FirstOrDefault()
                    : data;
                if (ticket.ValueKind != JsonValueKind.Object
                    || !ticket.TryGetProperty("status", out var status))
                {
                    throw new ExpoPushTransientException();
                }

                if (string.Equals(status.GetString(), "ok", StringComparison.Ordinal))
                {
                    if (!ticket.TryGetProperty("id", out var id)
                        || string.IsNullOrWhiteSpace(id.GetString()))
                    {
                        throw new ExpoPushTransientException();
                    }
                    return new ExpoPushSendResult(ExpoPushSendStatus.Submitted, id.GetString());
                }

                var providerError = ticket.TryGetProperty("details", out var details)
                    && details.ValueKind == JsonValueKind.Object
                    && details.TryGetProperty("error", out var error)
                        ? error.GetString()
                        : null;
                return string.Equals(providerError, "DeviceNotRegistered", StringComparison.Ordinal)
                    ? new ExpoPushSendResult(ExpoPushSendStatus.DeviceNotRegistered)
                    : new ExpoPushSendResult(ExpoPushSendStatus.PermanentFailure);
            }
            catch (JsonException)
            {
                throw new ExpoPushTransientException();
            }
        }
    }

    public async Task<IReadOnlyDictionary<string, ExpoPushReceiptResult>> GetReceiptsAsync(
        IReadOnlyCollection<string> receiptIds,
        CancellationToken cancellationToken)
    {
        if (receiptIds.Count is 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(receiptIds));

        using var request = new HttpRequestMessage(HttpMethod.Post, ReceiptsEndpoint)
        {
            Content = JsonContent.Create(new { ids = receiptIds })
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExpoPushTransientException();
        }
        catch (HttpRequestException)
        {
            throw new ExpoPushTransientException();
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests
                || response.StatusCode == HttpStatusCode.RequestTimeout
                || (int)response.StatusCode >= 500)
            {
                throw new ExpoPushTransientException();
            }

            if (!response.IsSuccessStatusCode)
                throw new ExpoPushReceiptPermanentException();

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.TryGetProperty("errors", out var errors)
                    && errors.ValueKind == JsonValueKind.Array
                    && errors.GetArrayLength() > 0)
                {
                    throw new ExpoPushReceiptPermanentException();
                }
                if (!document.RootElement.TryGetProperty("data", out var data)
                    || data.ValueKind != JsonValueKind.Object)
                {
                    throw new ExpoPushTransientException();
                }

                var requested = receiptIds.ToHashSet(StringComparer.Ordinal);
                var results = new Dictionary<string, ExpoPushReceiptResult>(StringComparer.Ordinal);
                foreach (var property in data.EnumerateObject())
                {
                    if (!requested.Contains(property.Name))
                        continue;

                    var receipt = property.Value;
                    if (receipt.ValueKind != JsonValueKind.Object
                        || !receipt.TryGetProperty("status", out var status))
                    {
                        results[property.Name] = new(ExpoPushReceiptStatus.PermanentFailure);
                        continue;
                    }

                    if (string.Equals(status.GetString(), "ok", StringComparison.Ordinal))
                    {
                        results[property.Name] = new(ExpoPushReceiptStatus.Succeeded);
                        continue;
                    }

                    var providerError = receipt.TryGetProperty("details", out var details)
                        && details.ValueKind == JsonValueKind.Object
                        && details.TryGetProperty("error", out var error)
                            ? error.GetString()
                            : null;
                    results[property.Name] = new(
                        string.Equals(providerError, "DeviceNotRegistered", StringComparison.Ordinal)
                            ? ExpoPushReceiptStatus.DeviceNotRegistered
                            : ExpoPushReceiptStatus.PermanentFailure);
                }
                return results;
            }
            catch (JsonException)
            {
                throw new ExpoPushTransientException();
            }
        }
    }
}
