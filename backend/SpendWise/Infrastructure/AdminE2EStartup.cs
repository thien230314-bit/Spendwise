using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.API.Infrastructure;

// Explicitly local, opt-in startup profile; all authentication/authorization remains unchanged.
public static class AdminE2EStartup
{
    public const string EnvironmentName = "AdminE2E";
    public const string Origin = "http://localhost:3000";
    private const string Server = @"DESKTOP-B5KA3LT\SQLEXPRESS01";
    private const string Database = "SpendWiseDb";

    public static void LoadConfiguration(WebApplicationBuilder builder, string[] args)
    {
        if (!builder.Environment.IsEnvironment(EnvironmentName)) return;
        builder.Configuration.AddUserSecrets("SpendWise-Development", reloadOnChange: false)
            .AddEnvironmentVariables().AddCommandLine(args);
    }

    public static void ConfigureRuntime(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsEnvironment(EnvironmentName)) return;
        var connection = new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
        if (connection.DataSource != Server || connection.InitialCatalog != Database
            || connection.AttachDBFilename.Length != 0 || connection.UserInstance)
            throw new InvalidOperationException("AdminE2E requires the approved SQL01/SpendWiseDb target.");
        _ = JwtSigningKey.Validate(builder.Configuration["Jwt:Key"] ?? string.Empty);
        if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"])
            || string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]))
            throw new InvalidOperationException("AdminE2E requires configured JWT issuer and audience.");
        if (builder.Configuration.GetSection("Kestrel:Endpoints").GetChildren().Any())
            throw new InvalidOperationException("AdminE2E does not allow configured additional Kestrel endpoints.");

        // Keep the framework web-server host; remove only application workers and DP warm-up.
        var workers = builder.Services.Where(x => x.ServiceType == typeof(IHostedService)
            && (x.ImplementationType?.Assembly == typeof(EmailOutboxDispatcher).Assembly
                || x.ImplementationType?.FullName == "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService"))
            .ToArray();
        foreach (var worker in workers) builder.Services.Remove(worker);
        // Local recovery links must point to the exact approved Admin Web, not a stale WebsiteUrl secret.
        builder.Configuration["Email:WebsiteUrl"] = Origin;
        var recoveryScope = AuthRecoveryDeliveryScope.Read(builder.Configuration);
        if (recoveryScope is not null)
        {
            if (!string.Equals(builder.Configuration["Email:DeliveryMode"], "Smtp", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Scoped Admin recovery requires explicit SMTP mode.");
            new SpendWise.Infrastructure.Identity.ConfiguredEmailSender(builder.Configuration, builder.Environment,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SpendWise.Infrastructure.Identity.ConfiguredEmailSender>.Instance,
                new SpendWise.Infrastructure.Identity.EmailTemplateRenderer()).ValidateConfiguration();
            builder.Services.AddSingleton(recoveryScope);
            builder.Services.AddScoped<AuthRecoveryEmailDispatcher>();
            builder.Services.AddHostedService<AuthRecoveryEmailBackgroundService>();
        }
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5159));
    }

    public static async Task VerifyDatabaseAsync(IServiceProvider services)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            await db.Database.OpenConnectionAsync(deadline.Token);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME()";
            await using var reader = await command.ExecuteReaderAsync(deadline.Token);
            if (!await reader.ReadAsync(deadline.Token) || reader.GetString(0) != Server || reader.GetString(1) != Database)
                throw new InvalidOperationException("Connected database identity mismatch.");
        }
        catch
        {
            // Do not expose connection/provider details or credentials in startup output.
            throw new InvalidOperationException("AdminE2E read-only database preflight failed; no migration/seeding was attempted.");
        }
        Console.WriteLine("AdminE2E: SQL01/SpendWiseDb verified read-only; migrations/seeding/bulk and cleanup workers disabled; scoped auth recovery "
            + (services.GetService<AuthRecoveryDeliveryScope>() is null ? "disabled." : "enabled."));
    }
}
