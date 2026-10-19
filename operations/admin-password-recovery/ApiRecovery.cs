using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.AdminPasswordRecovery;

internal sealed class RecoveryApiClient : IDisposable
{
    private readonly HttpClient client;
    public RecoveryApiClient(HttpMessageHandler? handler = null)
    {
        client = new HttpClient(handler ?? new HttpClientHandler
            { AllowAutoRedirect = false, UseProxy = false, UseCookies = false })
        { BaseAddress = new Uri("http://localhost:5159"), Timeout = TimeSpan.FromSeconds(45) };
    }

    public async Task<HttpStatusCode> ForgotAsync(CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new { identifier = RecoverySession.Email }, ct);
        return response.StatusCode;
    }

    public async Task<HttpStatusCode> ResetAsync(string otp, string password, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { identifier = RecoverySession.Email, otp, newPassword = password }, ct);
        return response.StatusCode;
    }
    public void Dispose() => client.Dispose();
}

internal static class ApiRecovery
{
    public static Guid RequireSingleNewId(IEnumerable<Guid> before, IEnumerable<Guid> after)
    {
        var created = after.Except(before).ToArray();
        if (created.Length != 1)
            throw new InvalidOperationException("Cannot uniquely identify the OTP from this request.");
        return created[0];
    }

    public static async Task IssueAndDeliverAsync(IServiceProvider provider, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        await db.Database.OpenConnectionAsync(ct);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME()";
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.GetString(0) != RecoveryServices.Server
                || reader.GetString(1) != RecoveryServices.Database)
                throw new InvalidOperationException("Unexpected connected database.");
        }
        var session = scope.ServiceProvider.GetRequiredService<RecoverySession>();
        var userId = await session.IdentifyAsync(ct);
        // IDs only: never retrieve/print a raw OTP, hash or email body during correlation.
        var before = await db.EmailVerificationTokens.AsNoTracking()
            .Where(x => x.UserId == userId && x.Purpose == EmailVerificationPurpose.ResetPassword)
            .Select(x => x.Id).ToArrayAsync(ct);
        var started = DateTime.UtcNow;
        using var api = new RecoveryApiClient();
        var status = await api.ForgotAsync(ct); // Exactly one HTTP request; never automatically retry.
        Console.WriteLine($"Forgot Password API HTTP {(int)status}.");
        if (status != HttpStatusCode.Accepted)
            throw new InvalidOperationException("Forgot API did not accept the request.");
        var after = await db.EmailVerificationTokens.AsNoTracking()
            .Where(x => x.UserId == userId && x.Purpose == EmailVerificationPurpose.ResetPassword)
            .Select(x => new { x.Id, x.CreatedAt, x.ExpiresAt, x.UsedAt, x.RevokedAt }).ToListAsync(ct);
        var tokenId = RequireSingleNewId(before, after.Select(x => x.Id));
        var token = after.Single(x => x.Id == tokenId);
        if (token.CreatedAt < started.AddSeconds(-1) || token.ExpiresAt <= DateTime.UtcNow
            || token.UsedAt != null || token.RevokedAt != null)
            throw new InvalidOperationException("New OTP is not eligible.");
        var key = $"email:password-reset-otp:{tokenId:N}";
        var messages = await db.EmailOutboxMessages.AsNoTracking().Where(x => x.DeduplicationKey == key)
            .Select(x => new { x.Id, x.RecipientEmail, x.DeduplicationKey, x.Status, x.AttemptCount }).ToListAsync(ct);
        if (messages.Count != 1 || messages[0].RecipientEmail != RecoverySession.Email
            || messages[0].Status != EmailDeliveryStatus.Pending || messages[0].AttemptCount != 0)
            throw new InvalidOperationException("New outbox identity/state is ambiguous or changed.");
        Console.WriteLine("Exactly one new OTP and matching Pending outbox verified; recipient/key match.");
        var ticket = new RecoveryTicket(userId, tokenId, messages[0].Id, key);
        using var deliveryDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deliveryDeadline.CancelAfter(TimeSpan.FromSeconds(45));
        if (!await session.DeliverAsync(ticket, deliveryDeadline.Token))
            throw new InvalidOperationException("Targeted SMTP send not confirmed; do not retry automatically.");
        Console.WriteLine("Targeted OTP delivery: SMTP SENT; persisted Sent by the existing dispatcher. Backlog not dispatched.");
        Console.WriteLine("Gmail receipt: WAITING FOR USER. STOP. Reset Password API: NOT EXECUTED.");
    }

    // Operations-only self-check: fake HTTP handler, no SQL/SMTP/configuration or real secrets.
    public static async Task SelfTestAsync()
    {
        var fake = new FakeHttp();
        using var client = new RecoveryApiClient(fake);
        if (await client.ForgotAsync(default) != HttpStatusCode.Accepted
            || await client.ResetAsync("12345678", "SyntheticPassword2026", default) != HttpStatusCode.NoContent
            || await client.ForgotAsync(default) != HttpStatusCode.TemporaryRedirect || fake.Count != 3)
            throw new InvalidOperationException("API transport self-check failed.");
        var old = Guid.NewGuid(); var created = Guid.NewGuid();
        if (RequireSingleNewId([old], [old, created]) != created)
            throw new InvalidOperationException("Unique identity self-check failed.");
        foreach (var candidates in new[] { new[] { old }, new[] { old, created, Guid.NewGuid() } })
        {
            var rejected = false;
            try { RequireSingleNewId([old], candidates); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Ambiguous identity accepted.");
        }
        Console.WriteLine("API transport/correlation self-check: 6 PASS / 0 FAIL. No SQL/SMTP or real API requests.");
    }

    private sealed class FakeHttp : HttpMessageHandler
    {
        public int Count;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Count++;
            if (request.Method != HttpMethod.Post || request.RequestUri?.Host != "localhost"
                || request.RequestUri.Port != 5159 || request.Headers.Authorization != null)
                throw new InvalidOperationException("Unexpected API destination or authorization.");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            if (body.RootElement.GetProperty("identifier").GetString() != RecoverySession.Email)
                throw new InvalidOperationException("Unexpected account.");
            if (Count == 2)
            {
                if (request.RequestUri.AbsolutePath != "/api/auth/reset-password"
                    || body.RootElement.GetProperty("otp").GetString() != "12345678"
                    || body.RootElement.GetProperty("newPassword").GetString() != "SyntheticPassword2026")
                    throw new InvalidOperationException("Unexpected reset payload.");
            }
            else if (request.RequestUri.AbsolutePath != "/api/auth/forgot-password")
                throw new InvalidOperationException("Unexpected forgot endpoint.");
            return new HttpResponseMessage(Count == 1 ? HttpStatusCode.Accepted
                : Count == 2 ? HttpStatusCode.NoContent : HttpStatusCode.TemporaryRedirect);
        }
    }
}
