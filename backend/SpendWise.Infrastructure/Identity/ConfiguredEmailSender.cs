using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Identity;

namespace SpendWise.Infrastructure.Identity;

// Testing always captures locally; Production requires configured SMTP.
public sealed class ConfiguredEmailSender(
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<ConfiguredEmailSender> logger,
    IEmailTemplateRenderer templateRenderer) : IEmailSender, ITransactionalEmailSender
{
    // Startup preflight only: validate without opening SMTP or printing settings.
    public void ValidateConfiguration()
    {
        if (ReadDeliveryMode() == EmailDeliveryMode.Smtp) _ = ReadSmtpSettings();
    }

    public async Task SendSecurityPinResetAsync(
        string email,
        string rawToken,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        await SendRecoveryAsync(
            email,
            rawToken,
            expiresAt,
            "security-pin-reset",
            "SpendWise Security PIN reset",
            cancellationToken);
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var deliveryMode = ReadDeliveryMode();
        if (deliveryMode == EmailDeliveryMode.Pickup)
        {
            await WriteDevelopmentPickupAsync(message, cancellationToken);
            return;
        }

        await SendSmtpAsync(message, cancellationToken);
    }

    private async Task SendRecoveryAsync(
        string email,
        string rawToken,
        DateTime expiresAt,
        string kind,
        string subject,
        CancellationToken cancellationToken)
    {
        var rendered = templateRenderer.Render(kind, new Dictionary<string, string?>
        {
            ["Title"] = subject,
            ["Heading"] = subject,
            ["Body"] = $"Use this one-time token before {expiresAt:O}: {rawToken}",
            ["Token"] = rawToken,
            ["ExpiresAt"] = expiresAt.ToString("O")
        });
        await SendAsync(new EmailMessage(email, subject, rendered.HtmlBody, rendered.TextBody), cancellationToken);
    }

    private async Task SendSmtpAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (new MailAddress(message.RecipientEmail).Host.EndsWith(".test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Reserved test recipients cannot be sent through SMTP.");
        // SMTP là provider-neutral; secret chỉ đọc từ deployment configuration.
        var settings = ReadSmtpSettings();

        using var mail = new MailMessage(
            settings.From,
            new MailAddress(message.RecipientEmail))
        {
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false
        };
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, null, "text/html"));
        using var client = new SmtpClient(
            settings.Host,
            settings.Port)
        {
            EnableSsl = settings.EnableSsl
        };
        if (settings.Username is not null)
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        await client.SendMailAsync(mail, cancellationToken);
        logger.LogInformation("Transactional email accepted by the configured SMTP provider; mailbox delivery is unverified.");
    }

    private async Task WriteDevelopmentPickupAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        // Raw token chỉ nằm trong pickup file local, không xuất hiện trong log hay API response.
        var configuredDirectory = configuration["PasswordReset:PickupDirectory"];
        var directory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "password-reset-pickup")
            : Path.GetFullPath(configuredDirectory);
        Directory.CreateDirectory(directory);
        var fileName = $"email-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.txt";
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(
            path,
            $"To: {message.RecipientEmail}{Environment.NewLine}Subject: {message.Subject}{Environment.NewLine}{Environment.NewLine}{message.TextBody}",
            cancellationToken);
        logger.LogInformation(
            "Development transactional email written to pickup file {FileName}.",
            fileName);
    }

    private EmailDeliveryMode ReadDeliveryMode()
    {
        // Test configuration may inherit SMTP credentials; never let it reach the network.
        if (environment.IsEnvironment("Testing")) return EmailDeliveryMode.Pickup;
        var configured = configuration["Email:DeliveryMode"]?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
            return environment.IsDevelopment() ? EmailDeliveryMode.Pickup : EmailDeliveryMode.Smtp;

        if (configured.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
            return EmailDeliveryMode.Smtp;

        if (configured.Equals("Pickup", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    "Email:DeliveryMode=Pickup is allowed only in Development or Testing.");
            }
            return EmailDeliveryMode.Pickup;
        }

        throw new InvalidOperationException(
            "Email:DeliveryMode must be either Pickup or Smtp.");
    }

    private SmtpSettings ReadSmtpSettings()
    {
        var host = configuration["Email:Smtp:Host"]?.Trim();
        var fromValue = configuration["Email:Smtp:From"]?.Trim();
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromValue))
        {
            throw new InvalidOperationException(
                "Email SMTP configuration requires Email:Smtp:Host and Email:Smtp:From.");
        }
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            throw new InvalidOperationException(
                "Email:Smtp:Host must be a valid DNS name or IP address.");
        }

        if (!int.TryParse(configuration["Email:Smtp:Port"] ?? "587", out var port)
            || port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "Email:Smtp:Port must be an integer between 1 and 65535.");
        }

        if (!bool.TryParse(configuration["Email:Smtp:EnableSsl"] ?? "true", out var enableSsl))
        {
            throw new InvalidOperationException(
                "Email:Smtp:EnableSsl must be true or false.");
        }

        MailAddress from;
        try
        {
            from = new MailAddress(fromValue);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Email:Smtp:From must be a valid email address.",
                exception);
        }

        var username = NullIfWhiteSpace(configuration["Email:Smtp:Username"]);
        var password = NullIfWhiteSpace(configuration["Email:Smtp:Password"]);
        if ((username is null) != (password is null))
        {
            throw new InvalidOperationException(
                "Email:Smtp:Username and Email:Smtp:Password must be configured together.");
        }

        return new SmtpSettings(host, from, port, enableSsl, username, password);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private enum EmailDeliveryMode
    {
        Pickup,
        Smtp
    }

    private sealed record SmtpSettings(
        string Host,
        MailAddress From,
        int Port,
        bool EnableSsl,
        string? Username,
        string? Password);
}
