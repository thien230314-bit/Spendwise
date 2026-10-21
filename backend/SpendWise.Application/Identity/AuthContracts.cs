namespace SpendWise.Application.Identity;

// Các DTO này mang dữ liệu đăng ký, đăng nhập, làm mới token và hồ sơ người dùng.

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string ConfirmPassword);
public sealed record LoginRequest(string Identifier, string Password, string? DeviceName = null);
public sealed record RefreshRequest(string RefreshToken, string? DeviceName = null);
public sealed record ForgotPasswordRequest(string Identifier);
public sealed record ResetPasswordRequest(string Identifier, string Otp, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record LoginChallengeResponse(string ChallengeToken, string Status, DateTime ExpiresAt);
public sealed record EmailVerificationPendingResponse(
    string Status,
    DateTime ExpiresAt,
    int ResendAfterSeconds);
public sealed record VerifyRegistrationEmailRequest(string Email, string Otp, string? DeviceName = null);
public sealed record ResendRegistrationEmailRequest(string Email);
public sealed record SetupSecurityPinRequest(string ChallengeToken, string Pin, string ConfirmPin);
public sealed record CompleteSecurityPinLoginRequest(
    string ChallengeToken,
    string Pin,
    string? DeviceTrustToken = null);
public sealed record VerifySecurityPinRequest(string Pin);
public sealed record ChangeSecurityPinRequest(string CurrentPin, string NewPin, string ConfirmPin);
public sealed record SecurityPinStatusDto(bool IsSet);
public sealed record ForgotSecurityPinRequest(string Identifier);
public sealed record ResetSecurityPinRequest(string Token, string NewPin, string ConfirmPin);
public sealed record ValidateSecurityPinResetTokenRequest(string Token);
public sealed record SecurityPinResetTokenStatusDto(bool Valid, DateTime ExpiresAt);
public sealed record UpdatePreferencesRequest(
    string PreferredLanguage,
    string PreferredTheme,
    string DefaultCurrency,
    bool? EngagementEmailOptIn = null);
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    UserProfile User,
    string? DeviceTrustToken = null);
public sealed record UserProfile(Guid Id, string FullName, string Email, string SpendId, string? Phone, string SystemRole,
    string Status, bool MustChangePassword, string PreferredLanguage, string PreferredTheme, string DefaultCurrency,
    DateTime CreatedAt, DateTime? LastLoginAt, bool EngagementEmailOptIn, string? AvatarUrl = null);
