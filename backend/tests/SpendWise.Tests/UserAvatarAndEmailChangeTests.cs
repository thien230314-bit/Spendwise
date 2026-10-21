using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Users.Commands.ConfirmChangeEmail;
using SpendWise.Application.Features.Users.Commands.RequestChangeEmail;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class UserAvatarAndEmailChangeTests
{
    private static readonly byte[] ValidPngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    [Fact]
    public async Task UploadAvatar_ValidPng_UpdatesUserAvatarAndReturnsProfile()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = CreateUser("Avatar Test User");
            db.Users.Add(user);
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();

            userId = user.Id;
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.CreateAccessToken(user, session.Id).Token);
        }

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(ValidPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "avatar.png");

        var response = await client.PostAsync("/api/users/me/avatar", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<UserProfile>();
        Assert.NotNull(profile);
        Assert.NotNull(profile.AvatarUrl);
        Assert.StartsWith("/avatars/", profile.AvatarUrl);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var savedUser = await db.Users.FindAsync(userId);
            Assert.NotNull(savedUser);
            Assert.Equal(profile.AvatarUrl, savedUser.AvatarUrl);
        }
    }

    [Fact]
    public async Task UploadAvatar_InvalidFileType_ReturnsBadRequest()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = CreateUser("Fake Image User");
            db.Users.Add(user);
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();

            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.CreateAccessToken(user, session.Id).Token);
        }

        using var content = new MultipartFormDataContent();
        var invalidBytes = "This is not an image file"u8.ToArray();
        var fileContent = new ByteArrayContent(invalidBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "avatar.png");

        var response = await client.PostAsync("/api/users/me/avatar", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeEmail_FullLifecycle_RequestAndConfirm_SucceedsAndUpdatesDatabase()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        Guid userId;
        string initialEmail;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = CreateUser("Email Change User");
            db.Users.Add(user);
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();

            userId = user.Id;
            initialEmail = user.Email;
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.CreateAccessToken(user, session.Id).Token);
        }

        var newEmail = $"newemail_{Guid.NewGuid():N}@spendwise.local";

        // Step 1: Request email change
        var requestResponse = await client.PostAsJsonAsync(
            "/api/users/me/email/request",
            new RequestChangeEmailRequest(newEmail));
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        // Verify PendingNewEmail in DB and token generated
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = await db.Users.FindAsync(userId);
            Assert.NotNull(user);
            Assert.Equal(newEmail, user.PendingNewEmail);
            Assert.Equal(initialEmail, user.Email); // Not changed yet!

            var tokens = await db.EmailVerificationTokens
                .Where(t => t.UserId == userId && t.Purpose == EmailVerificationPurpose.ChangeEmail)
                .ToListAsync();
            Assert.NotEmpty(tokens);
        }

        // In test mode, static OTP is configured as "12345678" or let's use StaticOtp
        var confirmResponse = await client.PostAsJsonAsync(
            "/api/users/me/email/confirm",
            new ConfirmChangeEmailRequest("12345678"));
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var updatedProfile = await confirmResponse.Content.ReadFromJsonAsync<UserProfile>();
        Assert.NotNull(updatedProfile);
        Assert.Equal(newEmail, updatedProfile.Email);

        // Verify committed data in DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var finalUser = await db.Users.FindAsync(userId);
            Assert.NotNull(finalUser);
            Assert.Equal(newEmail, finalUser.Email);
            Assert.Equal(newEmail.ToUpperInvariant(), finalUser.NormalizedEmail);
            Assert.Null(finalUser.PendingNewEmail);
            Assert.NotNull(finalUser.EmailVerifiedAt);

            // Verify security notice email was queued for old email
            var outbox = await db.EmailOutboxMessages
                .Where(m => m.RecipientEmail == initialEmail)
                .ToListAsync();
            Assert.NotEmpty(outbox);
        }
    }

    [Fact]
    public async Task RequestEmailChange_DuplicateEmail_ReturnsConflict()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        string existingOtherUserEmail;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var otherUser = CreateUser("Other User");
            db.Users.Add(otherUser);

            var currentUser = CreateUser("Current User");
            db.Users.Add(currentUser);

            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = currentUser.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();

            existingOtherUserEmail = otherUser.Email;
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.CreateAccessToken(currentUser, session.Id).Token);
        }

        var response = await client.PostAsJsonAsync(
            "/api/users/me/email/request",
            new RequestChangeEmailRequest(existingOtherUserEmail));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmailChange_ExpiredOtp_ReturnsBadRequest()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = CreateUser("Expired OTP User");
            db.Users.Add(user);
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();

            userId = user.Id;
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.CreateAccessToken(user, session.Id).Token);
        }

        var newEmail = $"expired_test_{Guid.NewGuid():N}@spendwise.local";
        var requestResponse = await client.PostAsJsonAsync("/api/users/me/email/request", new RequestChangeEmailRequest(newEmail));
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        // Expire the token in DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var token = await db.EmailVerificationTokens
                .Where(t => t.UserId == userId && t.Purpose == EmailVerificationPurpose.ChangeEmail)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        // Try confirm with expired OTP
        var confirmResponse = await client.PostAsJsonAsync("/api/users/me/email/confirm", new ConfirmChangeEmailRequest("12345678"));
        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmailChange_ReusedOtp_ReturnsBadRequest()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = CreateUser("Reused OTP User");
            db.Users.Add(user);
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();

            userId = user.Id;
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.CreateAccessToken(user, session.Id).Token);
        }

        var newEmail = $"reused_test_{Guid.NewGuid():N}@spendwise.local";
        var requestResponse = await client.PostAsJsonAsync("/api/users/me/email/request", new RequestChangeEmailRequest(newEmail));
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        // Confirm 1st time: SUCCESS
        var confirm1 = await client.PostAsJsonAsync("/api/users/me/email/confirm", new ConfirmChangeEmailRequest("12345678"));
        Assert.Equal(HttpStatusCode.OK, confirm1.StatusCode);

        // Confirm 2nd time with reused OTP: FAILS
        var confirm2 = await client.PostAsJsonAsync("/api/users/me/email/confirm", new ConfirmChangeEmailRequest("12345678"));
        Assert.Equal(HttpStatusCode.BadRequest, confirm2.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_InvalidExtension_ReturnsBadRequest()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = CreateUser("Bad Extension User");
            db.Users.Add(user);
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();

            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.CreateAccessToken(user, session.Id).Token);
        }

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(ValidPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "avatar.exe");

        var response = await client.PostAsync("/api/users/me/avatar", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_OversizedFile_ReturnsBadRequest()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = CreateUser("Oversized File User");
            db.Users.Add(user);
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();

            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.CreateAccessToken(user, session.Id).Token);
        }

        // 5 MB + 1 byte
        var oversizedBytes = new byte[5 * 1024 * 1024 + 1];
        // Prepend PNG magic bytes so it passes magic byte check but fails size check
        Array.Copy(ValidPngBytes, oversizedBytes, ValidPngBytes.Length);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(oversizedBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "large_avatar.png");

        var response = await client.PostAsync("/api/users/me/avatar", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Profile_IdentityIsolation_CannotModifyAnotherUser()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        Guid userAId;
        Guid userBId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var userA = CreateUser("User A");
            var userB = CreateUser("User B");
            db.Users.AddRange(userA, userB);
            var sessionA = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = userA.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            db.UserSessions.Add(sessionA);
            await db.SaveChangesAsync();

            userAId = userA.Id;
            userBId = userB.Id;

            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.CreateAccessToken(userA, sessionA.Id).Token);
        }

        // User A uploads avatar
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(ValidPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "avatar_a.png");

        var uploadRes = await client.PostAsync("/api/users/me/avatar", content);
        Assert.Equal(HttpStatusCode.OK, uploadRes.StatusCode);

        // Verify User A has avatar, but User B's profile is untouched
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var dbUserA = await db.Users.FindAsync(userAId);
            var dbUserB = await db.Users.FindAsync(userBId);

            Assert.NotNull(dbUserA?.AvatarUrl);
            Assert.Null(dbUserB?.AvatarUrl); // User B completely untouched!
        }
    }

    private static User CreateUser(string fullName)
    {
        var id = Guid.NewGuid();
        var unique = id.ToString("N");
        return new User
        {
            Id = id,
            FullName = fullName,
            Email = $"{unique}@spendwise.local",
            NormalizedEmail = $"{unique.ToUpperInvariant()}@SPENDWISE.LOCAL",
            SpendId = unique[..12],
            NormalizedSpendId = unique[..12].ToUpperInvariant(),
            PasswordHash = "TestHashPassword123",
            SecurityPinHash = "TestPinHash123",
            EmailVerifiedAt = DateTime.UtcNow,
            SystemRole = SystemRole.User,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }
}
