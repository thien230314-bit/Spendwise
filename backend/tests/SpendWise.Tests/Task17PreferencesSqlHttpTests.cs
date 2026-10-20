using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Identity;
using Xunit;

namespace SpendWise.Tests;

[Trait("Category", "SqlServer")]
public sealed class Task17PreferencesSqlHttpTests
{
    [Theory]
    [InlineData("light", "vi-VN")]
    [InlineData("dark", "en-US")]
    [InlineData("system", "vi-VN")]
    public async Task Preferences_HTTP_SQL02_survive_account_switch_and_fresh_login(string theme, string language)
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync();
        var first = await IdentityTestData.RegisterAndSetupPinAsync(f.Client, "Synthetic preferences A");
        var second = await IdentityTestData.RegisterAndSetupPinAsync(f.Client, "Synthetic preferences B");
        CoreAcceptanceHttpTests.Authorize(f.Client, first.AccessToken);
        using var update = await f.Client.PutAsJsonAsync("/api/users/me/preferences", new UpdatePreferencesRequest(language, theme, "VND"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        CoreAcceptanceHttpTests.Authorize(f.Client, second.AccessToken);
        var other = await f.Client.GetFromJsonAsync<UserProfile>("/api/users/me");
        Assert.Equal("system", other!.PreferredTheme); Assert.Equal("vi-VN", other.PreferredLanguage);
        var login = await IdentityTestData.LoginAndVerifyPinAsync(f.Client, first.User.Email);
        Assert.Equal(theme, login.User.PreferredTheme); Assert.Equal(language, login.User.PreferredLanguage);
        CoreAcceptanceHttpTests.Authorize(f.Client, login.AccessToken);
        var read = await f.Client.GetFromJsonAsync<UserProfile>("/api/users/me");
        Assert.Equal(theme, read!.PreferredTheme); Assert.Equal(language, read.PreferredLanguage);
        foreach (var invalid in new[] { new UpdatePreferencesRequest("fr-FR", theme, "VND"), new UpdatePreferencesRequest(language, "invalid", "VND") })
        { using var denied = await f.Client.PutAsJsonAsync("/api/users/me/preferences", invalid); Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode); }
        await using var db = f.Context(); var saved = await db.Users.SingleAsync(u => u.Id == first.User.Id);
        Assert.Equal(theme, saved.PreferredTheme); Assert.Equal(language, saved.PreferredLanguage);
    }
}
