using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class PinRecoveryDeliveryHttpTests
{
    [Theory]
    [InlineData("unknown")][InlineData("inactive")][InlineData("unverified")]
    public async Task PIN_forgot_is_neutral_without_challenge_for_ineligible_accounts(string condition)
    {
        using var factory=Factory(); using var client=factory.CreateHttpsClient();
        var email=await SeedAsync(factory);
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();var user=await db.Users.SingleAsync();
            if(condition=="inactive")user.Status=UserStatus.Locked;
            if(condition=="unverified")user.EmailVerifiedAt=null;
            await db.SaveChangesAsync();
        }
        var response=await client.PostAsJsonAsync("/api/auth/pin/forgot",new ForgotSecurityPinRequest(condition=="unknown"?"absent@fixture.invalid":email));
        var unknown=await client.PostAsJsonAsync("/api/auth/pin/forgot",new ForgotSecurityPinRequest("other@fixture.invalid"));
        Assert.Equal(HttpStatusCode.Accepted,response.StatusCode); Assert.Equal(unknown.StatusCode,response.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(),await response.Content.ReadAsStringAsync());
        using var check=factory.Services.CreateScope();var final=check.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Empty(await final.SecurityPinResetTokens.ToListAsync());Assert.Empty(await final.EmailOutboxMessages.ToListAsync());
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task PIN_cooldown_and_account_quota_keep_neutral_response_and_leave_state_untouched(bool quota)
    {
        using var factory=Factory();using var client=factory.CreateHttpsClient();var email=await SeedAsync(factory);
        await client.PostAsJsonAsync("/api/auth/pin/forgot",new ForgotSecurityPinRequest(email));
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();var old=await db.SecurityPinResetTokens.SingleAsync();
            if(quota)
            {
                old.CreatedAt=DateTime.UtcNow.AddMinutes(-2);
                for(var i=0;i<4;i++)db.SecurityPinResetTokens.Add(new SecurityPinResetToken{UserId=old.UserId,
                    TokenHash=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N"),Purpose=SecurityPinResetPurpose.SelfService,
                    CreatedAt=DateTime.UtcNow.AddMinutes(-3),ExpiresAt=DateTime.UtcNow.AddMinutes(10)});
                await db.SaveChangesAsync();
            }
        }
        var blocked=await client.PostAsJsonAsync("/api/auth/pin/forgot",new ForgotSecurityPinRequest(email));
        var unknown=await client.PostAsJsonAsync("/api/auth/pin/forgot",new ForgotSecurityPinRequest("absent@fixture.invalid"));
        Assert.Equal(HttpStatusCode.Accepted,blocked.StatusCode);Assert.Equal(await unknown.Content.ReadAsStringAsync(),await blocked.Content.ReadAsStringAsync());
        using var check=factory.Services.CreateScope();var final=check.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(quota?5:1,await final.SecurityPinResetTokens.CountAsync());
        Assert.All(await final.SecurityPinResetTokens.ToListAsync(),x=>{Assert.Null(x.UsedAt);Assert.Null(x.RevokedAt);});
        Assert.Single(await final.EmailOutboxMessages.ToListAsync());
    }

    [Theory]
    [InlineData("expired")][InlineData("used")][InlineData("revoked")][InlineData("inactive")]
    public async Task Invalid_PIN_challenge_cannot_reset_or_validate(string state)
    {
        using var factory=Factory();using var client=factory.CreateHttpsClient();var email=await SeedAsync(factory);
        await client.PostAsJsonAsync("/api/auth/pin/forgot",new ForgotSecurityPinRequest(email));
        var raw=await ReadTokenAsync(factory);
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();var token=await db.SecurityPinResetTokens.SingleAsync();
            if(state=="expired")token.ExpiresAt=DateTime.UtcNow.AddSeconds(-1);
            if(state=="used")token.UsedAt=DateTime.UtcNow;
            if(state=="revoked")token.RevokedAt=DateTime.UtcNow;
            if(state=="inactive")(await db.Users.SingleAsync()).Status=UserStatus.Locked;
            await db.SaveChangesAsync();
        }
        var valid=await client.PostAsJsonAsync("/api/auth/pin/reset/validate",new ValidateSecurityPinResetTokenRequest(raw));
        Assert.Equal(HttpStatusCode.OK,valid.StatusCode);Assert.False((await valid.Content.ReadFromJsonAsync<SecurityPinResetTokenStatusDto>())!.Valid);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/pin/reset",new ResetSecurityPinRequest(raw,"654321","654321"))).StatusCode);
        using var check=factory.Services.CreateScope();var user=await check.ServiceProvider.GetRequiredService<SpendWiseDbContext>().Users.SingleAsync();
        Assert.True(new SecurityPinService().Verify("123456",user.SecurityPinHash!));
        Assert.True(new PasswordService().Verify("SyntheticPassword2026",user.PasswordHash));
    }

    private static SpendWiseApiFactory Factory()=>new(new Dictionary<string,string?>{["Email:WebsiteUrl"]="http://localhost:3000"});
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task PIN_forgot_IP_limiter_returns_429_for_known_and_unknown_equally(bool known)
    {
        using var factory=Factory();using var client=factory.CreateHttpsClient();var email=await SeedAsync(factory);
        for(var i=0;i<10;i++)
            Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync("/api/auth/pin/forgot",
                new ForgotSecurityPinRequest(known?email:"absent@fixture.invalid"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await client.PostAsJsonAsync("/api/auth/pin/forgot",
            new ForgotSecurityPinRequest(known?email:"absent@fixture.invalid"))).StatusCode);
    }

    [Theory]
    [InlineData("12345","12345")][InlineData("123456","654321")]
    public async Task Invalid_new_PIN_or_confirmation_does_not_consume_proof(string pin,string confirm)
    {
        using var factory=Factory();using var client=factory.CreateHttpsClient();var email=await SeedAsync(factory);
        await client.PostAsJsonAsync("/api/auth/pin/forgot",new ForgotSecurityPinRequest(email));var raw=await ReadTokenAsync(factory);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/auth/pin/reset",new ResetSecurityPinRequest(raw,pin,confirm))).StatusCode);
        using var check=factory.Services.CreateScope();var db=check.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Null((await db.SecurityPinResetTokens.SingleAsync()).UsedAt);
        Assert.True(new SecurityPinService().Verify("123456",(await db.Users.SingleAsync()).SecurityPinHash!));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task PIN_replacement_invalidates_old_email_or_rolls_back_everything_if_queue_fails(bool failQueue)
    {
        using var factory=Factory();using var client=factory.CreateHttpsClient();var email=await SeedAsync(factory);
        await client.PostAsJsonAsync("/api/auth/pin/forgot",new ForgotSecurityPinRequest(email));
        Guid oldId;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();var token=await db.SecurityPinResetTokens.SingleAsync();
            oldId=token.Id;token.CreatedAt=DateTime.UtcNow.AddMinutes(-2);await db.SaveChangesAsync();
            if(failQueue)await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER synthetic_pin_queue_failure BEFORE INSERT ON EmailOutboxMessages BEGIN SELECT RAISE(ABORT, 'synthetic queue failure'); END;");
        }
        Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync("/api/auth/pin/forgot",new ForgotSecurityPinRequest(email))).StatusCode);
        using var check=factory.Services.CreateScope();var final=check.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(failQueue?1:2,await final.SecurityPinResetTokens.CountAsync());
        Assert.Equal(!failQueue,(await final.SecurityPinResetTokens.SingleAsync(x=>x.Id==oldId)).RevokedAt!=null);
        var oldMail=await final.EmailOutboxMessages.OrderBy(x=>x.CreatedAt).FirstAsync();
        Assert.Equal(failQueue?SpendWise.Domain.Notifications.EmailDeliveryStatus.Pending:SpendWise.Domain.Notifications.EmailDeliveryStatus.Failed,oldMail.Status);
        Assert.Equal(failQueue,!string.IsNullOrEmpty(oldMail.TextBody));
    }

    private static async Task<string> SeedAsync(SpendWiseApiFactory factory)
    {
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var email=$"admin-{Guid.NewGuid():N}@fixture.invalid";
        db.Users.Add(new User{FullName="Synthetic Admin",Email=email,NormalizedEmail=email.ToUpperInvariant(),
            SpendId=Guid.NewGuid().ToString("N"),NormalizedSpendId=Guid.NewGuid().ToString("N"),
            SystemRole=SystemRole.SystemAdmin,EmailVerifiedAt=DateTime.UtcNow,
            PasswordHash=new PasswordService().Hash("SyntheticPassword2026"),SecurityPinHash=new SecurityPinService().Hash("123456")});
        await db.SaveChangesAsync();return email;
    }
    private static async Task<string> ReadTokenAsync(SpendWiseApiFactory factory)
    {
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var row=await db.EmailOutboxMessages.SingleAsync();
        var text=scope.ServiceProvider.GetRequiredService<IEmailContentProtector>().Unprotect(row.TextBody);
        var match=Regex.Match(text,@"http://localhost:3000/reset-pin\?token=([^\s]+)");Assert.True(match.Success);
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    [Fact]
    public async Task Admin_forgot_PIN_queues_real_link_then_single_use_reset_preserves_password_role_and_revokes_sessions()
    {
        using var factory = new SpendWiseApiFactory(new Dictionary<string,string?> { ["Email:WebsiteUrl"]="http://localhost:3000" });
        using var client = factory.CreateHttpsClient();
        string email; string passwordHash; string oldPinHash;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            email=$"admin-{Guid.NewGuid():N}@fixture.invalid";
            passwordHash=new PasswordService().Hash("SyntheticPassword2026");
            oldPinHash=new SecurityPinService().Hash("123456");
            var user=new User {FullName="Synthetic Admin",Email=email,NormalizedEmail=email.ToUpperInvariant(),
                SpendId=Guid.NewGuid().ToString("N"),NormalizedSpendId=Guid.NewGuid().ToString("N"),
                SystemRole=SystemRole.SystemAdmin,EmailVerifiedAt=DateTime.UtcNow,PasswordHash=passwordHash,SecurityPinHash=oldPinHash};
            db.Users.Add(user); await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<SpendWise.Application.Features.Authentication.AuthenticationSupport>()
                .IssueAsync(user,"synthetic-PIN-session",null,null,default);
        }
        Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync("/api/auth/pin/forgot",new ForgotSecurityPinRequest(email))).StatusCode);
        string token;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var row=Assert.Single(await db.EmailOutboxMessages.ToListAsync());
            var plain=scope.ServiceProvider.GetRequiredService<IEmailContentProtector>().Unprotect(row.TextBody);
            var match=Regex.Match(plain,@"http://localhost:3000/reset-pin\?token=([^\s]+)");
            Assert.True(match.Success,"Queued PIN email must include the actual Frontend recovery link.");
            token=Uri.UnescapeDataString(match.Groups[1].Value);
            Assert.Equal(oldPinHash,(await db.Users.SingleAsync()).SecurityPinHash);
            Assert.NotEqual(token,(await db.SecurityPinResetTokens.SingleAsync()).TokenHash);
        }
        var valid=await client.PostAsJsonAsync("/api/auth/pin/reset/validate",new ValidateSecurityPinResetTokenRequest(token));
        Assert.True((await valid.Content.ReadFromJsonAsync<SecurityPinResetTokenStatusDto>())!.Valid);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/pin/reset",new ResetSecurityPinRequest("wrong-synthetic-token","654321","654321"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/auth/pin/reset",new ResetSecurityPinRequest(token,"654321","654321"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/pin/reset",new ResetSecurityPinRequest(token,"111111","111111"))).StatusCode);
        using var check=factory.Services.CreateScope();
        var final=check.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var saved=await final.Users.SingleAsync();
        Assert.Equal(passwordHash,saved.PasswordHash); Assert.Equal(email,saved.Email); Assert.Equal(SystemRole.SystemAdmin,saved.SystemRole);
        Assert.True(new SecurityPinService().Verify("654321",saved.SecurityPinHash!));
        Assert.All(await final.UserSessions.ToListAsync(),x=>Assert.NotNull(x.RevokedAt));
        Assert.All(await final.RefreshTokens.ToListAsync(),x=>Assert.NotNull(x.RevokedAt));
    }
}
