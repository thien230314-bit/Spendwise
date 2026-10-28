using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Common.Security;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication.Passkeys;

public sealed record GetPasskeyRegistrationOptionsCommand(Guid UserId) : IRequest<PasskeyRegistrationOptionsResponse>;

public sealed record VerifyPasskeyRegistrationCommand(Guid UserId, VerifyPasskeyRegistrationRequest Request) : IRequest<PasskeyCredentialDto>;

public sealed record GetPasskeyLoginOptionsCommand(string? Email) : IRequest<PasskeyLoginOptionsResponse>;

public sealed record VerifyPasskeyLoginCommand(
    VerifyPasskeyLoginRequest Request,
    string? IpAddress,
    string? UserAgent) : IRequest<AuthResponse>;

public sealed record GetUserPasskeysQuery(Guid UserId) : IRequest<IReadOnlyList<PasskeyCredentialDto>>;

public sealed record RevokePasskeyCommand(Guid UserId, Guid CredentialId) : IRequest<bool>;

public sealed class PasskeyFeatureHandlers(
    ISpendWiseDbContext db,
    AuthenticationSupport authSupport,
    IClock clock) :
    IRequestHandler<GetPasskeyRegistrationOptionsCommand, PasskeyRegistrationOptionsResponse>,
    IRequestHandler<VerifyPasskeyRegistrationCommand, PasskeyCredentialDto>,
    IRequestHandler<GetPasskeyLoginOptionsCommand, PasskeyLoginOptionsResponse>,
    IRequestHandler<VerifyPasskeyLoginCommand, AuthResponse>,
    IRequestHandler<GetUserPasskeysQuery, IReadOnlyList<PasskeyCredentialDto>>,
    IRequestHandler<RevokePasskeyCommand, bool>
{
    private const string DefaultRpId = "spendwise.test";
    private const string DefaultRpName = "SpendWise App";

    public async Task<PasskeyRegistrationOptionsResponse> Handle(
        GetPasskeyRegistrationOptionsCommand request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new AppException("user_not_found", "User was not found.", 404);

        var challengeStr = WebAuthnPasskeySupport.GenerateChallenge(32);
        var challengeEntity = new PasskeyChallenge
        {
            UserId = user.Id,
            Challenge = challengeStr,
            Purpose = PasskeyChallengePurpose.Registration,
            RpId = DefaultRpId,
            ExpiresAt = clock.UtcNow.AddMinutes(5)
        };

        db.PasskeyChallenges.Add(challengeEntity);
        await db.SaveChangesAsync(cancellationToken);

        return new PasskeyRegistrationOptionsResponse(
            Challenge: challengeStr,
            RpId: DefaultRpId,
            RpName: DefaultRpName,
            User: new PasskeyUserEntity(
                Id: user.Id.ToString(),
                Name: user.Email,
                DisplayName: string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName),
            PubKeyCredParams:
            [
                new PasskeyPubKeyCredParam("public-key", -7),   // ES256 (NIST P-256)
                new PasskeyPubKeyCredParam("public-key", -257)  // RS256 (RSA SHA-256)
            ],
            Timeout: 60000,
            Attestation: "none",
            AuthenticatorSelection: new PasskeyAuthenticatorSelection(
                AuthenticatorAttachment: "platform", // Face ID, Touch ID, Windows Hello, Fingerprint
                UserVerification: "preferred",
                RequireResidentKey: false));
    }

    public async Task<PasskeyCredentialDto> Handle(
        VerifyPasskeyRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        var req = command.Request;
        if (string.IsNullOrWhiteSpace(req.Challenge) || string.IsNullOrWhiteSpace(req.CredentialId) || string.IsNullOrWhiteSpace(req.PublicKey))
        {
            throw new AppException("invalid_request", "Challenge, CredentialId, and PublicKey are required.", 400);
        }

        // Validate challenge
        var challenge = await db.PasskeyChallenges
            .FirstOrDefaultAsync(c =>
                c.Challenge == req.Challenge &&
                c.Purpose == PasskeyChallengePurpose.Registration &&
                c.UserId == command.UserId &&
                c.UsedAt == null,
                cancellationToken);

        if (challenge == null || challenge.ExpiresAt < clock.UtcNow)
        {
            throw new AppException("invalid_or_expired_challenge", "Registration challenge is invalid or has expired.", 400);
        }

        // Validate clientDataJSON if provided
        if (!string.IsNullOrWhiteSpace(req.ClientDataJSON))
        {
            if (!WebAuthnPasskeySupport.VerifyClientDataJson(
                req.ClientDataJSON,
                req.Challenge,
                "webauthn.create",
                WebAuthnPasskeySupport.DefaultAllowedOrigins,
                out var clientErr))
            {
                throw new AppException("invalid_client_data", clientErr ?? "Client data validation failed.", 400);
            }
        }

        // Validate public key cryptographic importability
        var algorithm = string.IsNullOrWhiteSpace(req.Algorithm) ? "ES256" : req.Algorithm;
        if (!WebAuthnPasskeySupport.TryImportPublicKey(req.PublicKey, algorithm, out _, out _, out var keyErr))
        {
            throw new AppException("invalid_public_key", keyErr ?? "The provided public key is invalid.", 400);
        }

        // Check if CredentialId is already in use by an active credential
        var existing = await db.UserPasskeyCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == req.CredentialId && !c.IsRevoked, cancellationToken);
        if (existing != null)
        {
            throw new AppException("credential_already_registered", "This passkey credential ID is already registered.", 409);
        }

        var credential = new UserPasskeyCredential
        {
            UserId = command.UserId,
            CredentialId = req.CredentialId,
            PublicKey = req.PublicKey,
            Algorithm = algorithm,
            DeviceName = string.IsNullOrWhiteSpace(req.DeviceName) ? "Biometric Device" : req.DeviceName,
            Aaguid = req.Aaguid,
            Transports = req.Transports,
            SignCount = 0,
            UserVerified = true,
            IsRevoked = false
        };

        challenge.UsedAt = clock.UtcNow;
        db.UserPasskeyCredentials.Add(credential);
        await db.SaveChangesAsync(cancellationToken);

        return new PasskeyCredentialDto(
            credential.Id,
            credential.CredentialId,
            credential.Algorithm,
            credential.DeviceName,
            credential.SignCount,
            credential.UserVerified,
            credential.IsRevoked,
            credential.CreatedAt,
            credential.LastUsedAt,
            credential.RevokedAt);
    }

    public async Task<PasskeyLoginOptionsResponse> Handle(
        GetPasskeyLoginOptionsCommand request,
        CancellationToken cancellationToken)
    {
        Guid? userId = null;
        var allowCredentials = new List<PasskeyCredentialDescriptor>();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), cancellationToken);
            if (user != null)
            {
                userId = user.Id;
                var credentials = await db.UserPasskeyCredentials
                    .Where(c => c.UserId == user.Id && !c.IsRevoked)
                    .Select(c => new PasskeyCredentialDescriptor("public-key", c.CredentialId))
                    .ToListAsync(cancellationToken);
                allowCredentials.AddRange(credentials);
            }
        }

        var challengeStr = WebAuthnPasskeySupport.GenerateChallenge(32);
        var challengeEntity = new PasskeyChallenge
        {
            UserId = userId,
            Challenge = challengeStr,
            Purpose = PasskeyChallengePurpose.Authentication,
            RpId = DefaultRpId,
            ExpiresAt = clock.UtcNow.AddMinutes(5)
        };

        db.PasskeyChallenges.Add(challengeEntity);
        await db.SaveChangesAsync(cancellationToken);

        return new PasskeyLoginOptionsResponse(
            Challenge: challengeStr,
            RpId: DefaultRpId,
            Timeout: 60000,
            UserVerification: "preferred",
            AllowCredentials: allowCredentials);
    }

    public async Task<AuthResponse> Handle(
        VerifyPasskeyLoginCommand command,
        CancellationToken cancellationToken)
    {
        var req = command.Request;
        if (string.IsNullOrWhiteSpace(req.Challenge) || string.IsNullOrWhiteSpace(req.CredentialId) || string.IsNullOrWhiteSpace(req.Signature))
        {
            throw new AppException("invalid_request", "Challenge, CredentialId, and Signature are required.", 400);
        }

        // Validate challenge
        var challenge = await db.PasskeyChallenges
            .FirstOrDefaultAsync(c =>
                c.Challenge == req.Challenge &&
                c.Purpose == PasskeyChallengePurpose.Authentication &&
                c.UsedAt == null,
                cancellationToken);

        if (challenge == null || challenge.ExpiresAt < clock.UtcNow)
        {
            throw new AppException("invalid_or_expired_challenge", "Authentication challenge is invalid or has expired.", 401);
        }

        // Find credential
        var credential = await db.UserPasskeyCredentials
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CredentialId == req.CredentialId && !c.IsRevoked, cancellationToken);

        if (credential == null)
        {
            throw new AppException("credential_not_found_or_revoked", "Passkey credential was not found or has been revoked.", 401);
        }

        // Verify cryptographic assertion signature
        bool verified = WebAuthnPasskeySupport.VerifyPasskeyAssertion(
            publicKeyString: credential.PublicKey,
            algorithm: credential.Algorithm,
            signatureString: req.Signature,
            clientDataJsonString: req.ClientDataJSON,
            authenticatorDataString: req.AuthenticatorData,
            expectedChallenge: req.Challenge,
            expectedRpId: DefaultRpId,
            allowedOrigins: WebAuthnPasskeySupport.DefaultAllowedOrigins,
            requireWebAuthn: false,
            requireUserVerification: false,
            signCount: out var signCount,
            userVerified: out var userVerified,
            errorMessage: out var error);

        if (!verified)
        {
            throw new AppException("biometric_verification_failed", error ?? "Biometric signature verification failed.", 401);
        }

        // Check for authenticator cloning / counter rollback if counter is used
        if (signCount > 0 && credential.SignCount > 0 && signCount <= credential.SignCount)
        {
            throw new AppException("authenticator_counter_mismatch", "Potential authenticator replay detected.", 401);
        }

        // Mark challenge used and update credential state
        challenge.UsedAt = clock.UtcNow;
        if (signCount > credential.SignCount)
        {
            credential.SignCount = signCount;
        }
        credential.LastUsedAt = clock.UtcNow;
        credential.UserVerified = userVerified;

        await db.SaveChangesAsync(cancellationToken);

        // Issue JWT, Refresh Token and Session
        var user = credential.User;
        var deviceName = !string.IsNullOrWhiteSpace(req.DeviceName) ? req.DeviceName : credential.DeviceName;

        return await authSupport.IssueAsync(
            user: user,
            device: deviceName,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PasskeyCredentialDto>> Handle(
        GetUserPasskeysQuery query,
        CancellationToken cancellationToken)
    {
        return await db.UserPasskeyCredentials
            .Where(c => c.UserId == query.UserId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new PasskeyCredentialDto(
                c.Id,
                c.CredentialId,
                c.Algorithm,
                c.DeviceName,
                c.SignCount,
                c.UserVerified,
                c.IsRevoked,
                c.CreatedAt,
                c.LastUsedAt,
                c.RevokedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> Handle(
        RevokePasskeyCommand command,
        CancellationToken cancellationToken)
    {
        var credential = await db.UserPasskeyCredentials
            .FirstOrDefaultAsync(c => c.Id == command.CredentialId && c.UserId == command.UserId, cancellationToken)
            ?? throw new AppException("credential_not_found", "Passkey credential was not found.", 404);

        if (credential.IsRevoked)
        {
            return true;
        }

        credential.IsRevoked = true;
        credential.RevokedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
