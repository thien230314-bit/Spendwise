using SpendWise.Domain.Common;

namespace SpendWise.Domain.Identity;

public enum PasskeyChallengePurpose
{
    Registration,
    Authentication
}

public sealed class PasskeyChallenge : BaseEntity
{
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string Challenge { get; set; } = string.Empty; // Base64Url 32-byte nonce
    public PasskeyChallengePurpose Purpose { get; set; } = PasskeyChallengePurpose.Registration;
    public string RpId { get; set; } = "spendwise.test";
    public string? Origin { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
