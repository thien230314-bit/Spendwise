using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;

namespace SpendWise.Tests;

internal sealed class TestEmailVerificationPolicy : IEmailVerificationPolicy
{
    public TimeSpan OtpLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan ResendCooldown { get; init; } = TimeSpan.FromMinutes(1);
    public int MaximumFailedAttempts { get; init; } = 5;
    public int MaximumSendsPerHourPerAccount { get; init; } = 5;
    public int MaximumSendsPerHourPerIp { get; init; } = 20;
    public string WebsiteUrl { get; init; } = "https://spendwise.test";
}

internal sealed class TestDeviceTrustPolicy : IDeviceTrustPolicy
{
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(180);
}

internal sealed class TestEmailContentProtector : IEmailContentProtector
{
    public string Protect(string value) => $"protected:{value}";
    public string Unprotect(string value) => value.StartsWith("protected:", StringComparison.Ordinal)
        ? value[10..]
        : value;
}

internal static class EmailVerificationTestFactory
{
    public static EmailVerificationSupport Create(
        ISpendWiseDbContext db,
        IClock clock,
        IEmailVerificationPolicy? policy = null)
    {
        return new EmailVerificationSupport(
            db,
            new TestEmailVerificationOtpService(),
            policy ?? new TestEmailVerificationPolicy(),
            new EmailTemplateRenderer(),
            new EmailDeliveryQueue(db, new TestEmailContentProtector()),
            clock);
    }
}

internal static class SupportEmailTestFactory
{
    public static SupportCaseEmailSupport Create(ISpendWiseDbContext db) =>
        new(
            new EmailTemplateRenderer(),
            new EmailDeliveryQueue(db, new TestEmailContentProtector()),
            new TestEmailVerificationPolicy());
}

internal static class InteractiveLoginTestFactory
{
    public static InteractiveLoginSupport Create(
        ISpendWiseDbContext db,
        IClock clock,
        TimeSpan? lifetime = null) =>
        new(
            db,
            new DeviceTrustTokenService(),
            new TestDeviceTrustPolicy { Lifetime = lifetime ?? TimeSpan.FromDays(180) },
            new EmailTemplateRenderer(),
            new EmailDeliveryQueue(db, new TestEmailContentProtector()),
            new TestEmailVerificationPolicy(),
            clock);
}
