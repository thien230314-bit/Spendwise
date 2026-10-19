namespace SpendWise.Application.Identity;

// Các interface nhỏ này tách token xác thực và việc gửi email khỏi Application.

public interface ILoginChallengeTokenService
{
    string CreateToken();
    string HashToken(string token);
}

public interface ISecurityPinService
{
    string Hash(string pin);
    bool Verify(string pin, string encodedHash);
}

public interface ITemporaryPasswordService
{
    string CreateStrongPassword();
}

public interface IEmailSender
{
    Task SendSecurityPinResetAsync(
        string email,
        string rawToken,
        DateTime expiresAt,
        CancellationToken cancellationToken);
}

// Kết quả template gồm HTML cho client hiện đại và text fallback cho mailbox đơn giản.
public sealed record RenderedEmail(string HtmlBody, string TextBody);

// Dịch vụ render template dùng chung cho mọi email SpendWise.
public interface IEmailTemplateRenderer
{
    RenderedEmail Render(string templateName, IReadOnlyDictionary<string, string?> values);
}

// Sender tổng quát để outbox gửi email mà không phá interface recovery hiện tại.
public sealed record EmailMessage(
    string RecipientEmail,
    string Subject,
    string HtmlBody,
    string TextBody,
    DateTime? ContentExpiresAt = null);

public interface ITransactionalEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

// Queue bền vững; deduplication key giúp cùng một sự kiện không xếp hàng hai lần.
public interface IEmailDeliveryQueue
{
    Task<bool> EnqueueAsync(
        EmailMessage message,
        string deduplicationKey,
        CancellationToken cancellationToken);
}

// Chính sách OTP được đọc từ configuration nhưng Application chỉ phụ thuộc abstraction này.
public interface IEmailVerificationPolicy
{
    TimeSpan OtpLifetime { get; }
    TimeSpan ResendCooldown { get; }
    int MaximumFailedAttempts { get; }
    int MaximumSendsPerHourPerAccount { get; }
    int MaximumSendsPerHourPerIp { get; }
    string WebsiteUrl { get; }
}

// OTP dùng CSPRNG; HMAC tách theo User và purpose để chống dò offline khi lộ database.
public interface IEmailVerificationOtpService
{
    string CreateOtp();
    string HashOtp(Guid userId, string purpose, string otp);
    bool VerifyOtp(Guid userId, string purpose, string otp, string expectedHash);
    string HashRateLimitKey(string value);
}
