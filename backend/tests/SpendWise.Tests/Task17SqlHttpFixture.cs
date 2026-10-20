using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

internal sealed class Task17Clock : IClock { public DateTime UtcNow { get; set; } = DateTime.UtcNow; }

internal sealed class Task17SqlHttpFixture : IAsyncDisposable
{
    private readonly string connection = SqlTestFixtureLifecycle.CreateOwnedConnectionString(
        "SpendWise_FinancialS05_Test_" + Guid.NewGuid().ToString("N"), Environment.GetEnvironmentVariable(SqlTestFixtureLifecycle.InstanceEnvironmentVariable));
    public Task17Clock Clock { get; } = new();
    public SpendWiseApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public SpendWiseDbContext Context(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connection).AddInterceptors(interceptors).Options, Clock);
    public static async Task<Task17SqlHttpFixture> CreateAsync(bool kestrel = false)
    {
        var f = new Task17SqlHttpFixture(); await SqlTestFixtureLifecycle.CreateOwnedCatalogAsync(f.connection);
        return await SqlTestFixtureLifecycle.CreateAsync(async () =>
        {
            await using var db = f.Context(); await db.Database.EnsureCreatedAsync();
            await SqlTestFixtureLifecycle.ConfigureOwnedCatalogAsync(f.connection);
            f.Factory = new(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthPermitLimit"] = kestrel ? "1000" : "100", ["RateLimiting:FinancialPermitLimit"] = kestrel ? "1000" : "200",
                ["Email:DeliveryMode"] = "Pickup", ["PasswordReset:PickupDirectory"] = Path.Combine(Path.GetTempPath(), "spendwise-task17", "pickup"),
                ["SePayBankHub:Enabled"] = kestrel ? "false" : "true", ["Gemini:ApiKey"] = "",
                ["V1:Activity:CoverageVerified"] = "true", ["V1:Activity:CoverageStartsAtUtc"] = f.Clock.UtcNow.AddDays(-1).ToString("O"),
                ["V1:Broadcast:Version"] = "Task17-fixture", ["V1:Broadcast:MaxRecipients"] = "100", ["V1:Broadcast:MinSpacingSeconds"] = "60",
                ["V1:MaintenanceReminders:Version"] = "Task17-fixture", ["V1:MaintenanceReminders:MaximumCount"] = "3",
                ["V1:MaintenanceReminders:MinimumSpacingMinutes"] = "10", ["V1:MaintenanceReminders:LateToleranceSeconds"] = "60"
            }, clock: f.Clock, sqlConnection: f.connection);
            if (kestrel)
            {
                f.Factory.UseKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0));
                f.Client = f.Factory.CreateClient(new() { AllowAutoRedirect = false });
            }
            else f.Client = f.Factory.CreateHttpsClient();
            return f;
        }, f.DisposeAsync);
    }
    public async Task ActorAsync(User user)
    {
        await using var db = Context(); var session = new UserSession { UserId = user.Id, LastSeenAt = Clock.UtcNow };
        db.UserSessions.Add(session); await db.SaveChangesAsync();
        using var scope = Factory.Services.CreateScope();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, session.Id).Token);
    }
    public async ValueTask DisposeAsync()
    {
        Client?.Dispose(); Factory?.Dispose();
        await SqlTestFixtureLifecycle.VerifyCatalogOwnerAsync(connection);
        await using var db = Context(); await db.Database.EnsureDeletedAsync();
    }
}
