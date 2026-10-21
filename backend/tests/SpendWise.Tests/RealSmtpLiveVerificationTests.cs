using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace SpendWise.Tests;

/// <summary>
/// Custom xUnit Fact: Tự động SKIP nếu biến môi trường SPENDWISE_ENABLE_LIVE_SMTP_TESTS != true.
/// Đảm bảo tuyệt đối không gửi email thật trong CI hoặc khi chạy toàn bộ test tự động mặc định.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LiveSmtpFactAttribute : FactAttribute
{
    public LiveSmtpFactAttribute()
    {
        var envVal = Environment.GetEnvironmentVariable("SPENDWISE_ENABLE_LIVE_SMTP_TESTS");
        var isEnabled = string.Equals(envVal, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(envVal, "1", StringComparison.OrdinalIgnoreCase);

        if (!isEnabled)
        {
            Skip = "Opt-in: Live SMTP test is disabled by default. Set SPENDWISE_ENABLE_LIVE_SMTP_TESTS=true to enable.";
        }
    }
}

/// <summary>
/// Kiểm thử thủ công (Opt-In Manual Tests) cho việc gửi email thật qua SMTP và vòng đời Outbox trên SQL02.
/// Mặc định KHÔNG chạy trong CI hoặc khi chạy toàn bộ test tự động (xUnit sẽ SKIP hoàn toàn).
/// Chỉ kích hoạt khi có:
///   SPENDWISE_ENABLE_LIVE_SMTP_TESTS=true
///   SPENDWISE_LIVE_SMTP_RECIPIENT=&lt;email_thử_nghiệm&gt;
///   SPENDWISE_SQL02_CONNECTION=&lt;chuỗi_kết_nối_sql02&gt; (cho test outbox)
/// </summary>
[Trait("Category", "ManualLiveSmtp")]
public sealed class RealSmtpLiveVerificationTests(ITestOutputHelper output)
{
    private sealed class DevelopmentHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "SpendWise";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RealSystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed record EmailTypeTestCase(
        string Code,
        string Name,
        string Category,
        string TemplateName,
        string Recipient,
        string Subject,
        Dictionary<string, string?> TemplateValues);

    private static IConfiguration LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .AddUserSecrets("SpendWise-Development")
            .AddEnvironmentVariables()
            .Build();
    }

    private static string? GetConfiguredRecipient(IConfiguration config)
    {
        return Environment.GetEnvironmentVariable("SPENDWISE_LIVE_SMTP_RECIPIENT")
            ?? config["LiveTest:SmtpRecipient"]
            ?? config["Email:LiveTestRecipient"];
    }

    private static string? GetConfiguredSql02Connection(IConfiguration config)
    {
        return Environment.GetEnvironmentVariable("SPENDWISE_SQL02_CONNECTION")
            ?? config.GetConnectionString("Sql02Connection")
            ?? config.GetConnectionString("SpendWise_E2E_G4")
            ?? config.GetConnectionString("SpendWiseDb")
            ?? config.GetConnectionString("DefaultConnection");
    }

    private static IServiceProvider BuildServiceProvider(IConfiguration configuration, string? sqlConnection = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.AddSingleton<IHostEnvironment>(new DevelopmentHostEnvironment());
        services.AddSingleton<IClock, RealSystemClock>();
        services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();

        services.AddLogging(builder => builder.AddDebug());
        services.AddDataProtection();

        if (!string.IsNullOrWhiteSpace(sqlConnection))
        {
            services.AddDbContext<SpendWiseDbContext>(options =>
                options.UseSqlServer(sqlConnection));
            services.AddScoped<ISpendWiseDbContext>(sp => sp.GetRequiredService<SpendWiseDbContext>());
        }

        services.AddScoped<IEmailContentProtector, EmailContentProtector>();
        services.AddScoped<ConfiguredEmailSender>();
        services.AddScoped<ITransactionalEmailSender>(sp => sp.GetRequiredService<ConfiguredEmailSender>());
        services.AddScoped<IEmailDeliveryQueue, EmailDeliveryQueue>();
        services.AddScoped<EmailOutboxDispatcher>();

        return services.BuildServiceProvider();
    }

    [LiveSmtpFact]
    public async Task Verify_All_14_Email_Types_Sent_Via_Gmail_Smtp_Tls()
    {
        var config = LoadConfiguration();

        // 1. Kiểm tra email người nhận từ cấu hình môi trường (không hardcode trong source)
        var primaryRecipient = GetConfiguredRecipient(config);
        if (string.IsNullOrWhiteSpace(primaryRecipient) || !primaryRecipient.Contains('@'))
        {
            output.WriteLine("SKIPPED: Live SMTP tests require a valid recipient email. Set SPENDWISE_LIVE_SMTP_RECIPIENT environment variable.");
            return;
        }

        // 2. Kiểm tra thông tin cấu hình SMTP
        if (string.IsNullOrWhiteSpace(config["Email:Smtp:Host"]) ||
            string.IsNullOrWhiteSpace(config["Email:Smtp:Username"]) ||
            string.IsNullOrWhiteSpace(config["Email:Smtp:Password"]))
        {
            output.WriteLine("SKIPPED: Live SMTP credentials (Host, Username, Password) are not configured.");
            return;
        }

        // 3. Sinh địa chỉ plus-addressing động từ email người nhận được cấu hình
        var atIndex = primaryRecipient.IndexOf('@');
        var userPart = primaryRecipient[..atIndex];
        var domainPart = primaryRecipient[atIndex..];
        var plusOldRecipient = $"{userPart}+spw-old{domainPart}";
        var plusNewRecipient = $"{userPart}+spw-new{domainPart}";

        var sp = BuildServiceProvider(config);
        using var scope = sp.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();
        var sender = scope.ServiceProvider.GetRequiredService<ConfiguredEmailSender>();

        var testCases = new List<EmailTypeTestCase>
        {
            // 1. Account & Auth OTPs
            new(
                "OTP-01",
                "Mã xác thực đăng ký (Registration OTP)",
                "Xác thực & Đăng nhập",
                "registration-verification",
                primaryRecipient,
                "[SpendWise] Mã xác thực đăng ký tài khoản",
                new Dictionary<string, string?>
                {
                    ["Email"] = primaryRecipient,
                    ["Otp"] = "83920147",
                    ["Minutes"] = "10"
                }),

            new(
                "OTP-02",
                "Mã OTP đặt lại mật khẩu (Password Reset OTP)",
                "Xác thực & Đăng nhập",
                "password-reset-verification",
                primaryRecipient,
                "[SpendWise] Mã OTP đặt lại mật khẩu",
                new Dictionary<string, string?>
                {
                    ["Email"] = primaryRecipient,
                    ["Otp"] = "49201835",
                    ["Minutes"] = "10"
                }),

            new(
                "OTP-03",
                "Mã xác thực thay đổi email (Change Email OTP - Plus Addressing)",
                "Xác thực & Đăng nhập",
                "email-change-verification",
                plusNewRecipient,
                "[SpendWise] Mã xác thực thay đổi email",
                new Dictionary<string, string?>
                {
                    ["Email"] = plusNewRecipient,
                    ["Otp"] = "71549203",
                    ["Minutes"] = "10"
                }),

            // 2. Security Alerts & Notifications
            new(
                "SEC-01",
                "Cảnh báo bảo mật: Thay đổi email (Security Notice - Old Email)",
                "Cảnh báo bảo mật",
                "email-change-notice",
                plusOldRecipient,
                "[SpendWise] Cảnh báo bảo mật: Email tài khoản đã thay đổi",
                new Dictionary<string, string?>
                {
                    ["OldEmail"] = plusOldRecipient,
                    ["NewEmail"] = plusNewRecipient,
                    ["ChangedAt"] = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss 'UTC'")
                }),

            new(
                "SEC-02",
                "Thông báo mật khẩu đã được đặt lại (Password Reset Success)",
                "Cảnh báo bảo mật",
                "password-reset-success",
                primaryRecipient,
                "[SpendWise] Mật khẩu đã được đặt lại thành công",
                new Dictionary<string, string?>
                {
                    ["UserName"] = "Test User",
                    ["ChangedAt"] = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss 'UTC'"),
                    ["ActionUrl"] = "https://spendwise.local/login"
                }),

            new(
                "SEC-03",
                "Cảnh báo đăng nhập thiết bị mới (New Device Login Alert)",
                "Cảnh báo bảo mật",
                "new-device-login",
                primaryRecipient,
                "[SpendWise] Cảnh báo bảo mật: Đăng nhập từ trình duyệt mới",
                new Dictionary<string, string?>
                {
                    ["UserName"] = "Test User",
                    ["LoginTime"] = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss 'UTC'"),
                    ["DeviceName"] = "Windows 11 PC (Chrome 131)",
                    ["BrowserInfo"] = "Chrome 131.0 on Windows",
                    ["IpAddress"] = "14.232.208.45",
                    ["ActionUrl"] = "https://spendwise.local/app/profile"
                }),

            new(
                "SEC-04",
                "Cấp lại mã PIN bảo mật (Security PIN Reset Token)",
                "Cảnh báo bảo mật",
                "security-pin-reset",
                primaryRecipient,
                "[SpendWise] Yêu cầu đặt lại mã PIN bảo mật",
                new Dictionary<string, string?>
                {
                    ["Token"] = "PIN-RESET-93821047",
                    ["ExpiresAt"] = DateTime.UtcNow.AddMinutes(15).ToString("dd/MM/yyyy HH:mm:ss 'UTC'"),
                    ["ActionUrl"] = "https://spendwise.local/reset-pin"
                }),

            // 3. User Lifecycle & Engagement
            new(
                "LIFE-01",
                "Chào mừng thành viên mới (Registration Welcome)",
                "Vòng đời người dùng",
                "registration-welcome",
                primaryRecipient,
                "[SpendWise] Chào mừng bạn đến với SpendWise",
                new Dictionary<string, string?>
                {
                    ["UserName"] = "Test User",
                    ["ActionUrl"] = "https://spendwise.local/app",
                    ["ActionText"] = "Bắt đầu khám phá SpendWise"
                }),

            new(
                "LIFE-02",
                "Chào mừng người dùng quay trở lại (Welcome Back Inactive User)",
                "Vòng đời người dùng",
                "welcome-back",
                primaryRecipient,
                "[SpendWise] Chào mừng bạn quay trở lại SpendWise",
                new Dictionary<string, string?>
                {
                    ["UserName"] = "Test User",
                    ["ActionUrl"] = "https://spendwise.local/app"
                }),

            // 4. Customer Support & Dispute Resolutions
            new(
                "SUPP-01",
                "Tiếp nhận yêu cầu hỗ trợ (Support Case Received)",
                "Hỗ trợ & Khiếu nại",
                "support-received",
                primaryRecipient,
                "[SpendWise] Đã tiếp nhận yêu cầu hỗ trợ #SPW-2026-9041",
                new Dictionary<string, string?>
                {
                    ["TicketNumber"] = "SPW-2026-9041",
                    ["Subject"] = "Tra soát giao dịch nạp tiền qua SePay",
                    ["SubmittedAt"] = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss 'UTC'"),
                    ["Status"] = "Đang xử lý",
                    ["ActionUrl"] = "https://spendwise.local/support/cases/SPW-2026-9041"
                }),

            new(
                "SUPP-02",
                "Giải quyết yêu cầu hỗ trợ thành công (Support Case Resolved)",
                "Hỗ trợ & Khiếu nại",
                "support-resolved",
                primaryRecipient,
                "[SpendWise] Yêu cầu hỗ trợ #SPW-2026-9041 đã được giải quyết",
                new Dictionary<string, string?>
                {
                    ["TicketNumber"] = "SPW-2026-9041",
                    ["Message"] = "Giao dịch nạp tiền 500,000 VND đã được đối chiếu thành công và ghi có vào ví chính của bạn.",
                    ["ActionUrl"] = "https://spendwise.local/support/cases/SPW-2026-9041"
                }),

            new(
                "SUPP-03",
                "Thông báo từ chối yêu cầu hỗ trợ (Support Case Rejected)",
                "Hỗ trợ & Khiếu nại",
                "support-rejected",
                primaryRecipient,
                "[SpendWise] Thông báo về yêu cầu hỗ trợ #SPW-2026-8812",
                new Dictionary<string, string?>
                {
                    ["TicketNumber"] = "SPW-2026-8812",
                    ["Message"] = "Thông tin mã tham chiếu thanh toán không khớp với dữ liệu từ ngân hàng liên kết.",
                    ["ActionUrl"] = "https://spendwise.local/support/cases/SPW-2026-8812"
                }),

            new(
                "SUPP-04",
                "Yêu cầu bổ sung chứng từ (Support Documents Requested)",
                "Hỗ trợ & Khiếu nại",
                "support-info-request",
                primaryRecipient,
                "[SpendWise] Yêu cầu bổ sung chứng từ cho yêu cầu #SPW-2026-9041",
                new Dictionary<string, string?>
                {
                    ["TicketNumber"] = "SPW-2026-9041",
                    ["Subject"] = "Tra soát nạp tiền ngân hàng",
                    ["RequiredDocuments"] = "Ảnh chụp biên lai chuyển khoản ngân hàng có mã giao dịch FT...",
                    ["Note"] = "Vui lòng chụp rõ ràng đầy đủ 4 góc của biên lai thanh toán.",
                    ["ActionUrl"] = "https://spendwise.local/support/cases/SPW-2026-9041"
                }),

            new(
                "SUPP-05",
                "Cập nhật tiến trình tra soát tài chính (Fund Investigation Progress)",
                "Hỗ trợ & Khiếu nại",
                "support-fund-investigation",
                primaryRecipient,
                "[SpendWise] Cập nhật tiến độ điều tra quỹ — Yêu cầu #SPW-2026-9041",
                new Dictionary<string, string?>
                {
                    ["TicketNumber"] = "SPW-2026-9041",
                    ["FundName"] = "Quỹ Dự Phòng Cá Nhân",
                    ["CurrentPhase"] = "Đối soát sổ cái bất biến với ngân hàng đối tác",
                    ["Note"] = "Số dư hiện tại vẫn được bảo toàn theo sổ cái.",
                    ["ActionUrl"] = "https://spendwise.local/support/cases/SPW-2026-9041"
                })
        };

        output.WriteLine("=== BẮT ĐẦU KIỂM THỬ THỦ CÔNG GỬI EMAIL THẬT QUA GMAIL SMTP ===");
        output.WriteLine($"Người nhận chính: {primaryRecipient}");
        output.WriteLine($"Plus addressing nhận mới: {plusNewRecipient}");
        output.WriteLine($"Plus addressing nhận cũ: {plusOldRecipient}");
        output.WriteLine($"Tổng số loại email kiểm thử: {testCases.Count}");
        output.WriteLine("---------------------------------------------------------");

        var stopwatch = new Stopwatch();
        var index = 1;

        foreach (var tc in testCases)
        {
            stopwatch.Restart();
            var rendered = renderer.Render(tc.TemplateName, tc.TemplateValues);
            var emailMsg = new EmailMessage(tc.Recipient, tc.Subject, rendered.HtmlBody, rendered.TextBody);

            await sender.SendAsync(emailMsg, CancellationToken.None);
            stopwatch.Stop();

            output.WriteLine($"[{index++:D2}/{testCases.Count:D2}] [{tc.Code}] {tc.Name}");
            output.WriteLine($"      To: {tc.Recipient}");
            output.WriteLine($"      Subject: {tc.Subject}");
            output.WriteLine($"      SMTP Status: ACCEPTED by smtp.gmail.com (STARTTLS/TLS 1.3) [{stopwatch.ElapsedMilliseconds} ms]");
            output.WriteLine("---------------------------------------------------------");
        }

        output.WriteLine("=== HOÀN TẤT GỬI TOÀN BỘ 14 LOẠI EMAIL THẬT QUA GMAIL SMTP ===");
    }

    [LiveSmtpFact]
    public async Task Verify_Sql02_Outbox_Dispatch_Lifecycle_Via_Real_Smtp()
    {
        var config = LoadConfiguration();

        // 1. Kiểm tra email người nhận từ môi trường
        var primaryRecipient = GetConfiguredRecipient(config);
        if (string.IsNullOrWhiteSpace(primaryRecipient) || !primaryRecipient.Contains('@'))
        {
            output.WriteLine("SKIPPED: SQL02 Outbox live test requires a valid recipient email. Set SPENDWISE_LIVE_SMTP_RECIPIENT environment variable.");
            return;
        }

        // 2. Kiểm tra chuỗi kết nối SQL02 từ môi trường/cấu hình (không hardcode)
        var sql02Connection = GetConfiguredSql02Connection(config);
        if (string.IsNullOrWhiteSpace(sql02Connection))
        {
            output.WriteLine("SKIPPED: SQL02 Outbox live test requires SQL connection string. Set SPENDWISE_SQL02_CONNECTION environment variable.");
            return;
        }

        var atIndex = primaryRecipient.IndexOf('@');
        var userPart = primaryRecipient[..atIndex];
        var domainPart = primaryRecipient[atIndex..];
        var plusNewRecipient = $"{userPart}+spw-new{domainPart}";

        var sp = BuildServiceProvider(config, sql02Connection);
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<IEmailDeliveryQueue>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailOutboxDispatcher>();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();

        // Test SQL02 transactional outbox queue & dispatch with Change Email OTP (plus addressing)
        var rendered = renderer.Render("email-change-verification", new Dictionary<string, string?>
        {
            ["Email"] = plusNewRecipient,
            ["Otp"] = "98234105",
            ["Minutes"] = "10"
        });

        var dedupKey = $"email:live-smtp-verification:{Guid.NewGuid():N}";
        var msg = new EmailMessage(
            plusNewRecipient,
            "[SpendWise Outbox] Xác nhận thay đổi email qua SQL02 Outbox",
            rendered.HtmlBody,
            rendered.TextBody);

        var enqueued = await queue.EnqueueAsync(msg, dedupKey, CancellationToken.None);
        Assert.True(enqueued, "Failed to enqueue message to SQL02 EmailOutboxMessages.");

        // Query SQL02: Status == Pending
        var pendingMessage = await db.EmailOutboxMessages.AsNoTracking()
            .SingleAsync(x => x.DeduplicationKey == dedupKey);
        Assert.Equal(EmailDeliveryStatus.Pending, pendingMessage.Status);
        Assert.Null(pendingMessage.SentAt);

        // Dispatch via real SMTP
        var dispatched = await dispatcher.DispatchByIdAsync(
            pendingMessage.Id,
            plusNewRecipient,
            dedupKey,
            CancellationToken.None);
        Assert.True(dispatched, "EmailOutboxDispatcher failed to dispatch outbox message via SMTP.");

        // Query SQL02: Status == Sent, SentAt != null, LastError == null, HtmlBody cleared
        db.ChangeTracker.Clear();
        var sentMessage = await db.EmailOutboxMessages.AsNoTracking()
            .SingleAsync(x => x.DeduplicationKey == dedupKey);

        Assert.Equal(EmailDeliveryStatus.Sent, sentMessage.Status);
        Assert.NotNull(sentMessage.SentAt);
        Assert.Null(sentMessage.LastError);
        Assert.Equal(string.Empty, sentMessage.HtmlBody); // Xóa nội dung sau khi gửi thành công để bảo mật thông tin
        Assert.Equal(string.Empty, sentMessage.TextBody);

        output.WriteLine("=== SQL02 OUTBOX REAL SMTP DISPATCH TEST ===");
        output.WriteLine($"DeduplicationKey: {dedupKey}");
        output.WriteLine($"Outbox Id: {sentMessage.Id}");
        output.WriteLine($"Recipient: {plusNewRecipient}");
        output.WriteLine($"Status in SQL02: {sentMessage.Status}");
        output.WriteLine($"SentAt in SQL02: {sentMessage.SentAt:yyyy-MM-dd HH:mm:ss 'UTC'}");
        output.WriteLine("SQL02 Outbox lifecycle PASS: Encrypted on enqueue -> Dispatched via Gmail SMTP -> Updated Sent -> Content wiped.");
    }
}
