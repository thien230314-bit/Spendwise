using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SpendWise.API.Infrastructure;
using SpendWise.Domain.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1PlatformAccessTests
{
    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public void V1_roles_exist_without_remapping_legacy_accounts(string name)
    {
        Assert.True(Enum.TryParse<SystemRole>(name, out var role));
        Assert.NotEqual(SystemRole.SystemAdmin, role);
        Assert.NotEqual(SystemRole.User, role);
        Assert.Equal(0, (int)SystemRole.SystemAdmin);
        Assert.Equal(1, (int)SystemRole.User);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task Platform_roles_cannot_use_User_business_APIs(string role)
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/wallets";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)], "test"));
        var invoked = false;
        var middleware = new BusinessAccessMiddleware(_ => { invoked = true; return Task.CompletedTask; });

        var error = await Assert.ThrowsAsync<SpendWise.Application.Common.Exceptions.ForbiddenException>(
            () => middleware.InvokeAsync(context, fixture.Db));

        Assert.Equal("user_role_required", error.Code);
        Assert.False(invoked);
    }
}
