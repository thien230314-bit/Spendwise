using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Users.Commands.ChangeEmail;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class EmailChangeSecurityTests
{
    private static User CreateUser(string fullName, string email)
    {
        var id = Guid.NewGuid();
        var unique = id.ToString("N");
        return new User
        {
            Id = id,
            FullName = fullName,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SpendId = unique[..12],
            NormalizedSpendId = unique[..12].ToUpperInvariant(),
            PasswordHash = "TestPasswordHash123",
            SecurityPinHash = "TestPinHash123",
            EmailVerifiedAt = DateTime.UtcNow,
            SystemRole = SystemRole.User,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static (Guid userId, Guid sessionId, string token) SetupUserClient(
        SpendWiseApiFactory factory,
        HttpClient client,
        string email,
        string fullName = "Security Test User")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = CreateUser(fullName, email);
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
        db.SaveChanges();

        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokenService.CreateAccessToken(user, session.Id).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (user.Id, session.Id, token);
    }

    [Fact]
    public async Task Test01_OTP_EmailCuDung_Va_OTPMoiDung_EmailCapNhatThanhCong()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var initialEmail = $"user_{Guid.NewGuid():N}@spendwise.local";
        var (userId, sessionId, _) = SetupUserClient(factory, client, initialEmail);

        var newEmail = $"verified_{Guid.NewGuid():N}@spendwise.local";

        // Giai đoạn A: Yêu cầu OTP email gốc
        var reqCurr = await client.PostAsync("/api/users/me/email/current/request", null);
        Assert.Equal(HttpStatusCode.OK, reqCurr.StatusCode);

        // Giai đoạn A: Xác thực OTP email gốc
        var verifyCurr = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        Assert.Equal(HttpStatusCode.OK, verifyCurr.StatusCode);
        var ticketData = await verifyCurr.Content.ReadFromJsonAsync<VerifyCurrentEmailOtpResponse>();
        Assert.NotNull(ticketData?.Ticket);

        // Giai đoạn B: Yêu cầu OTP email mới kèm ticket
        var reqNew = await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticketData.Ticket, newEmail));
        Assert.Equal(HttpStatusCode.OK, reqNew.StatusCode);

        // Giai đoạn C: Xác thực OTP email mới kèm ticket
        var confirmNew = await client.PostAsJsonAsync("/api/users/me/email/new/confirm", new ConfirmNewEmailOtpRequest(ticketData.Ticket, "12345678"));
        Assert.Equal(HttpStatusCode.OK, confirmNew.StatusCode);
        var profile = await confirmNew.Content.ReadFromJsonAsync<UserProfile>();
        Assert.NotNull(profile);
        Assert.Equal(newEmail, profile.Email);

        // Đối chiếu DB
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var userInDb = await db.Users.FindAsync(userId);
        Assert.NotNull(userInDb);
        Assert.Equal(newEmail, userInDb.Email);
        Assert.Null(userInDb.PendingNewEmail);
    }

    [Fact]
    public async Task Test02_ChuaXacMinhEmailCu_KhongDuocGuiOTPMoi()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"user_{Guid.NewGuid():N}@spendwise.local";
        SetupUserClient(factory, client, email);

        var newEmail = $"target_{Guid.NewGuid():N}@spendwise.local";

        // Gọi thẳng new/request với ticket rác hoặc rỗng
        var res = await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest("bogus-ticket-123", newEmail));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var resEmpty = await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest("", newEmail));
        Assert.Equal(HttpStatusCode.BadRequest, resEmpty.StatusCode);
    }

    [Fact]
    public async Task Test03_OTPEmailCuSai_BiTuChoi()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"user_{Guid.NewGuid():N}@spendwise.local";
        SetupUserClient(factory, client, email);

        await client.PostAsync("/api/users/me/email/current/request", null);

        var verifyRes = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("00000000"));
        Assert.Equal(HttpStatusCode.BadRequest, verifyRes.StatusCode);
    }

    [Fact]
    public async Task Test04_OTPEmailCuHetHan_BiTuChoi()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"user_{Guid.NewGuid():N}@spendwise.local";
        var (userId, _, _) = SetupUserClient(factory, client, email);

        await client.PostAsync("/api/users/me/email/current/request", null);

        // Làm hết hạn token trong DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var token = await db.EmailVerificationTokens
                .Where(t => t.UserId == userId && t.Purpose == EmailVerificationPurpose.ChangeEmailCurrent)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        var verifyRes = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        Assert.Equal(HttpStatusCode.BadRequest, verifyRes.StatusCode);
    }

    [Fact]
    public async Task Test05_ReplayOTPCu_BiTuChoi()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"user_{Guid.NewGuid():N}@spendwise.local";
        SetupUserClient(factory, client, email);

        await client.PostAsync("/api/users/me/email/current/request", null);

        // Lần 1: Thành công
        var verify1 = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        Assert.Equal(HttpStatusCode.OK, verify1.StatusCode);

        // Lần 2 với cùng OTP: Từ chối
        var verify2 = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        Assert.Equal(HttpStatusCode.BadRequest, verify2.StatusCode);
    }

    [Fact]
    public async Task Test06_OTPEmailMoiSai_KhongThayDoiEmail()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var initialEmail = $"user_{Guid.NewGuid():N}@spendwise.local";
        var (userId, _, _) = SetupUserClient(factory, client, initialEmail);

        var newEmail = $"target_{Guid.NewGuid():N}@spendwise.local";

        await client.PostAsync("/api/users/me/email/current/request", null);
        var verifyCurr = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        var ticket = (await verifyCurr.Content.ReadFromJsonAsync<VerifyCurrentEmailOtpResponse>())!.Ticket;

        await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticket, newEmail));

        // Nhập sai OTP mới
        var confirmRes = await client.PostAsJsonAsync("/api/users/me/email/new/confirm", new ConfirmNewEmailOtpRequest(ticket, "00000000"));
        Assert.Equal(HttpStatusCode.BadRequest, confirmRes.StatusCode);

        // Kiểm tra email không đổi trong DB
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var userInDb = await db.Users.FindAsync(userId);
        Assert.NotNull(userInDb);
        Assert.Equal(initialEmail, userInDb.Email);
    }

    [Fact]
    public async Task Test07_OTPEmailMoiHetHan_KhongThayDoiEmail()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var initialEmail = $"user_{Guid.NewGuid():N}@spendwise.local";
        var (userId, _, _) = SetupUserClient(factory, client, initialEmail);

        var newEmail = $"target_{Guid.NewGuid():N}@spendwise.local";

        await client.PostAsync("/api/users/me/email/current/request", null);
        var verifyCurr = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        var ticket = (await verifyCurr.Content.ReadFromJsonAsync<VerifyCurrentEmailOtpResponse>())!.Ticket;

        await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticket, newEmail));

        // Cho token mới hết hạn
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var token = await db.EmailVerificationTokens
                .Where(t => t.UserId == userId && t.Purpose == EmailVerificationPurpose.ChangeEmailNew)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var confirmRes = await client.PostAsJsonAsync("/api/users/me/email/new/confirm", new ConfirmNewEmailOtpRequest(ticket, "12345678"));
        Assert.Equal(HttpStatusCode.BadRequest, confirmRes.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var userInDb = await db.Users.FindAsync(userId);
            Assert.Equal(initialEmail, userInDb!.Email);
        }
    }

    [Fact]
    public async Task Test08_DungOTPCuThayOTPMoi_BiTuChoi()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var initialEmail = $"user_{Guid.NewGuid():N}@spendwise.local";
        var (userId, _, _) = SetupUserClient(factory, client, initialEmail);

        var newEmail = $"target_{Guid.NewGuid():N}@spendwise.local";

        await client.PostAsync("/api/users/me/email/current/request", null);
        var verifyCurr = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        var ticket = (await verifyCurr.Content.ReadFromJsonAsync<VerifyCurrentEmailOtpResponse>())!.Ticket;

        await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticket, newEmail));

        // Thử dùng OTP không đúng hash của purpose mới
        var confirmRes = await client.PostAsJsonAsync("/api/users/me/email/new/confirm", new ConfirmNewEmailOtpRequest(ticket, "99999999"));
        Assert.Equal(HttpStatusCode.BadRequest, confirmRes.StatusCode);
    }

    [Fact]
    public async Task Test09_DoiEmailDichGiuaLuong_OTPTruocKhongConQuyenXacNhan()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var initialEmail = $"user_{Guid.NewGuid():N}@spendwise.local";
        var (userId, _, _) = SetupUserClient(factory, client, initialEmail);

        var email1 = $"email1_{Guid.NewGuid():N}@spendwise.local";
        var email2 = $"email2_{Guid.NewGuid():N}@spendwise.local";

        await client.PostAsync("/api/users/me/email/current/request", null);
        var verifyCurr = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        var ticket = (await verifyCurr.Content.ReadFromJsonAsync<VerifyCurrentEmailOtpResponse>())!.Ticket;

        // Gửi đến email1
        await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticket, email1));

        // Đổi ý, gửi đến email2 (làm token của email1 bị thu hồi)
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var token1 = await db.EmailVerificationTokens
                .Where(t => t.UserId == userId && t.Purpose == EmailVerificationPurpose.ChangeEmailNew)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
            token1.CreatedAt = DateTime.UtcNow.AddMinutes(-3); // bypass cooldown
            await db.SaveChangesAsync();
        }

        await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticket, email2));

        // Kiểm tra trong DB: token1 đã bị thu hồi
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var tokens = await db.EmailVerificationTokens
                .Where(t => t.UserId == userId && t.Purpose == EmailVerificationPurpose.ChangeEmailNew)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();
            Assert.Equal(2, tokens.Count);
            Assert.NotNull(tokens[0].RevokedAt); // token cũ bị revoked!
            Assert.Null(tokens[1].RevokedAt);   // token mới còn hiệu lực!
        }
    }

    [Fact]
    public async Task Test10_EmailMoiDaTonTai_ConflictKhongCapNhat()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var otherUserEmail = $"other_{Guid.NewGuid():N}@spendwise.local";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            db.Users.Add(CreateUser("Other User", otherUserEmail));
            await db.SaveChangesAsync();
        }

        var myEmail = $"me_{Guid.NewGuid():N}@spendwise.local";
        SetupUserClient(factory, client, myEmail);

        await client.PostAsync("/api/users/me/email/current/request", null);
        var verifyCurr = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        var ticket = (await verifyCurr.Content.ReadFromJsonAsync<VerifyCurrentEmailOtpResponse>())!.Ticket;

        var dupRes = await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticket, otherUserEmail));
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);
    }

    [Fact]
    public async Task Test11_HaiRequestDongThoi_KhongBypassHoacCommitHaiLan()
    {
        using var factory = new SpendWiseApiFactory();
        using var client1 = factory.CreateHttpsClient();
        using var client2 = factory.CreateHttpsClient();

        var initialEmail = $"concurrent_{Guid.NewGuid():N}@spendwise.local";
        var (userId, sessionId, token) = SetupUserClient(factory, client1, initialEmail);
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newEmail = $"target_{Guid.NewGuid():N}@spendwise.local";

        await client1.PostAsync("/api/users/me/email/current/request", null);
        var verifyCurr = await client1.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        var ticket = (await verifyCurr.Content.ReadFromJsonAsync<VerifyCurrentEmailOtpResponse>())!.Ticket;

        await client1.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticket, newEmail));

        // Gọi đồng thời confirm từ 2 client
        var task1 = client1.PostAsJsonAsync("/api/users/me/email/new/confirm", new ConfirmNewEmailOtpRequest(ticket, "12345678"));
        var task2 = client2.PostAsJsonAsync("/api/users/me/email/new/confirm", new ConfirmNewEmailOtpRequest(ticket, "12345678"));

        var responses = await Task.WhenAll(task1, task2);
        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        // Đúng một request thành công (200), request còn lại bị từ chối (không bao giờ commit 2 lần)
        Assert.Single(statusCodes, c => c == HttpStatusCode.OK);
        Assert.Contains(statusCodes, c => c == HttpStatusCode.BadRequest || c == HttpStatusCode.InternalServerError || c == HttpStatusCode.Conflict);

        // Kiểm tra trong DB: email đã được commit đúng và PendingNewEmail đã xóa
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var u = await db.Users.FindAsync(userId);
            Assert.NotNull(u);
            Assert.Equal(newEmail, u.Email);
            Assert.Null(u.PendingNewEmail);
        }
    }

    [Fact]
    public async Task Test12_PhienBiThuHoi_KhongTheTiepTucDoiEmail()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var initialEmail = $"user_{Guid.NewGuid():N}@spendwise.local";
        var (userId, sessionId, _) = SetupUserClient(factory, client, initialEmail);

        var newEmail = $"target_{Guid.NewGuid():N}@spendwise.local";

        await client.PostAsync("/api/users/me/email/current/request", null);
        var verifyCurr = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        var ticket = (await verifyCurr.Content.ReadFromJsonAsync<VerifyCurrentEmailOtpResponse>())!.Ticket;

        await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticket, newEmail));

        // Thu hồi phiên hiện tại trong DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var session = await db.UserSessions.FindAsync(sessionId);
            session!.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // Cố gắng confirm khi phiên đã bị thu hồi
        var confirmRes = await client.PostAsJsonAsync("/api/users/me/email/new/confirm", new ConfirmNewEmailOtpRequest(ticket, "12345678"));
        Assert.True(confirmRes.StatusCode == HttpStatusCode.Unauthorized || confirmRes.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Test13_HuyYeuCau_KhongTheTiepTucXacNhan()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var initialEmail = $"user_{Guid.NewGuid():N}@spendwise.local";
        var (userId, _, _) = SetupUserClient(factory, client, initialEmail);

        var newEmail = $"target_{Guid.NewGuid():N}@spendwise.local";

        await client.PostAsync("/api/users/me/email/current/request", null);
        var verifyCurr = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        var ticket = (await verifyCurr.Content.ReadFromJsonAsync<VerifyCurrentEmailOtpResponse>())!.Ticket;

        await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticket, newEmail));

        // Hủy yêu cầu
        var cancelRes = await client.PostAsJsonAsync("/api/users/me/email/cancel", new CancelEmailChangeRequest(ticket));
        Assert.Equal(HttpStatusCode.NoContent, cancelRes.StatusCode);

        // Sau khi hủy, confirm bị từ chối
        var confirmRes = await client.PostAsJsonAsync("/api/users/me/email/new/confirm", new ConfirmNewEmailOtpRequest(ticket, "12345678"));
        Assert.Equal(HttpStatusCode.BadRequest, confirmRes.StatusCode);

        // PendingNewEmail bị xóa
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var userInDb = await db.Users.FindAsync(userId);
        Assert.Null(userInDb!.PendingNewEmail);
    }

    [Fact]
    public async Task Test14_GuiLaiOTP_MaCuVoHieu_CooldownDung()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var initialEmail = $"user_{Guid.NewGuid():N}@spendwise.local";
        var (userId, _, _) = SetupUserClient(factory, client, initialEmail);

        // Lần 1
        var req1 = await client.PostAsync("/api/users/me/email/current/request", null);
        Assert.Equal(HttpStatusCode.OK, req1.StatusCode);

        // Ngay lập tức gọi lại lần 2 -> Bị chặn cooldown 429
        var req2 = await client.PostAsync("/api/users/me/email/current/request", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, req2.StatusCode);

        // Giả lập trôi qua cooldown trong DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var token = await db.EmailVerificationTokens
                .Where(t => t.UserId == userId && t.Purpose == EmailVerificationPurpose.ChangeEmailCurrent)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
            token.CreatedAt = DateTime.UtcNow.AddMinutes(-3);
            await db.SaveChangesAsync();
        }

        // Sau cooldown, gọi lại thành công
        var req3 = await client.PostAsync("/api/users/me/email/current/request", null);
        Assert.Equal(HttpStatusCode.OK, req3.StatusCode);

        // Mã cũ bị thu hồi
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var tokens = await db.EmailVerificationTokens
                .Where(t => t.UserId == userId && t.Purpose == EmailVerificationPurpose.ChangeEmailCurrent)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();
            Assert.Equal(2, tokens.Count);
            Assert.NotNull(tokens[0].RevokedAt); // Mã đầu tiên đã revoked
            Assert.Null(tokens[1].RevokedAt);    // Mã mới còn hiệu lực
        }
    }

    [Fact]
    public async Task Test15_GoiTrucTiepAPICommit_KhongBypassBuocEmailGoc()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var initialEmail = $"user_{Guid.NewGuid():N}@spendwise.local";
        SetupUserClient(factory, client, initialEmail);

        // Thử bypass gọi trực tiếp new/confirm mà chưa làm bước A
        var confirmRes = await client.PostAsJsonAsync("/api/users/me/email/new/confirm", new ConfirmNewEmailOtpRequest("fake-ticket", "12345678"));
        Assert.Equal(HttpStatusCode.BadRequest, confirmRes.StatusCode);

        // Thử gọi endpoint legacy cũ
        var legacyReq = await client.PostAsJsonAsync("/api/users/me/email/request", new { newEmail = "hacker@evil.com" });
        Assert.Equal(HttpStatusCode.BadRequest, legacyReq.StatusCode);

        var legacyConfirm = await client.PostAsJsonAsync("/api/users/me/email/confirm", new { otp = "12345678" });
        Assert.Equal(HttpStatusCode.BadRequest, legacyConfirm.StatusCode);
    }

    [Fact]
    public async Task Test16_ThayDoiEmailThanhCong_ThongBaoToiEmailGoc_TrangThaiTaiKhoanNhatQuan()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var initialEmail = $"initial_{Guid.NewGuid():N}@spendwise.local";
        var (userId, sessionId, _) = SetupUserClient(factory, client, initialEmail);

        var newEmail = $"final_{Guid.NewGuid():N}@spendwise.local";

        await client.PostAsync("/api/users/me/email/current/request", null);
        var verifyCurr = await client.PostAsJsonAsync("/api/users/me/email/current/verify", new VerifyCurrentEmailOtpRequest("12345678"));
        var ticket = (await verifyCurr.Content.ReadFromJsonAsync<VerifyCurrentEmailOtpResponse>())!.Ticket;

        await client.PostAsJsonAsync("/api/users/me/email/new/request", new RequestNewEmailOtpRequest(ticket, newEmail));
        var confirmRes = await client.PostAsJsonAsync("/api/users/me/email/new/confirm", new ConfirmNewEmailOtpRequest(ticket, "12345678"));
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var userInDb = await db.Users.FindAsync(userId);
        Assert.NotNull(userInDb);
        Assert.Equal(newEmail, userInDb.Email);
        Assert.Equal(newEmail.ToUpperInvariant(), userInDb.NormalizedEmail);
        Assert.Null(userInDb.PendingNewEmail);
        Assert.NotNull(userInDb.EmailVerifiedAt);

        // Thông báo bảo mật đã được gửi về email gốc
        var outbox = await db.EmailOutboxMessages
            .Where(m => m.RecipientEmail == initialEmail && m.Subject.Contains("Cảnh báo bảo mật"))
            .ToListAsync();
        Assert.NotEmpty(outbox);
    }
}
