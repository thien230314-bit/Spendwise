using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class OtpAntiEnumerationHttpTests
{
    [Fact]
    public async Task Forgot_password_business_IP_quota_is_neutral_without_OTP_or_email_side_effects()
    {
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["EmailVerification:MaximumSendsPerHourPerIp"] = "1"
        });
        using var client = factory.CreateHttpsClient();
        var firstEmail = await SeedUserAsync(factory);
        var otherEmail = await SeedUserAsync(factory);
        var first = await SendPeerAsync(factory, firstEmail);
        Assert.Equal(HttpStatusCode.Accepted, first.Status);
        using var body = System.Text.Json.JsonDocument.Parse(first.Body);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            Assert.NotNull((await db.EmailVerificationTokens.SingleAsync()).RequestIpHash);
            Assert.Single(await db.EmailOutboxMessages.ToListAsync());
        }
        var before = await SnapshotAsync(factory);
        // Another eligible account has no account cooldown/quota: this denial is IP-only.
        var blocked = await SendPeerAsync(factory, otherEmail);
        var unknown = await SendPeerAsync(factory, "unknown-ip-quota@test.local");
        Assert.Equal(HttpStatusCode.Accepted, blocked.Status);
        Assert.Equal(HttpStatusCode.Accepted, unknown.Status);
        Assert.Equal(first.Body, blocked.Body);
        Assert.Equal(first.Body, unknown.Body);
        Assert.Equal(before, await SnapshotAsync(factory));
    }

    private static async Task<(HttpStatusCode Status, string Body)> SendPeerAsync(
        SpendWiseApiFactory factory, string email)
    {
        using var requestBody = new MemoryStream(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            new ForgotPasswordRequest(email)));
        using var responseBody = new MemoryStream();
        var context = await factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.100");
            context.Request.Scheme = "https";
            context.Request.Method = "POST";
            context.Request.Path = "/api/auth/forgot-password";
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = requestBody.Length;
            context.Request.Body = requestBody;
            context.Response.Body = responseBody;
        });
        return ((HttpStatusCode)context.Response.StatusCode,
            System.Text.Encoding.UTF8.GetString(responseBody.ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Forgot_password_account_cooldown_or_quota_has_same_accepted_body_as_unknown(bool hourlyQuota)
    {
        using var factory = new SpendWiseApiFactory(hourlyQuota
            ? new Dictionary<string, string?> { ["EmailVerification:MaximumSendsPerHourPerAccount"] = "1" }
            : null);
        using var client = factory.CreateHttpsClient();
        var email = await SeedUserAsync(factory);
        var first = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var expectedBody = await first.Content.ReadAsStringAsync();
        var before = await SnapshotAsync(factory);

        var blocked = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest($"  {email.ToUpperInvariant()}  "));
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("not-registered@test.local"));

        Assert.Equal(HttpStatusCode.Accepted, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(expectedBody, await blocked.Content.ReadAsStringAsync());
        Assert.Equal(expectedBody, await unknown.Content.ReadAsStringAsync());
        Assert.Equal(before, await SnapshotAsync(factory));
    }

    [Fact]
    public async Task Forgot_password_http_ip_limit_is_identical_for_known_and_unknown_and_ignores_forged_headers()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = await SeedUserAsync(factory);
        for (var i = 0; i < 10; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/forgot-password")
            {
                Content = JsonContent.Create(new ForgotPasswordRequest(i % 2 == 0 ? email : "unknown@test.local"))
            };
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{i + 1}");
            request.Headers.Add("X-User-Id", Guid.NewGuid().ToString());
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
        var before = await SnapshotAsync(factory);
        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("unknown@test.local"));
        Assert.Equal(HttpStatusCode.TooManyRequests, known.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(before, await SnapshotAsync(factory));
    }

    private static async Task<string> SeedUserAsync(SpendWiseApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var unique = Guid.NewGuid().ToString("N");
        var email = $"{unique}@test.local";
        db.Users.Add(new User
        {
            FullName = "OTP anti-enumeration test", Email = email, NormalizedEmail = email.ToUpperInvariant(),
            SpendId = unique, NormalizedSpendId = unique.ToUpperInvariant(), Phone = IdentityTestData.NewPhone(),
            PasswordHash = "test-only-hash", EmailVerifiedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return email;
    }

    private static async Task<string> SnapshotAsync(SpendWiseApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var tokens = await db.EmailVerificationTokens.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var messages = await db.EmailOutboxMessages.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        return System.Text.Json.JsonSerializer.Serialize(new { tokens, messages });
    }
}
