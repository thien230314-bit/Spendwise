using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.ForgotPassword;
using SpendWise.Application.Features.Authentication.Commands.ResetPassword;
using SpendWise.Application.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;

namespace SpendWise.AdminPasswordRecovery;

public static class RecoveryServices
{
    public const string Server = @"DESKTOP-B5KA3LT\SQLEXPRESS01";
    public const string Database = "SpendWiseDb";

    public static void ValidateConfiguration(IConfiguration configuration)
    {
        var sql = new SqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection"));
        if (sql.DataSource != Server || sql.InitialCatalog != Database
            || sql.AttachDBFilename.Length != 0 || sql.UserInstance)
            throw new InvalidOperationException("Recovery SQL target is not approved.");
        if (configuration["Email:DeliveryMode"] != "Smtp"
            || configuration["Email:Smtp:Host"] != "smtp.gmail.com"
            || configuration.GetValue<int>("Email:Smtp:Port") != 587
            || !configuration.GetValue<bool>("Email:Smtp:EnableSsl"))
            throw new InvalidOperationException("Gmail SMTP configuration is incomplete.");
        foreach (var key in new[] { "Email:Smtp:From", "Email:Smtp:Username", "Email:Smtp:Password",
                     "EmailVerification:OtpPepper", "Email:WebsiteUrl" })
            if (string.IsNullOrWhiteSpace(configuration[key]))
                throw new InvalidOperationException("Required recovery configuration is missing.");
        _ = new System.Net.Mail.MailAddress(configuration["Email:Smtp:From"]!);
        _ = new EmailVerificationOtpService(configuration);
    }

    // Explicit registrations only: no AddInfrastructure, Host, seeder or hosted services.
    public static IServiceCollection Create(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging(logging => logging.ClearProviders());
        services.AddSingleton<IHostEnvironment>(new RecoveryEnvironment());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddDbContext<SpendWiseDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        services.AddScoped<ISpendWiseDbContext>(p => p.GetRequiredService<SpendWiseDbContext>());
        services.AddDataProtection().SetApplicationName("SpendWise.EmailOutbox");
        // Key protection works on demand; the optional key-ring warm-up host is not started here.
        services.RemoveAll<IHostedService>();
        services.AddSingleton<IEmailContentProtector, EmailContentProtector>();
        services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddSingleton<IEmailVerificationPolicy, EmailVerificationPolicy>();
        services.AddSingleton<IEmailVerificationOtpService, EmailVerificationOtpService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ITransactionalEmailSender, ConfiguredEmailSender>();
        services.AddScoped<IEmailDeliveryQueue, EmailDeliveryQueue>();
        services.AddScoped<EmailVerificationSupport>();
        services.AddScoped<AuthenticationSupport>();
        services.AddScoped<ForgotPasswordCommandHandler>();
        services.AddScoped<ResetPasswordCommandHandler>();
        services.AddScoped<EmailOutboxDispatcher>();
        services.AddScoped<RecoverySession>();
        return services;
    }

    private sealed class RecoveryEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Staging;
        public string ApplicationName { get; set; } = "SpendWise.AdminPasswordRecovery";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
