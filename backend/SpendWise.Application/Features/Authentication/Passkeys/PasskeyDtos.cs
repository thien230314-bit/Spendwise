namespace SpendWise.Application.Features.Authentication.Passkeys;

public sealed record PasskeyRegistrationOptionsResponse(
    string Challenge,
    string RpId,
    string RpName,
    PasskeyUserEntity User,
    IReadOnlyList<PasskeyPubKeyCredParam> PubKeyCredParams,
    int Timeout,
    string Attestation,
    PasskeyAuthenticatorSelection AuthenticatorSelection);

public sealed record PasskeyUserEntity(
    string Id,
    string Name,
    string DisplayName);

public sealed record PasskeyPubKeyCredParam(
    string Type,
    int Alg);

public sealed record PasskeyAuthenticatorSelection(
    string AuthenticatorAttachment,
    string UserVerification,
    bool RequireResidentKey);

public sealed record VerifyPasskeyRegistrationRequest(
    string Challenge,
    string CredentialId,
    string PublicKey,
    string? Algorithm,
    string? DeviceName,
    string? Aaguid,
    string? Transports,
    string? ClientDataJSON = null);

public sealed record PasskeyLoginOptionsRequest(
    string? Email);

public sealed record PasskeyLoginOptionsResponse(
    string Challenge,
    string RpId,
    int Timeout,
    string UserVerification,
    IReadOnlyList<PasskeyCredentialDescriptor> AllowCredentials);

public sealed record PasskeyCredentialDescriptor(
    string Type,
    string Id);

public sealed record VerifyPasskeyLoginRequest(
    string CredentialId,
    string Challenge,
    string Signature,
    string? ClientDataJSON,
    string? AuthenticatorData,
    string? DeviceName);

public sealed record PasskeyCredentialDto(
    Guid Id,
    string CredentialId,
    string Algorithm,
    string DeviceName,
    long SignCount,
    bool UserVerified,
    bool IsRevoked,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? RevokedAt);
