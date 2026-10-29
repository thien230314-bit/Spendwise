using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Users.Commands.ConfirmChangeEmail;
using SpendWise.Application.Features.Users.Commands.RequestChangeEmail;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

/// <summary>
/// Bộ kiểm thử E2E chuyên sâu về toàn bộ vòng đời Email, Dynamic OTP (không dùng OTP tĩnh),
/// chống replay, vô hiệu hóa mã cũ, đổi mật khẩu, đổi email, reset PIN và hàng đợi Outbox.
/// </summary>
public sealed class RealEmailLifecycleE2ETests
{
    private sealed class DynamicOtpApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        public DynamicOtpApiFactory()
        {
            connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:Key", "SpendWise-HTTP-tests-only-signing-key-2026-at-least-32-bytes");
            builder.UseSetting("Jwt:Issuer", "SpendWise.Tests");
            builder.UseSetting("Jwt:Audience", "SpendWise.Tests");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(local);Database=unused");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "SpendWise-HTTP-tests-only-signing-key-2026-at-least-32-bytes",
                    ["Jwt:Issuer"] = "SpendWise.Tests",
                    ["Jwt:Audience"] = "SpendWise.Tests",
                    ["Email:WebsiteUrl"] = "http://localhost:3000",
                    ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString,
                    ["EmailVerification:OtpPepper"] = "oYk7OMIyI1xUUzrIczOINKXYgtU9SYQpSNKKjkIfUGf7mPXz98k7ZF76uvsp8qYT",
                    // Bỏ hoàn toàn StaticOtp để ép buộc sinh OTP ngẫu nhiên động (CSPRNG 8 chữ số)
                    ["EmailVerification:StaticOtp"] = null,
                    ["EmailVerification:ResendCooldownSeconds"] = "60"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<SpendWiseDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SpendWiseDbContext>>();
                services.RemoveAll<SpendWiseDbContext>();
                services.RemoveAll<ISpendWiseDbContext>();

                services.AddDbContext<SpendWiseDbContext>(options => options.UseSqlite(connection));
                services.AddScoped<ISpendWiseDbContext>(p => p.GetRequiredService<SpendWiseDbContext>());

