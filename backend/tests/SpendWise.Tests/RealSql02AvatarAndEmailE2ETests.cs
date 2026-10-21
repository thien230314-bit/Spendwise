using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin.Users.Queries.SearchAdminUsers;
using SpendWise.Application.Features.Users.Commands.ConfirmChangeEmail;
using SpendWise.Application.Features.Users.Commands.RequestChangeEmail;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Identity;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class RealSql02AvatarAndEmailE2ETests
{
    private const string Sql02Connection =
        @"Server=DESKTOP-B5KA3LT\SQLEXPRESS02;Database=SpendWise_E2E_G4;Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly byte[] ValidPngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    [Fact]
    public async Task Real_SQL02_Avatar_Full_Lifecycle_E2E()
    {
        Environment.SetEnvironmentVariable("SPENDWISE_SQL_TEST_INSTANCE", @"DESKTOP-B5KA3LT\SQLEXPRESS02");
        using var factory = new SpendWiseApiFactory(sqlConnection: Sql02Connection);
        using var client = factory.CreateHttpsClient();

        Guid userId;
        string userEmail;
        string adminEmail;
        string password = "StrongPass1";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            var pinService = new SecurityPinService();

            var uniqueUser = Guid.NewGuid().ToString("N");
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "SQL02 Avatar Test User",
                Email = $"sql02_avatar_{uniqueUser}@spendwise.test",
                NormalizedEmail = $"SQL02_AVATAR_{uniqueUser.ToUpperInvariant()}@SPENDWISE.TEST",
                SpendId = uniqueUser[..12],
                NormalizedSpendId = uniqueUser[..12].ToUpperInvariant(),
                PasswordHash = passwordService.Hash(password),
                SecurityPinHash = pinService.Hash(IdentityTestData.DefaultPin),
                EmailVerifiedAt = DateTime.UtcNow,
                SystemRole = SystemRole.User,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);

            var uniqueAdmin = Guid.NewGuid().ToString("N");
            var admin = new User
            {
                Id = Guid.NewGuid(),
                FullName = "SQL02 Admin User",
                Email = $"sql02_admin_{uniqueAdmin}@spendwise.test",
                NormalizedEmail = $"SQL02_ADMIN_{uniqueAdmin.ToUpperInvariant()}@SPENDWISE.TEST",
                SpendId = uniqueAdmin[..12],
                NormalizedSpendId = uniqueAdmin[..12].ToUpperInvariant(),
                PasswordHash = passwordService.Hash(password),
                SecurityPinHash = pinService.Hash(IdentityTestData.DefaultPin),
                EmailVerifiedAt = DateTime.UtcNow,
                SystemRole = SystemRole.Admin,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(admin);

            await db.SaveChangesAsync();

            userId = user.Id;
            userEmail = user.Email;
            adminEmail = admin.Email;
        }

        // 1. Login user through real API
        var userAuth = await IdentityTestData.LoginAndVerifyPinAsync(client, userEmail, password);
        Assert.NotNull(userAuth.AccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);

        // 2. GET initial profile
        var initialProfile = await client.GetFromJsonAsync<UserProfile>("/api/users/me");
        Assert.NotNull(initialProfile);
        Assert.Null(initialProfile.AvatarUrl);

        // 3. POST avatar image
        using var avatarContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(ValidPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        avatarContent.Add(fileContent, "file", "avatar.png");

        var uploadResponse = await client.PostAsync("/api/users/me/avatar", avatarContent);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var updatedProfile = await uploadResponse.Content.ReadFromJsonAsync<UserProfile>();
        Assert.NotNull(updatedProfile);
        Assert.NotNull(updatedProfile.AvatarUrl);
        Assert.StartsWith("/avatars/", updatedProfile.AvatarUrl);

        // 4. GET uploaded image URL via HTTP
        var imageResponse = await client.GetAsync(updatedProfile.AvatarUrl);
        Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
        var fetchedImageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
        Assert.True(fetchedImageBytes.Length > 0);

        // 5. Query SQL02 directly to verify database persistence
        await using (var connection = new SqlConnection(Sql02Connection))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT AvatarUrl FROM Users WHERE Id = @UserId";
            cmd.Parameters.AddWithValue("@UserId", userId);
            var dbAvatarUrl = await cmd.ExecuteScalarAsync() as string;
            Assert.Equal(updatedProfile.AvatarUrl, dbAvatarUrl);
        }

        // 6. Simulate logout and login again
        client.DefaultRequestHeaders.Authorization = null;
        var reAuth = await IdentityTestData.LoginAndVerifyPinAsync(client, userEmail, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reAuth.AccessToken);

        var freshProfile = await client.GetFromJsonAsync<UserProfile>("/api/users/me");
        Assert.NotNull(freshProfile);
        Assert.Equal(updatedProfile.AvatarUrl, freshProfile.AvatarUrl);

        // 7. Verify Admin sees the user in V1 directory and MediatR SearchAdminUsersQuery includes AvatarUrl
        client.DefaultRequestHeaders.Authorization = null;
        var adminAuth = await IdentityTestData.LoginAndVerifyPinAsync(client, adminEmail, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);

        // V1 Admin API: /api/v1/admin/users/search
        var searchResponse = await client.PostAsJsonAsync("/api/v1/admin/users/search", new { reason = "Audit avatar change", search = userEmail });
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        // MediatR Admin query verification (maps to AdminUserDto including AvatarUrl)
        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var adminUsers = await mediator.Send(new SearchAdminUsersQuery(userEmail));
            var targetUserInAdmin = adminUsers.FirstOrDefault(u => u.Id == userId);
            Assert.NotNull(targetUserInAdmin);
            Assert.Equal(updatedProfile.AvatarUrl, targetUserInAdmin.AvatarUrl);
        }
    }

    [Fact]
    public async Task Real_SQL02_Email_Full_Lifecycle_E2E()
    {
        Environment.SetEnvironmentVariable("SPENDWISE_SQL_TEST_INSTANCE", @"DESKTOP-B5KA3LT\SQLEXPRESS02");
        using var factory = new SpendWiseApiFactory(sqlConnection: Sql02Connection);
        using var client = factory.CreateHttpsClient();

        Guid userId;
        Guid walletId;
        string initialEmail;
        string adminEmail;
        string password = "StrongPass1";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            var pinService = new SecurityPinService();

            var uniqueUser = Guid.NewGuid().ToString("N");
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "SQL02 Email Test User",
                Email = $"sql02_email_{uniqueUser}@spendwise.test",
                NormalizedEmail = $"SQL02_EMAIL_{uniqueUser.ToUpperInvariant()}@SPENDWISE.TEST",
                SpendId = uniqueUser[..12],
                NormalizedSpendId = uniqueUser[..12].ToUpperInvariant(),
                PasswordHash = passwordService.Hash(password),
                SecurityPinHash = pinService.Hash(IdentityTestData.DefaultPin),
                EmailVerifiedAt = DateTime.UtcNow,
                SystemRole = SystemRole.User,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);

            var wallet = WalletFactory.Create(user.Id, "Ví Chính Test", WalletType.Main, true);
            wallet.AvailableBalance = 500_000;
            wallet.LedgerAccount.Balance = 500_000;
            db.Wallets.Add(wallet);

            var uniqueAdmin = Guid.NewGuid().ToString("N");
            var admin = new User
            {
                Id = Guid.NewGuid(),
                FullName = "SQL02 Admin User 2",
                Email = $"sql02_admin2_{uniqueAdmin}@spendwise.test",
                NormalizedEmail = $"SQL02_ADMIN2_{uniqueAdmin.ToUpperInvariant()}@SPENDWISE.TEST",
                SpendId = uniqueAdmin[..12],
                NormalizedSpendId = uniqueAdmin[..12].ToUpperInvariant(),
                PasswordHash = passwordService.Hash(password),
                SecurityPinHash = pinService.Hash(IdentityTestData.DefaultPin),
                EmailVerifiedAt = DateTime.UtcNow,
                SystemRole = SystemRole.Admin,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(admin);

            await db.SaveChangesAsync();

            userId = user.Id;
            walletId = wallet.Id;
            initialEmail = user.Email;
            adminEmail = admin.Email;
        }

        // 1. Log in user
        var userAuth = await IdentityTestData.LoginAndVerifyPinAsync(client, initialEmail, password);
        Assert.NotNull(userAuth.AccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);

        // 2. Request change email to new email
        var newEmail = $"sql02_verified_{Guid.NewGuid():N}@spendwise.test";
        var requestResponse = await client.PostAsJsonAsync(
            "/api/users/me/email/request",
            new RequestChangeEmailRequest(newEmail));
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        // 3. Query SQL02 directly: PendingNewEmail is set, official Email is UNCHANGED
        await using (var connection = new SqlConnection(Sql02Connection))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Email, PendingNewEmail FROM Users WHERE Id = @UserId";
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(initialEmail, reader.GetString(0)); // NOT changed!
            Assert.Equal(newEmail, reader.GetString(1));    // Pending!
        }

        // 4. Confirm with WRONG OTP -> rejected
        var wrongOtpResponse = await client.PostAsJsonAsync(
            "/api/users/me/email/confirm",
            new ConfirmChangeEmailRequest("99999999"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongOtpResponse.StatusCode);

        // Recheck DB: still initial email
        await using (var connection = new SqlConnection(Sql02Connection))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Email, PendingNewEmail FROM Users WHERE Id = @UserId";
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(initialEmail, reader.GetString(0));
            Assert.Equal(newEmail, reader.GetString(1));
        }

        // 5. Confirm with CORRECT OTP ("12345678" in test environment)
        var confirmResponse = await client.PostAsJsonAsync(
            "/api/users/me/email/confirm",
            new ConfirmChangeEmailRequest("12345678"));
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var updatedProfile = await confirmResponse.Content.ReadFromJsonAsync<UserProfile>();
        Assert.NotNull(updatedProfile);
        Assert.Equal(newEmail, updatedProfile.Email);

        // 6. Direct SQL02 query: Users.Email updated, PendingNewEmail cleared, EmailVerifiedAt updated
        await using (var connection = new SqlConnection(Sql02Connection))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Email, NormalizedEmail, PendingNewEmail, EmailVerifiedAt FROM Users WHERE Id = @UserId";
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(newEmail, reader.GetString(0));
            Assert.Equal(newEmail.ToUpperInvariant(), reader.GetString(1));
            Assert.True(await reader.IsDBNullAsync(2)); // PendingNewEmail cleared!
            Assert.False(await reader.IsDBNullAsync(3)); // EmailVerifiedAt set!
        }

        // 7. Verify security notice email was queued in SQL02 for old email
        await using (var connection = new SqlConnection(Sql02Connection))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM EmailOutboxMessages WHERE RecipientEmail = @OldEmail";
            cmd.Parameters.AddWithValue("@OldEmail", initialEmail);
            var noticeCount = (int)(await cmd.ExecuteScalarAsync())!;
            Assert.True(noticeCount > 0);
        }

        // 8. Re-authenticate with NEW email: SUCCESS
        client.DefaultRequestHeaders.Authorization = null;
        var reAuthNewEmail = await IdentityTestData.LoginAndVerifyPinAsync(client, newEmail, password);
        Assert.NotNull(reAuthNewEmail.AccessToken);
        Assert.Equal(newEmail, reAuthNewEmail.User.Email);

        // 9. Attempt authenticate with OLD email: FAILS
        var failOldEmailLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(initialEmail, password, "Test Browser"));
        Assert.Equal(HttpStatusCode.Unauthorized, failOldEmailLogin.StatusCode);

        // 10. Admin verifies updated email on SQL02 via V1 Admin API and MediatR
        var adminAuth = await IdentityTestData.LoginAndVerifyPinAsync(client, adminEmail, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);

        var searchResponse = await client.PostAsJsonAsync("/api/v1/admin/users/search", new { reason = "Audit email change", search = newEmail });
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var adminUsers = await mediator.Send(new SearchAdminUsersQuery(newEmail));
            var targetUserInAdmin = adminUsers.FirstOrDefault(u => u.Id == userId);
            Assert.NotNull(targetUserInAdmin);
            Assert.Equal(newEmail, targetUserInAdmin.Email);
            Assert.True(targetUserInAdmin.EmailVerified);
        }

        // 11. Verify user's wallet is completely preserved in SQL02
        await using (var connection = new SqlConnection(Sql02Connection))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT AvailableBalance FROM Wallets WHERE Id = @WalletId AND OwnerUserId = @UserId";
            cmd.Parameters.AddWithValue("@WalletId", walletId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var walletBalance = (decimal)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal(500_000, walletBalance);
        }
    }
}
