using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class MobilePushHttpTests
{
    private const string FirstToken = "ExpoPushToken[aaaaaaaaaaaaaaaaaaaaaa]";
    private const string ReplacementToken = "ExpoPushToken[bbbbbbbbbbbbbbbbbbbbbb]";

    [Fact]
    public async Task User_registers_and_replaces_installation_idempotently_from_jwt()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Push owner");
        UseToken(client, user.AccessToken);
        var installationId = Guid.NewGuid();

        var created = await client.PutAsJsonAsync(
            $"/api/mobile/push/installations/{installationId}",
            new { platform = "Android", pushToken = FirstToken, deviceName = "Pixel" });
        var replaced = await client.PutAsJsonAsync(
            $"/api/mobile/push/installations/{installationId}",
            new { platform = "Ios", pushToken = ReplacementToken, deviceName = "iPhone" });

        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, replaced.StatusCode);
        Assert.DoesNotContain(FirstToken, await created.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain(ReplacementToken, await replaced.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var installation = await db.MobilePushInstallations.SingleAsync();
        Assert.Equal(installationId, installation.Id);
        Assert.Equal(user.User.Id, installation.UserId);
        Assert.Equal("Ios", installation.Platform.ToString());
        Assert.Equal(ReplacementToken, installation.PushToken);
        Assert.Equal("iPhone", installation.DeviceName);
        Assert.True(installation.IsActive);
    }

    [Theory]
    [InlineData("Windows", FirstToken, "Phone")]
    [InlineData("Android", "not-an-expo-token", "Phone")]
    [InlineData("Ios", FirstToken, "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task Invalid_platform_token_or_device_name_is_rejected(
        string platform,
        string pushToken,
        string deviceName)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Invalid push input");
        UseToken(client, user.AccessToken);

        var response = await client.PutAsJsonAsync(
            $"/api/mobile/push/installations/{Guid.NewGuid()}",
            new { platform, pushToken, deviceName });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(pushToken, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().MobilePushInstallations);
    }

    [Fact]
    public async Task SystemAdmin_cannot_register_mobile_push_installation()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var candidate = await RegisterAsync(client, "Push admin");
        await PromoteToAdminAsync(factory, candidate.User.Id);
        var admin = await IdentityTestData.LoginAndVerifyPinAsync(
            client,
            candidate.User.Email,
            "StrongPass1");
        UseToken(client, admin.AccessToken);

        var response = await client.PutAsJsonAsync(
            $"/api/mobile/push/installations/{Guid.NewGuid()}",
            new { platform = "Android", pushToken = FirstToken, deviceName = "Admin phone" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().MobilePushInstallations);
    }

    [Fact]
    public async Task Only_owner_can_unregister_and_delete_is_idempotent()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await RegisterAsync(client, "Push delete owner");
        var other = await RegisterAsync(client, "Push delete other");
        var installationId = Guid.NewGuid();
        UseToken(client, owner.AccessToken);
        (await client.PutAsJsonAsync(
            $"/api/mobile/push/installations/{installationId}",
            new { platform = "Android", pushToken = FirstToken, deviceName = "Owner phone" }))
            .EnsureSuccessStatusCode();

        UseToken(client, other.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/mobile/push/installations/{installationId}")).StatusCode);
        using (var otherScope = factory.Services.CreateScope())
        {
            var unchanged = await otherScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
                .MobilePushInstallations.AsNoTracking().SingleAsync();
            Assert.True(unchanged.IsActive);
            Assert.Equal(owner.User.Id, unchanged.UserId);
        }

        UseToken(client, owner.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/mobile/push/installations/{installationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/mobile/push/installations/{installationId}")).StatusCode);
        using var ownerScope = factory.Services.CreateScope();
        var revoked = await ownerScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
            .MobilePushInstallations.AsNoTracking().SingleAsync();
        Assert.False(revoked.IsActive);
        Assert.Null(revoked.PushToken);
        Assert.Null(revoked.DeviceName);

        UseToken(client, other.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync(
                $"/api/mobile/push/installations/{installationId}",
                new { platform = "Ios", pushToken = ReplacementToken, deviceName = "Other phone" }))
            .StatusCode);
        ownerScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().ChangeTracker.Clear();
        var reassigned = await ownerScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
            .MobilePushInstallations.AsNoTracking().SingleAsync();
        Assert.Equal(other.User.Id, reassigned.UserId);
        Assert.True(reassigned.IsActive);
        Assert.Equal(ReplacementToken, reassigned.PushToken);
    }

    private static async Task<SpendWise.Application.Identity.AuthResponse> RegisterAsync(
        HttpClient client,
        string name)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return await IdentityTestData.RegisterAndSetupPinAsync(client, name);
    }

    private static void UseToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task PromoteToAdminAsync(SpendWiseApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await db.Users.FindAsync(userId);
        user!.SystemRole = SystemRole.SystemAdmin;
        await db.SaveChangesAsync();
    }
}
