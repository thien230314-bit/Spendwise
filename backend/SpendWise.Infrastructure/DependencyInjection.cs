using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using SpendWise.Application.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Application.Finance;
using SpendWise.Infrastructure.Finance;
using SpendWise.Application.Planning;
using SpendWise.Infrastructure.Planning;
using SpendWise.Application.Notifications;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Application.AI;
using SpendWise.Infrastructure.AI;
using SpendWise.Application.Admin;
using SpendWise.Infrastructure.Admin;
using SpendWise.Application.Banking;
using SpendWise.Infrastructure.Banking;
using Microsoft.AspNetCore.DataProtection;
using SpendWise.Application.Features.AdminAi;
using SpendWise.Infrastructure.AdminAi;
using SpendWise.Application.MobilePush;
using SpendWise.Infrastructure.MobilePush;

namespace SpendWise.Infrastructure;

// Tập trung đăng ký database, xác thực và các dịch vụ hạ tầng.

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Đăng ký DbContext, dịch vụ kỹ thuật và background worker cho Infrastructure.
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Production phải nhận connection string từ secret store hoặc biến môi trường.
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required through secure configuration.");
        }

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IAvatarStorage, LocalAvatarStorage>();
        services.Configure<SePayBankHubOptions>(
            configuration.GetSection(SePayBankHubOptions.SectionName));
        services.AddHttpClient("SePayBankHub", client =>
            client.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient("SePayTestModeAccounts", client =>
            client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<ISePayTestModeAccountReader, SePayTestModeAccountReader>();
        services.AddSingleton<SePayBankHubConfiguration>();
        services.AddSingleton<IBankHubConfiguration>(provider =>
            provider.GetRequiredService<SePayBankHubConfiguration>());
        services.AddSingleton<IBankHubGateway, SePayBankHubGateway>();
        services.AddDbContext<SpendWiseDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ISpendWiseDbContext>(provider => provider.GetRequiredService<SpendWiseDbContext>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ILoginChallengeTokenService, LoginChallengeTokenService>();
        services.AddSingleton<ISecurityPinService, SecurityPinService>();
        services.AddSingleton<IDeviceTrustTokenService, DeviceTrustTokenService>();
        services.AddSingleton<IDeviceTrustPolicy, DeviceTrustPolicy>();
        services.AddSingleton<ITemporaryPasswordService, TemporaryPasswordService>();
        services.AddSingleton<IEmailVerificationPolicy, EmailVerificationPolicy>();
        services.AddSingleton<IEmailVerificationOtpService, EmailVerificationOtpService>();
        services.AddDataProtection().SetApplicationName("SpendWise.EmailOutbox");
        services.AddSingleton<IEmailContentProtector, EmailContentProtector>();
        services.AddSingleton<ConfiguredEmailSender>();
        services.AddScoped<IEmailSender, QueuedSecurityPinEmailSender>();
        services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddScoped<ITransactionalEmailSender>(provider =>
            provider.GetRequiredService<ConfiguredEmailSender>());
        services.AddScoped<IEmailDeliveryQueue, EmailDeliveryQueue>();
        services.AddScoped<EmailOutboxDispatcher>();
        services.AddHostedService<EmailOutboxBackgroundService>();
        services.AddScoped<IAccessSessionValidator, AccessSessionValidator>();
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IRecurringProcessor, RecurringProcessor>();
        services.AddHostedService<RecurringBackgroundService>();
        services.AddHostedService<V1MaintenanceScheduler>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<OutboxDispatcher>();
        services.AddHostedService<OutboxBackgroundService>();
        services.Configure<MobilePushOptions>(
            configuration.GetSection(MobilePushOptions.SectionName));
        services.AddHttpClient<IExpoPushSender, ExpoPushSender>(client =>
            client.Timeout = TimeSpan.FromSeconds(20));
        services.AddScoped<MobilePushDispatcher>();
        services.AddScoped<MobilePushReceiptReconciler>();
        services.AddHostedService<MobilePushBackgroundService>();
        services.AddSingleton<IProfanityFilter, ProfanityFilter>();
        services.AddHttpClient<GeminiCompletionProvider>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IAiCompletionProvider, ConfiguredAiCompletionProvider>();
        services.AddSingleton<GeminiProviderStatus>();
        services.AddSingleton<IAiProviderStatus, ConfiguredAiProviderStatus>();
        services.AddScoped<IAiAttachmentReader, AiAttachmentReader>();
        services.AddSingleton<IAdminAiReportExporter, AdminAiReportExporter>();
        services.AddHostedService<AiAttachmentCleanupService>();
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IDatabaseHealthChecker, DatabaseHealthChecker>();
        services.AddHealthChecks()
            .AddCheck<SqlServerReadinessHealthCheck>("sql_server", tags: ["ready"]);
        services.AddScoped<IOperationPolicy, OperationPolicy>();
        return services;
    }
}
