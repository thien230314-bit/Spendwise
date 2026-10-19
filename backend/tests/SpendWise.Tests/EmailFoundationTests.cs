using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class EmailFoundationTests
{
    [Fact]
    public void Renderer_escapes_dynamic_content_and_returns_text_fallback()
    {
        var renderer = new EmailTemplateRenderer();

        var result = renderer.Render("support", new Dictionary<string, string?>
        {
            ["Title"] = "<Title>",
            ["Heading"] = "<Heading>",
            ["Body"] = "<script>alert('x')</script> & details"
        });

        Assert.Contains("&lt;script&gt;", result.HtmlBody);
        Assert.DoesNotContain("<script>", result.HtmlBody);
        Assert.Contains("<script>alert('x')</script>", result.TextBody);
    }

    [Fact]
    public void Renderer_applies_shared_email_safe_shell_to_every_supported_template()
    {
        var renderer = new EmailTemplateRenderer();

        foreach (var (templateName, values, criticalValue) in TemplateCases())
        {
            var result = renderer.Render(templateName, values);

            Assert.False(string.IsNullOrWhiteSpace(result.HtmlBody));
            Assert.False(string.IsNullOrWhiteSpace(result.TextBody));
            Assert.StartsWith("<!doctype html>", result.HtmlBody);
            Assert.Contains("role=\"presentation\"", result.HtmlBody);
            Assert.Contains("FINANCE PLATFORM", result.HtmlBody);
            Assert.Contains("Spend<span", result.HtmlBody);
            Assert.Contains(criticalValue, result.HtmlBody);
            Assert.Contains(criticalValue, result.TextBody);
            Assert.DoesNotContain("<script", result.HtmlBody.ToLowerInvariant());
            Assert.DoesNotContain("<img", result.HtmlBody.ToLowerInvariant());
            Assert.DoesNotContain("localhost", result.HtmlBody.ToLowerInvariant());
            Assert.EndsWith("</html>", result.HtmlBody);
        }
    }

    [Fact]
    public void Renderer_html_encodes_dynamic_values_in_named_templates()
    {
        var renderer = new EmailTemplateRenderer();
        const string unsafeOtp = "<4829&1374>";

        var result = renderer.Render("registration-verification", new Dictionary<string, string?>
        {
            ["Email"] = "demo@example.test",
            ["Otp"] = unsafeOtp,
            ["Minutes"] = "10"
        });

        Assert.Contains("&lt;4829&amp;1374&gt;", result.HtmlBody);
        Assert.DoesNotContain(unsafeOtp, result.HtmlBody);
        Assert.Contains(unsafeOtp, result.TextBody);
    }

    [Fact]
    public async Task Queue_deduplicates_same_event_and_preserves_recipient()
    {
        await using var db = CreateDb(nameof(Queue_deduplicates_same_event_and_preserves_recipient));
        var protector = new TestEmailContentProtector();
        var queue = new EmailDeliveryQueue(db, protector);
        var message = new EmailMessage("user@example.test", "Subject", "<p>HTML</p>", "Text");

        var first = await queue.EnqueueAsync(message, "support-case:42", CancellationToken.None);
        var second = await queue.EnqueueAsync(message, " support-case:42 ", CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
        var saved = await db.EmailOutboxMessages.SingleAsync();
        Assert.Equal("user@example.test", saved.RecipientEmail);
        Assert.Equal(EmailDeliveryStatus.Pending, saved.Status);
    }

    [Fact]
    public async Task Dispatcher_retries_failure_and_marks_terminal_failure_after_five_attempts()
    {
        var clock = new FixedClock(DateTime.UtcNow);
        await using var db = CreateDb(nameof(Dispatcher_retries_failure_and_marks_terminal_failure_after_five_attempts), clock);
        db.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            RecipientEmail = "user@example.test",
            Subject = "Subject",
            HtmlBody = "<p>HTML</p>",
            TextBody = "Text",
            DeduplicationKey = "failure:1"
        });
        await db.SaveChangesAsync();
        var sender = new FailingSender();
        var dispatcher = new EmailOutboxDispatcher(
            db,
            sender,
            new TestEmailContentProtector(),
            clock,
            NullLogger<EmailOutboxDispatcher>.Instance);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var item = await db.EmailOutboxMessages.SingleAsync();
            item.NextAttemptAt = null;
            await db.SaveChangesAsync();
            await dispatcher.DispatchPendingAsync(CancellationToken.None);
        }

        var saved = await db.EmailOutboxMessages.SingleAsync();
        Assert.Equal(5, saved.AttemptCount);
        Assert.Equal(EmailDeliveryStatus.Failed, saved.Status);
        Assert.Equal(string.Empty, saved.HtmlBody);
        Assert.Equal(string.Empty, saved.TextBody);
    }

    private static SpendWiseDbContext CreateDb(string name, IClock? clock = null)
    {
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new SpendWiseDbContext(options, clock ?? new FixedClock(DateTime.UtcNow));
    }

    private static IEnumerable<(string TemplateName, Dictionary<string, string?> Values, string CriticalValue)> TemplateCases()
    {
        const string actionUrl = "https://spendwise.example/app";
        yield return ("registration-verification", new()
        {
            ["Email"] = "demo@example.test", ["Otp"] = "48291374", ["Minutes"] = "10"
        }, "48291374");
        yield return ("registration-welcome", new()
        {
            ["UserName"] = "Nguyễn Văn Demo", ["ActionUrl"] = actionUrl, ["ActionText"] = "Truy cập SpendWise"
        }, "Nguyễn Văn Demo");
        yield return ("password-reset-verification", new()
        {
            ["Email"] = "demo@example.test", ["Otp"] = "48291374", ["Minutes"] = "10"
        }, "48291374");
        yield return ("password-reset-success", new()
        {
            ["UserName"] = "Nguyễn Văn Demo", ["ChangedAt"] = "2026-09-29T12:00:00Z",
            ["ActionUrl"] = actionUrl, ["ActionText"] = "Truy cập SpendWise"
        }, "2026-09-29T12:00:00Z");
        yield return ("support-received", new()
        {
            ["TicketNumber"] = "SW-DEMO-001", ["Subject"] = "Yêu cầu hỗ trợ mẫu",
            ["SubmittedAt"] = "29/09/2026 12:00 UTC", ["Status"] = "Đang chờ xử lý", ["ActionUrl"] = actionUrl
        }, "SW-DEMO-001");
        yield return ("support-resolved", new()
        {
            ["TicketNumber"] = "SW-DEMO-001", ["Message"] = "Yêu cầu đã được xử lý.", ["ActionUrl"] = actionUrl
        }, "SW-DEMO-001");
        yield return ("support-rejected", new()
        {
            ["TicketNumber"] = "SW-DEMO-001", ["Message"] = "Cần bổ sung thông tin.", ["ActionUrl"] = actionUrl
        }, "SW-DEMO-001");
        yield return ("welcome-back", new()
        {
            ["UserName"] = "Nguyễn Văn Demo", ["ActionUrl"] = actionUrl
        }, "Nguyễn Văn Demo");
        yield return ("new-device-login", new()
        {
            ["UserName"] = "Nguyễn Văn Demo", ["LoginTime"] = "2026-09-29T12:00:00Z",
            ["DeviceName"] = "Demo device", ["BrowserInfo"] = "Demo browser", ["IpAddress"] = "192.0.2.10",
            ["ActionUrl"] = actionUrl
        }, "Demo device");
        yield return ("security-pin-reset", new()
        {
            ["Token"] = "PIN-DEMO-TOKEN", ["ExpiresAt"] = "2026-09-29T12:10:00Z"
        }, "PIN-DEMO-TOKEN");
    }

    private sealed class FailingSender : ITransactionalEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("provider unavailable");
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }
}
