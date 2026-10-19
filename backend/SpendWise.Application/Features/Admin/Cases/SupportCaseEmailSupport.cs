using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Admin.Cases;

// Tạo email cho từng sự kiện support và đưa vào outbox bằng khóa chống trùng ổn định.
public sealed class SupportCaseEmailSupport(
    IEmailTemplateRenderer renderer,
    IEmailDeliveryQueue emailQueue,
    IEmailVerificationPolicy policy)
{
    public Task<bool> QueueReceivedAsync(
        User owner,
        SupportCase supportCase,
        CancellationToken cancellationToken)
    {
        if (!HasVerifiedEmail(owner)) return Task.FromResult(false);
        var rendered = renderer.Render("support-received", new Dictionary<string, string?>
        {
            ["TicketNumber"] = supportCase.TicketNumber,
            ["Subject"] = supportCase.Subject,
            ["SubmittedAt"] = supportCase.CreatedAt.ToString("dd/MM/yyyy HH:mm 'UTC'"),
            ["Status"] = "Đang chờ xử lý",
            ["ActionUrl"] = CaseUrl(supportCase.Id)
        });

        return emailQueue.EnqueueAsync(
            new EmailMessage(
                owner.Email,
                $"[SpendWise] Đã nhận yêu cầu {supportCase.TicketNumber}",
                rendered.HtmlBody,
                rendered.TextBody),
            $"support-received:{supportCase.Id:N}",
            cancellationToken);
    }

    public Task<bool> QueueResolvedAsync(
        User owner,
        SupportCase supportCase,
        string resolution,
        CancellationToken cancellationToken) =>
        QueueDecisionAsync(
            owner,
            supportCase,
            resolution,
            "support-resolved",
            "[SpendWise] Yêu cầu hỗ trợ đã được giải quyết",
            $"support-resolved:{supportCase.Id:N}",
            cancellationToken);

    public Task<bool> QueueRejectedAsync(
        User owner,
        SupportCase supportCase,
        string reason,
        CancellationToken cancellationToken) =>
        QueueDecisionAsync(
            owner,
            supportCase,
            reason,
            "support-rejected",
            "[SpendWise] Yêu cầu hỗ trợ đã bị từ chối",
            $"support-rejected:{supportCase.Id:N}",
            cancellationToken);

    private Task<bool> QueueDecisionAsync(
        User owner,
        SupportCase supportCase,
        string message,
        string template,
        string subject,
        string deduplicationKey,
        CancellationToken cancellationToken)
    {
        if (!HasVerifiedEmail(owner)) return Task.FromResult(false);
        var rendered = renderer.Render(template, new Dictionary<string, string?>
        {
            ["TicketNumber"] = supportCase.TicketNumber,
            ["Message"] = message,
            ["ActionUrl"] = CaseUrl(supportCase.Id)
        });

        return emailQueue.EnqueueAsync(
            new EmailMessage(owner.Email, subject, rendered.HtmlBody, rendered.TextBody),
            deduplicationKey,
            cancellationToken);
    }

    private string CaseUrl(Guid caseId) =>
        $"{policy.WebsiteUrl.TrimEnd('/')}/support/{caseId}";

    private static bool HasVerifiedEmail(User owner) =>
        owner.EmailVerifiedAt is not null && !string.IsNullOrWhiteSpace(owner.Email);
}
