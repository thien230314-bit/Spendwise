using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Banking;
using SpendWise.Application.Identity;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Banking;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Tests;

// Factory chạy đúng HTTP pipeline nhưng thay SQL Server bằng SQLite cô lập cho test.
internal sealed class SpendWiseApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection? connection;
    private readonly string? databasePath;
    private readonly string sqliteConnectionString;
    private readonly IReadOnlyDictionary<string, string?> configurationOverrides;

    public SpendWiseApiFactory(
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        bool useIndependentDatabaseConnections = false)
    {
        this.configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
        if (useIndependentDatabaseConnections)
        {
            // File SQLite tạm cho phép hai HTTP request dùng hai connection/DbContext thật sự độc lập.
            databasePath = Path.Combine(
                Path.GetTempPath(),
                $"spendwise-bankhub-concurrency-{Guid.NewGuid():N}.db");
            sqliteConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Default,
                Pooling = false,
                DefaultTimeout = 30
            }.ToString();
        }
        else
        {
            connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            sqliteConnectionString = connection.ConnectionString;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "Jwt:Key",
            "SpendWise-HTTP-tests-only-signing-key-2026-at-least-32-bytes");
        builder.UseSetting("Jwt:Issuer", "SpendWise.Tests");
        builder.UseSetting("Jwt:Audience", "SpendWise.Tests");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(local);Database=unused");
        // Test host không ghi Windows Event Log vì sandbox không có quyền tạo event source.
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // Test dùng khóa đủ dài và không đọc bí mật deployment thật.
            var values = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SpendWise-HTTP-tests-only-signing-key-2026-at-least-32-bytes",
                ["Jwt:Issuer"] = "SpendWise.Tests",
                ["Jwt:Audience"] = "SpendWise.Tests",
                ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=unused",
                ["SePayBankHub:Enabled"] = "true",
                ["SePayBankHub:Environment"] = "Sandbox",
                ["SePayBankHub:ClientId"] = "test-client",
                ["SePayBankHub:ClientSecret"] = "test-secret",
                ["SePayBankHub:CompanyXid"] = "test-company",
                ["SePayBankHub:LifecycleWebhookSecret"] = "test-lifecycle-secret",
                ["SePayBankHub:IpnApiKey"] = "test-ipn-key",
                ["SePayBankHub:CompletionRedirectUri"] = "https://localhost:5173/bank-callback",
                ["SePayBankHub:MobileCompletionRedirectUri"] = "https://mobile.spendwise.test/bank-callback",
                ["SePayBankHub:AllowedCompletionRedirectOrigins:0"] = "https://localhost:5173",
                ["SePayBankHub:AllowedCompletionRedirectOrigins:1"] = "https://mobile.spendwise.test"
            };
            foreach (var item in configurationOverrides)
                values[item.Key] = item.Value;
            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureServices(services =>
        {
            // Background workers không cần chạy trong test request ngắn và có thể tranh connection SQLite.
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<SpendWiseDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SpendWiseDbContext>>();
            services.RemoveAll<SpendWiseDbContext>();
            services.RemoveAll<ISpendWiseDbContext>();
            services.AddDbContext<SpendWiseDbContext>(options =>
            {
                if (connection is null)
                    options.UseSqlite(sqliteConnectionString);
                else
                    options.UseSqlite(connection);
            });
            services.AddScoped<ISpendWiseDbContext>(provider =>
                provider.GetRequiredService<SpendWiseDbContext>());

            services.RemoveAll<IEmailVerificationOtpService>();
            services.AddSingleton<IEmailVerificationOtpService, TestEmailVerificationOtpService>();
            services.RemoveAll<IRealtimeNotifier>();
            services.AddSingleton<IRealtimeNotifier, NullRealtimeNotifier>();
            services.RemoveAll<IBankHubGateway>();
            services.AddSingleton<IBankHubGateway, TestBankHubGateway>();
        });
    }

    public HttpClient CreateHttpsClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
            .Database.EnsureCreated();
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            connection?.Dispose();
            if (databasePath is not null)
            {
                // Xóa database tạm sau test; source tree không nhận thêm artifact.
                SqliteConnection.ClearAllPools();
                foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
                {
                    var path = databasePath + suffix;
                    if (File.Exists(path)) File.Delete(path);
                }
            }
        }
    }

    private sealed class NullEmailSender : IEmailSender
    {
        public Task SendSecurityPinResetAsync(
            string email,
            string rawToken,
            DateTime expiresAt,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NullRealtimeNotifier : IRealtimeNotifier
    {
        public Task SendToUserAsync(
            Guid userId,
            string eventName,
            object payload,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

internal sealed class TestEmailVerificationOtpService : IEmailVerificationOtpService
{
    public string CreateOtp() => IdentityTestData.DefaultEmailOtp;

    public string HashOtp(Guid userId, string purpose, string otp) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{userId:N}|{purpose}|{otp}")));

    public bool VerifyOtp(Guid userId, string purpose, string otp, string expectedHash) =>
        string.Equals(HashOtp(userId, purpose, otp), expectedHash, StringComparison.Ordinal);

    public string HashRateLimitKey(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)));
}

// Gateway giả giúp HTTP test đi qua toàn bộ Controller/MediatR mà không gọi SePay thật.
internal sealed class TestBankHubGateway : IBankHubGateway
{
    private int sequence;
    public List<ExternalBankTransactionData> Transactions { get; } = [];
    public int SandboxCalls { get; private set; }
    public bool FailSync { get; set; }
    public ConcurrentQueue<BankProviderLinkRequest> LinkRequests { get; } = new();

    public Task<BankProviderLinkResult> CreateLinkAsync(
        BankProviderLinkRequest request,
        CancellationToken cancellationToken)
    {
        LinkRequests.Enqueue(request);
        var id = Interlocked.Increment(ref sequence);
        return Task.FromResult(new BankProviderLinkResult(
            $"link-xid-{id}",
            $"https://bankhub-api-sandbox.sepay.vn/hosted/{id}",
            DateTime.UtcNow.AddMinutes(10)));
    }

    public Task<ExternalBankTransactionPage> GetTransactionsAsync(
        string externalBankAccountId,
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (FailSync) throw new InvalidOperationException("Provider test failure.");
        var items = Transactions.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var pages = Math.Max(1, (int)Math.Ceiling(Transactions.Count / (decimal)pageSize));
        return Task.FromResult(new ExternalBankTransactionPage(items, page, pages));
    }

    public Task CreateSandboxTransactionAsync(
        string externalBankAccountId,
        BankTransactionDirection direction,
        decimal amount,
        string content,
        CancellationToken cancellationToken)
    {
        SandboxCalls++;
        return Task.CompletedTask;
    }
}