                // Đăng ký dịch vụ OTP thật (không dùng TestEmailVerificationOtpService trả 12345678)
                services.RemoveAll<IEmailVerificationOtpService>();
                services.AddSingleton<IEmailVerificationOtpService>(sp =>
                {
                    var cfg = sp.GetRequiredService<IConfiguration>();
                    return new EmailVerificationOtpService(cfg, null);
                });
            });
        }

        public HttpClient CreateHttpsClient()
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().Database.EnsureCreated();
            return client;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) connection.Dispose();
        }
    }

    private static string ExtractOtpFromEmailText(string textBody)
    {
        // OTP là chuỗi 8 chữ số trong nội dung email
        var match = Regex.Match(textBody, @"\b\d{8}\b");
        Assert.True(match.Success, $"Không tìm thấy mã OTP 8 chữ số trong nội dung email:\n{textBody}");
        return match.Value;
    }

    [Fact]
    public async Task Registration_DynamicOtp_FromOutbox_FullLifecycle_Succeeds_AndReplayFails()
    {
        using var factory = new DynamicOtpApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"dynamic-reg-{Guid.NewGuid():N}@spendwise.local";

        // 1. Đăng ký tài khoản
        var regResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = "Dynamic OTP User",
            Email = email,
            Phone = "09" + Random.Shared.Next(10000000, 99999999),
            Password = "StrongPass1!",
            ConfirmPassword = "StrongPass1!"
        });
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        // 2. Kiểm tra DB: User chưa active, email chưa verify, và email outbox có message
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IEmailContentProtector>();

        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Null(user.EmailVerifiedAt);

        var outboxMessage = await db.EmailOutboxMessages.SingleAsync(m => m.RecipientEmail == email);
        Assert.Equal(EmailDeliveryStatus.Pending, outboxMessage.Status);
        Assert.Contains("xác thực đăng ký", outboxMessage.Subject);

        // Giải mã nội dung text để trích xuất dynamic OTP thật
        var unencryptedText = protector.Unprotect(outboxMessage.TextBody);
        var dynamicOtp = ExtractOtpFromEmailText(unencryptedText);
        Assert.Equal(8, dynamicOtp.Length);
        Assert.Matches(@"^\d{8}$", dynamicOtp);

        // 3. Thử verify với OTP SAI -> Bị từ chối
        var wrongOtpResponse = await client.PostAsJsonAsync("/api/auth/email-verification/verify", new
        {
            Email = email,
            Otp = "00000000"
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongOtpResponse.StatusCode);

        // 4. Verify với Dynamic OTP THẬT vừa trích xuất -> Thành công, trả challengeToken
        var verifyResponse = await client.PostAsJsonAsync("/api/auth/email-verification/verify", new
        {
            Email = email,
            Otp = dynamicOtp
        });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var challengeObj = await verifyResponse.Content.ReadFromJsonAsync<LoginChallengeResponse>();
        Assert.NotNull(challengeObj);
        Assert.Equal("PIN_SETUP_REQUIRED", challengeObj.Status);
        Assert.False(string.IsNullOrWhiteSpace(challengeObj.ChallengeToken));

        // 5. Thử verify lại lần 2 (Replay attack với mã OTP đã dùng) -> Bị từ chối dứt điểm
        var replayResponse = await client.PostAsJsonAsync("/api/auth/email-verification/verify", new
        {
            Email = email,
            Otp = dynamicOtp
        });
        Assert.Equal(HttpStatusCode.BadRequest, replayResponse.StatusCode);

        // 6. Kiểm tra User trong DB: EmailVerifiedAt đã được đánh dấu
        var verifiedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.NotNull(verifiedUser.EmailVerifiedAt);

        // 7. Thiết lập mã PIN bảo mật
        var pinSetupResponse = await client.PostAsJsonAsync("/api/auth/pin/setup", new
        {
            ChallengeToken = challengeObj.ChallengeToken,
            Pin = "123456",
            ConfirmPin = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, pinSetupResponse.StatusCode);

        // 8. Đăng nhập bình thường
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Identifier = email,
            Password = "StrongPass1!",
            DeviceName = "Email-E2E-Device"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginChallenge = await loginResponse.Content.ReadFromJsonAsync<LoginChallengeResponse>();
        Assert.NotNull(loginChallenge);
        Assert.Equal("PIN_REQUIRED", loginChallenge.Status);

        // Xác nhận PIN để lấy JWT
        var pinVerifyResponse = await client.PostAsJsonAsync("/api/auth/pin/verify", new
        {
            ChallengeToken = loginChallenge.ChallengeToken,
            Pin = "123456",
            DeviceName = "Email-E2E-Device"
        });
        Assert.Equal(HttpStatusCode.OK, pinVerifyResponse.StatusCode);
        var authResult = await pinVerifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResult);
        Assert.False(string.IsNullOrWhiteSpace(authResult.AccessToken));
    }

    [Fact]
    public async Task ResendOtp_DynamicOtp_RevokesPreviousOtp_AndPreventsUse()
    {
        using var factory = new DynamicOtpApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"resend-dyn-{Guid.NewGuid():N}@spendwise.local";

        // 1. Đăng ký -> sinh OTP 1
        var regResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = "Resend Test User",
            Email = email,
            Phone = "09" + Random.Shared.Next(10000000, 99999999),
            Password = "StrongPass1!",
            ConfirmPassword = "StrongPass1!"
        });
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IEmailContentProtector>();

        var firstEmail = await db.EmailOutboxMessages
            .Where(m => m.RecipientEmail == email)
            .OrderBy(m => m.CreatedAt)
            .LastAsync();
        var firstOtp = ExtractOtpFromEmailText(protector.Unprotect(firstEmail.TextBody));

        // 2. Thử resend ngay lập tức -> Dính Cooldown 429
        var immediateResend = await client.PostAsJsonAsync("/api/auth/email-verification/resend", new { Email = email });
        Assert.Equal(HttpStatusCode.TooManyRequests, immediateResend.StatusCode);

        // 3. Giả lập vượt qua Cooldown bằng cách chỉnh CreatedAt của token cũ lùi 2 phút
        var oldToken = await db.EmailVerificationTokens.SingleAsync(t => t.Purpose == EmailVerificationPurpose.Registration);
        oldToken.CreatedAt = DateTime.UtcNow.AddMinutes(-2);
        await db.SaveChangesAsync();

        // 4. Gọi Resend hợp lệ -> Sinh OTP 2
        var resendResponse = await client.PostAsJsonAsync("/api/auth/email-verification/resend", new { Email = email });
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);

        // 5. Kiểm tra DB: Token cũ đã bị RevokedAt
        db.ChangeTracker.Clear();
        var tokens = await db.EmailVerificationTokens.Where(t => t.UserId == oldToken.UserId).OrderBy(t => t.CreatedAt).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].RevokedAt); // Token cũ bị thu hồi
        Assert.Null(tokens[1].RevokedAt);   // Token mới active

        var secondEmail = await db.EmailOutboxMessages
            .Where(m => m.RecipientEmail == email)
            .OrderBy(m => m.CreatedAt)
            .LastAsync();
        var secondOtp = ExtractOtpFromEmailText(protector.Unprotect(secondEmail.TextBody));

        // 6. Thử verify bằng OTP cũ (firstOtp) -> BỊ TỪ CHỐI
        var oldOtpVerify = await client.PostAsJsonAsync("/api/auth/email-verification/verify", new
        {
            Email = email,
            Otp = firstOtp
        });
        Assert.Equal(HttpStatusCode.BadRequest, oldOtpVerify.StatusCode);

        // 7. Verify bằng OTP mới (secondOtp) -> THÀNH CÔNG
        var newOtpVerify = await client.PostAsJsonAsync("/api/auth/email-verification/verify", new
        {
            Email = email,
            Otp = secondOtp
        });
        Assert.Equal(HttpStatusCode.OK, newOtpVerify.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_DynamicOtp_ResetsPassword_AndRevokesSessions()
    {
        using var factory = new DynamicOtpApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"forgot-dyn-{Guid.NewGuid():N}@spendwise.local";

        // Tạo user đã verify sẵn và có phiên active
        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var pwdService = new PasswordService();
            var pinService = new SecurityPinService();

            var u = new User
            {
                FullName = "Forgot User",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                SpendId = "spend_" + Guid.NewGuid().ToString("N")[..8],
                NormalizedSpendId = "SPEND_" + Guid.NewGuid().ToString("N")[..8],
                Phone = "09" + Random.Shared.Next(10000000, 99999999),
                PasswordHash = pwdService.Hash("OldPass1!"),
                SecurityPinHash = pinService.Hash("123456"),
                EmailVerifiedAt = DateTime.UtcNow,
                Status = UserStatus.Active
            };
            db.Users.Add(u);

            // Thêm 1 active session
            db.UserSessions.Add(new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = u.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "InitialAgent"
            });
            await db.SaveChangesAsync();
            userId = u.Id;
        }

        // 1. Yêu cầu Quên mật khẩu
        var forgotResp = await client.PostAsJsonAsync("/api/auth/forgot-password", new { Identifier = email });
        Assert.Equal(HttpStatusCode.Accepted, forgotResp.StatusCode);

        // 2. Trích xuất Dynamic OTP reset từ email outbox
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<IEmailContentProtector>();

            var resetEmail = await db.EmailOutboxMessages.SingleAsync(m => m.RecipientEmail == email && m.Subject.Contains("mật khẩu"));
            var resetOtp = ExtractOtpFromEmailText(protector.Unprotect(resetEmail.TextBody));

            // 3. Đặt lại mật khẩu mới bằng Dynamic OTP
            var resetResp = await client.PostAsJsonAsync("/api/auth/reset-password", new
            {
                Identifier = email,
                Otp = resetOtp,
                NewPassword = "NewStrongPass2!"
            });
            Assert.Equal(HttpStatusCode.NoContent, resetResp.StatusCode);

            // 4. Kiểm tra session cũ đã bị thu hồi hoàn toàn (RevokedAt != null, không còn active session)
            var activeSessions = await db.UserSessions.Where(s => s.UserId == userId && s.RevokedAt == null).ToListAsync();
            Assert.Empty(activeSessions);
            var revokedSession = await db.UserSessions.SingleAsync(s => s.UserId == userId);
            Assert.NotNull(revokedSession.RevokedAt);

            // 5. Kiểm tra email thông báo đổi mật khẩu thành công đã được xếp vào outbox
            var successNotice = await db.EmailOutboxMessages.SingleOrDefaultAsync(m => m.RecipientEmail == email && m.Subject.Contains("đã được đặt lại"));
            Assert.NotNull(successNotice);
        }

        // 6. Đăng nhập bằng mật khẩu cũ -> BỊ TỪ CHỐI (401)
        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Identifier = email,
            Password = "OldPass1!",
            DeviceName = "Device"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // 7. Đăng nhập bằng mật khẩu mới -> THÀNH CÔNG (200)
        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Identifier = email,
            Password = "NewStrongPass2!",
            DeviceName = "Device"
        });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ChangeEmail_DynamicOtp_SentToNewAddress_AndSecurityNoticeSentToOldAddress()
    {
        using var factory = new DynamicOtpApiFactory();
        using var client = factory.CreateHttpsClient();
        var oldEmail = $"old-{Guid.NewGuid():N}@spendwise.local";
        var newEmail = $"new-{Guid.NewGuid():N}@spendwise.local";

        Guid userId;
        string accessToken;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var pwdService = new PasswordService();
            var pinService = new SecurityPinService();

            var u = new User
            {
                FullName = "Change Email User",
                Email = oldEmail,
                NormalizedEmail = oldEmail.ToUpperInvariant(),
                SpendId = "spend_" + Guid.NewGuid().ToString("N")[..8],
                NormalizedSpendId = "SPEND_" + Guid.NewGuid().ToString("N")[..8],
                Phone = "09" + Random.Shared.Next(10000000, 99999999),
                PasswordHash = pwdService.Hash("StrongPass1!"),
                SecurityPinHash = pinService.Hash("123456"),
                EmailVerifiedAt = DateTime.UtcNow,
                Status = UserStatus.Active
            };
            db.Users.Add(u);
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = u.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = "Agent"
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();
            userId = u.Id;

            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            accessToken = tokenService.CreateAccessToken(u, session.Id).Token;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // 1. Gửi yêu cầu đổi sang email mới
        var changeReq = await client.PostAsJsonAsync("/api/users/me/email/request", new RequestChangeEmailRequest(newEmail));
        Assert.Equal(HttpStatusCode.OK, changeReq.StatusCode);

        // 2. Trích xuất Dynamic OTP từ email gửi tới ĐỊA CHỈ MỚI
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<IEmailContentProtector>();

            var emailToNew = await db.EmailOutboxMessages.SingleAsync(m => m.RecipientEmail == newEmail);
            var changeOtp = ExtractOtpFromEmailText(protector.Unprotect(emailToNew.TextBody));

            // 3. Xác nhận đổi email bằng OTP
            var confirmResp = await client.PostAsJsonAsync("/api/users/me/email/confirm", new ConfirmChangeEmailRequest(changeOtp));
            Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

            // 4. Kiểm tra User trong DB: Email chính thức đổi sang newEmail
            var updatedUser = await db.Users.FindAsync(userId);
            Assert.NotNull(updatedUser);
            Assert.Equal(newEmail, updatedUser.Email);
            Assert.Null(updatedUser.PendingNewEmail);

            // 5. Kiểm tra cảnh báo bảo mật đã được gửi về ĐỊA CHỈ CŨ
            var noticeToOld = await db.EmailOutboxMessages.SingleOrDefaultAsync(m => m.RecipientEmail == oldEmail && m.Subject.Contains("thay đổi"));
            Assert.NotNull(noticeToOld);
        }
    }

    [Fact]
    public async Task SecurityPinReset_EmailToken_Lifecycle_Succeeds_AndSingleUseEnforced()
    {
        using var factory = new DynamicOtpApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"pin-reset-{Guid.NewGuid():N}@spendwise.local";

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var pwdService = new PasswordService();
            var pinService = new SecurityPinService();

            var u = new User
            {
                FullName = "PIN Reset User",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                SpendId = "spend_" + Guid.NewGuid().ToString("N")[..8],
                NormalizedSpendId = "SPEND_" + Guid.NewGuid().ToString("N")[..8],
                Phone = "09" + Random.Shared.Next(10000000, 99999999),
                PasswordHash = pwdService.Hash("StrongPass1!"),
                SecurityPinHash = pinService.Hash("123456"),
                EmailVerifiedAt = DateTime.UtcNow,
                Status = UserStatus.Active
            };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            userId = u.Id;
        }

        // 1. Yêu cầu reset mã PIN
        var reqPinReset = await client.PostAsJsonAsync("/api/auth/pin/forgot", new { Identifier = email });
        Assert.Equal(HttpStatusCode.Accepted, reqPinReset.StatusCode);

        // 2. Trích xuất raw token từ email gửi ra
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<IEmailContentProtector>();

            var pinResetEmail = await db.EmailOutboxMessages.SingleAsync(m => m.RecipientEmail == email && m.Subject.Contains("PIN"));
            var text = protector.Unprotect(pinResetEmail.TextBody);

            // Raw token có dạng query string "?token=..."
            var match = Regex.Match(text, @"reset-pin\?token=([^\s""'<>]+)");
            Assert.True(match.Success, $"Không tìm thấy raw token trong email PIN reset:\n{text}");
            var rawToken = Uri.UnescapeDataString(match.Groups[1].Value);

            // 3. Đặt lại PIN bằng raw token hợp lệ
            var resetPinResp = await client.PostAsJsonAsync("/api/auth/pin/reset", new
            {
                Token = rawToken,
                NewPin = "654321",
                ConfirmPin = "654321"
            });
            Assert.Equal(HttpStatusCode.NoContent, resetPinResp.StatusCode);

            // 4. Thử đặt lại PIN lần 2 bằng token cũ -> Bị từ chối
            var replayPinResp = await client.PostAsJsonAsync("/api/auth/pin/reset", new
            {
                Token = rawToken,
                NewPin = "999999",
                ConfirmPin = "999999"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, replayPinResp.StatusCode);
        }
    }

    [Fact]
    public void All_14_Email_Templates_Render_Cleanly_Without_Exceptions()
    {
        var renderer = new EmailTemplateRenderer();
        var templates = new[]
        {
            "registration-verification",
            "registration-welcome",
            "password-reset-verification",
            "password-reset-success",
            "support-received",
            "support-resolved",
            "support-rejected",
            "support-info-request",
            "support-fund-investigation",
            "welcome-back",
            "new-device-login",
            "security-pin-reset",
            "email-change-verification",
            "email-change-notice",
            "generic"
        };

        foreach (var tmpl in templates)
        {
            var rendered = renderer.Render(tmpl, new Dictionary<string, string?>
            {
                ["Email"] = "test@spendwise.local",
                ["UserName"] = "Test User",
                ["Otp"] = "12345678",
                ["Minutes"] = "10",
                ["Token"] = "token123",
                ["ActionUrl"] = "https://spendwise.test/action",
                ["ActionText"] = "Xác nhận",
                ["OldEmail"] = "old@spendwise.local",
                ["NewEmail"] = "new@spendwise.local",
                ["ChangedAt"] = DateTime.UtcNow.ToString("O"),
                ["TicketNumber"] = "SW-TEST-001",
                ["Subject"] = "Test Subject",
                ["SubmittedAt"] = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss"),
                ["Status"] = "Đang xử lý",
                ["Message"] = "Nội dung giải quyết hỗ trợ",
                ["ResolutionNote"] = "Resolved cleanly",
                ["RequiredDocuments"] = "Sao kê ngân hàng MB",
                ["Note"] = "Ghi chú bổ sung",
                ["FundName"] = "Quỹ Thử Nghiệm",
                ["CurrentPhase"] = "Đối soát kế toán",
                ["LoginTime"] = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss"),
                ["DeviceName"] = "iPhone 15 Pro",
                ["BrowserInfo"] = "Safari 17.0",
                ["IpAddress"] = "127.0.0.1"
            });

            Assert.False(string.IsNullOrWhiteSpace(rendered.HtmlBody), $"Template {tmpl} rendered empty HTML body.");
            Assert.False(string.IsNullOrWhiteSpace(rendered.TextBody), $"Template {tmpl} rendered empty Text body.");
        }
    }
}
