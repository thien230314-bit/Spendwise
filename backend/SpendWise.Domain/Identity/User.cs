using SpendWise.Domain.Common;
using SpendWise.Domain.Banking;

namespace SpendWise.Domain.Identity;

// Các entity này lưu tài khoản, refresh token và phiên đăng nhập của người dùng.

// Preserve legacy numeric values; new platform roles are never inferred from SystemAdmin.
public enum SystemRole { SystemAdmin = 0, User = 1, Admin = 2, SuperAdmin = 3 }
public enum UserStatus { Active, Restricted, Locked, Suspended }
public enum LoginChallengePurpose { PinSetup, PinVerification }
public enum SecurityPinResetPurpose { SelfService, AdminEnrollment }
public enum EmailVerificationPurpose { Registration, ResetPassword, ChangeEmail }

public sealed class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PendingNewEmail { get; set; }
    public string SpendId { get; set; } = string.Empty;
    public string NormalizedSpendId { get; set; } = string.Empty;
    // Phone của tài khoản mới luôn lưu dạng 10 chữ số 0XXXXXXXXX.
    public string? Phone { get; set; }
    // Chỉ lưu salted hash của PIN; null nghĩa là người dùng chưa thiết lập PIN.
    public string? SecurityPinHash { get; set; }
    // Hai bộ đếm độc lập bảo vệ mật khẩu và PIN khỏi thử sai liên tục.
    public int PasswordFailedAttempts { get; set; }
    public DateTime? PasswordLockoutUntil { get; set; }
    public int SecurityPinFailedAttempts { get; set; }
    public DateTime? SecurityPinLockoutUntil { get; set; }
    // Cờ do Admin/recovery đặt để chặn nghiệp vụ cho tới khi đổi mật khẩu.
    public bool MustChangePassword { get; set; }
    // Chỉ lưu chuỗi hash của mật khẩu, không bao giờ lưu plaintext.
    public string PasswordHash { get; set; } = string.Empty;
    public SystemRole SystemRole { get; set; } = SystemRole.User;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public string PreferredLanguage { get; set; } = "vi-VN";
    public string PreferredTheme { get; set; } = "system";
    public string DefaultCurrency { get; set; } = Money.DefaultCurrency;
    // Email tương tác là tùy chọn; mặc định tắt để bảo vệ quyền riêng tư.
    public bool EngagementEmailOptIn { get; set; }
    public DateTime? LastLoginAt { get; set; }
    // Tài khoản do migration/seeder tạo sẵn được xem là đã xác minh; registration mới đặt lại null.
    public DateTime? EmailVerifiedAt { get; set; } = DateTime.UnixEpoch;
    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<UserSession> Sessions { get; set; } = [];
    public List<PasswordResetToken> PasswordResetTokens { get; set; } = [];
    public List<LoginChallenge> LoginChallenges { get; set; } = [];
    public List<SecurityPinResetToken> SecurityPinResetTokens { get; set; } = [];
    public List<EmailVerificationToken> EmailVerificationTokens { get; set; } = [];
    // Các collection này chỉ biểu diễn dữ liệu ngân hàng bên ngoài thuộc người dùng.
    public List<BankConnection> BankConnections { get; set; } = [];
    public List<BankLinkSession> BankLinkSessions { get; set; } = [];
    public List<BankTransaction> BankTransactions { get; set; } = [];
}

// OTP email chỉ lưu HMAC, purpose và trạng thái sử dụng; không lưu mã tám chữ số.
public sealed class EmailVerificationToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string CodeHash { get; set; } = string.Empty;
    public EmailVerificationPurpose Purpose { get; set; } = EmailVerificationPurpose.Registration;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int FailedAttempts { get; set; }
    // Chỉ lưu HMAC của IP để rate-limit, không lưu địa chỉ thô trong token OTP.
    public string? RequestIpHash { get; set; }
}

public sealed class SecurityPinResetToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    // Raw reset token chỉ gửi qua email; database chỉ giữ hash.
    public string TokenHash { get; set; } = string.Empty;
    public SecurityPinResetPurpose Purpose { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? CreatedByAdminId { get; set; }
}

public sealed class LoginChallenge : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    // Raw token chỉ trả cho client một lần; database chỉ giữ SHA-256 hash.
    public string TokenHash { get; set; } = string.Empty;
    public LoginChallengePurpose Purpose { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int FailedAttempts { get; set; }
    public string DeviceName { get; set; } = "Browser";
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public sealed class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    // Database chỉ lưu SHA-256 hash; raw token chỉ được gửi một lần qua email.
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    // Mỗi refresh token thuộc một phiên đăng nhập; rotation tiếp tục dùng cùng phiên này.
    public Guid UserSessionId { get; set; }
    public UserSession UserSession { get; set; } = null!;
    // TokenHash cho phép kiểm tra token mà không lưu giá trị thô bị đánh cắp được.
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    // ReplacedByTokenId ghi lại token mới trong quá trình refresh-token rotation.
    public Guid? ReplacedByTokenId { get; set; }
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    // Token chỉ hoạt động khi chưa thu hồi và chưa hết hạn.
    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}

public sealed class UserSession : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string DeviceName { get; set; } = "Unknown";
    public string? DeviceIdentifier { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime LastSeenAt { get; set; }
    // RevokedAt có giá trị nghĩa là access token gắn với session không còn hợp lệ.
    public DateTime? RevokedAt { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
