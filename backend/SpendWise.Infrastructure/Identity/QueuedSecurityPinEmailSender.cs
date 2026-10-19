using SpendWise.Application.Identity;

namespace SpendWise.Infrastructure.Identity;

// PIN recovery joins the existing encrypted outbox, not an untracked direct SMTP call.
public sealed class QueuedSecurityPinEmailSender(
    IEmailDeliveryQueue queue, IEmailTemplateRenderer renderer,
    IEmailVerificationPolicy policy, ILoginChallengeTokenService tokens) : IEmailSender
{
    public async Task SendSecurityPinResetAsync(string email, string rawToken, DateTime expiresAt, CancellationToken ct)
    {
        var baseUri = new Uri(policy.WebsiteUrl.TrimEnd('/') + "/", UriKind.Absolute);
        if ((baseUri.Scheme != Uri.UriSchemeHttps && !(baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback))
            || baseUri.UserInfo.Length != 0 || baseUri.Query.Length != 0 || baseUri.Fragment.Length != 0)
            throw new InvalidOperationException("PIN recovery requires an HTTPS website URL or loopback HTTP.");
        var url = new Uri(baseUri, "reset-pin").AbsoluteUri + "?token=" + Uri.EscapeDataString(rawToken);
        var content = renderer.Render("security-pin-reset", new Dictionary<string,string?>
        { ["Token"] = rawToken, ["ExpiresAt"] = expiresAt.ToString("O"), ["ActionUrl"] = url });
        await queue.EnqueueAsync(new EmailMessage(email, "SpendWise Security PIN reset", content.HtmlBody,
            content.TextBody, expiresAt), $"email:security-pin-reset:{tokens.HashToken(rawToken)}", ct);
    }
}
