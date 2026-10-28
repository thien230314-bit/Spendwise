using SpendWise.Domain.Common;

namespace SpendWise.Domain.Identity;

public sealed class UserPasskeyCredential : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string CredentialId { get; set; } = string.Empty; // Base64Url credential ID
    public string PublicKey { get; set; } = string.Empty;     // Base64 public key (SPKI DER or Raw EC point)
    public string Algorithm { get; set; } = "ES256";          // "ES256" (-7) or "RS256" (-257)
    public string DeviceName { get; set; } = "Biometric Device";
    public long SignCount { get; set; }
    public string? Aaguid { get; set; }
    public string? Transports { get; set; }
    public bool UserVerified { get; set; } = true;
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
