using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SpendWise.Application.AccountRecovery;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.AccountRecovery.Commands.ReviewAccountRecovery;
using SpendWise.Application.Features.AccountRecovery.Commands.SubmitAccountRecovery;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Users.Commands.ResetUserPassword;
using SpendWise.Application.Features.Admin.Users.Commands.ResetUserSecurityPin;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.RequestSecurityPinReset;
using SpendWise.Application.Features.Authentication.Commands.ResetSecurityPin;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ token recovery, Admin PIN và workflow khôi phục tài khoản.
public sealed class IdentityRecoveryWorkflowTests
{
    [Fact]
    public async Task Forgot_pin_keeps_old_pin_until_single_use_reset_then_revokes_sessions()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var pins = new SecurityPinService();
        var user = await fixture.AddUserAsync("PIN recovery");
        user.EmailVerifiedAt = fixture.Clock.UtcNow;
        user.Phone = "0912345678";
        user.SecurityPinHash = pins.Hash("123456");
        user.SecurityPinFailedAttempts = 5;
        user.SecurityPinLockoutUntil = fixture.Clock.UtcNow.AddMinutes(5);
        await fixture.Db.SaveChangesAsync();
        var session = await fixture.AddSessionAsync(user.Id);
        var email = new RecordingEmailSender();
        var support = new SecurityPinResetSupport(fixture.Db, new LoginChallengeTokenService(), fixture.Clock);
        var requestHandler = new RequestSecurityPinResetCommandHandler(
            fixture.Db,
            support,
            email,
            NullLogger<RequestSecurityPinResetCommandHandler>.Instance);

        // Request theo phone vẫn gửi raw token tới email đăng ký và chỉ lưu hash.
        await requestHandler.Handle(
            new RequestSecurityPinResetCommand(new ForgotSecurityPinRequest(user.Phone)),
            default);
        var rawToken = Assert.Single(email.PinMessages).RawToken;
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.SecurityPinResetTokens.SingleAsync();
        Assert.NotEqual(rawToken, stored.TokenHash);
        Assert.True(pins.Verify("123456", (await fixture.Db.Users.FindAsync(user.Id))!.SecurityPinHash!));

        var resetHandler = new ResetSecurityPinCommandHandler(
            fixture.Db,
            support,
            pins,
            new AuthenticationSupport(fixture.Db, new TargetedTokenService(), fixture.Clock));
        await resetHandler.Handle(
            new ResetSecurityPinCommand(new ResetSecurityPinRequest(rawToken, "654321", "654321")),
            default);
        fixture.Db.ChangeTracker.Clear();
        var resetUser = (await fixture.Db.Users.FindAsync(user.Id))!;
        Assert.True(pins.Verify("654321", resetUser.SecurityPinHash!));
        Assert.Equal(0, resetUser.SecurityPinFailedAttempts);
        Assert.Null(resetUser.SecurityPinLockoutUntil);
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        await Assert.ThrowsAsync<AppException>(() => resetHandler.Handle(
            new ResetSecurityPinCommand(new ResetSecurityPinRequest(rawToken, "222222", "222222")),
            default));
    }

    [Fact]
    public async Task Expired_pin_reset_token_is_rejected()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Expired PIN reset");
        var support = new SecurityPinResetSupport(fixture.Db, new LoginChallengeTokenService(), fixture.Clock);
        var issued = await support.CreateAsync(user, SecurityPinResetPurpose.SelfService, null, default);
        issued.Token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<AppException>(() => new ResetSecurityPinCommandHandler(
            fixture.Db,
            support,
            new SecurityPinService(),
            new AuthenticationSupport(fixture.Db, new TargetedTokenService(), fixture.Clock))
            .Handle(new ResetSecurityPinCommand(new ResetSecurityPinRequest(issued.RawToken, "654321", "654321")), default));
    }

    [Fact]
    public async Task Admin_password_reset_requires_admin_pin_hashes_random_password_and_revokes_sessions()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var pins = new SecurityPinService();
        var passwords = new PasswordService();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        admin.SecurityPinHash = pins.Hash("123456");
        var target = await fixture.AddUserAsync("Reset target");
        target.PasswordHash = passwords.Hash("PreviousPass1");
        target.PasswordFailedAttempts = 5;
        target.PasswordLockoutUntil = fixture.Clock.UtcNow.AddMinutes(5);
        await fixture.Db.SaveChangesAsync();
        var session = await fixture.AddSessionAsync(target.Id);
        var handler = new ResetUserPasswordCommandHandler(
            fixture.Db,
            new AdminSecurityGuard(fixture.Db, pins,
                new AccountCredentialLockout(fixture.Db, fixture.Clock)),
            new AdminAuditWriter(fixture.Db),
            new TemporaryPasswordService(),
            passwords,
            new AuthenticationSupport(fixture.Db, new TargetedTokenService(), fixture.Clock));

        var nonAdmin = await fixture.AddUserAsync("Non-admin operator");
        nonAdmin.SecurityPinHash = pins.Hash("123456");
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new ResetUserPasswordCommand(
                nonAdmin.Id,
                target.Id,
                new AdminCredentialResetRequest("Unauthorized support attempt", "123456")),
            default));
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new ResetUserPasswordCommand(admin.Id, target.Id, new AdminCredentialResetRequest("Support verified identity", "654321")),
            default));
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new ResetUserPasswordCommand(admin.Id, target.Id, new AdminCredentialResetRequest("", "123456")),
            default));
        var result = await handler.Handle(
            new ResetUserPasswordCommand(admin.Id, target.Id, new AdminCredentialResetRequest("Support verified identity", "123456")),
            default);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.Users.FindAsync(target.Id);
        Assert.True(passwords.Verify(result.TemporaryPassword, saved!.PasswordHash));
        Assert.NotEqual("Spendwise123", result.TemporaryPassword);
        Assert.DoesNotContain(result.TemporaryPassword, (await fixture.Db.AuditLogs.SingleAsync()).MetadataJson ?? string.Empty);
        Assert.True(saved.MustChangePassword);
        Assert.Equal(0, saved.PasswordFailedAttempts);
        Assert.Null(saved.PasswordLockoutUntil);
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Admin_pin_reset_invalidates_hash_and_issues_hashed_enrollment_token()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var pins = new SecurityPinService();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        admin.SecurityPinHash = pins.Hash("123456");
        var target = await fixture.AddUserAsync("PIN reset target");
        target.SecurityPinHash = pins.Hash("222222");
        target.SecurityPinFailedAttempts = 5;
        target.SecurityPinLockoutUntil = fixture.Clock.UtcNow.AddMinutes(5);
        await fixture.Db.SaveChangesAsync();
        var session = await fixture.AddSessionAsync(target.Id);
        var email = new RecordingEmailSender();
        var handler = new ResetUserSecurityPinCommandHandler(
            fixture.Db,
            new AdminSecurityGuard(fixture.Db, pins,
                new AccountCredentialLockout(fixture.Db, fixture.Clock)),
            new AdminAuditWriter(fixture.Db),
            new SecurityPinResetSupport(fixture.Db, new LoginChallengeTokenService(), fixture.Clock),
            new AuthenticationSupport(fixture.Db, new TargetedTokenService(), fixture.Clock),
            email,
            NullLogger<ResetUserSecurityPinCommandHandler>.Instance);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new ResetUserSecurityPinCommand(
                admin.Id,
                target.Id,
                new AdminCredentialResetRequest("Invalid administrator PIN", "654321")),
            default));
        await handler.Handle(
            new ResetUserSecurityPinCommand(admin.Id, target.Id, new AdminCredentialResetRequest("Verified PIN recovery request", "123456")),
            default);
        fixture.Db.ChangeTracker.Clear();
        var resetTarget = (await fixture.Db.Users.FindAsync(target.Id))!;
        Assert.Null(resetTarget.SecurityPinHash);
        Assert.Equal(0, resetTarget.SecurityPinFailedAttempts);
        Assert.Null(resetTarget.SecurityPinLockoutUntil);
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        var raw = Assert.Single(email.PinMessages).RawToken;
        Assert.NotEqual(raw, (await fixture.Db.SecurityPinResetTokens.SingleAsync()).TokenHash);

        // PIN bị xóa nên lần xác thực kế tiếp chỉ tạo challenge thiết lập PIN.
        var challenge = await new LoginChallengeSupport(
            fixture.Db,
            new LoginChallengeTokenService(),
            fixture.Clock).CreateAsync(
                (await fixture.Db.Users.FindAsync(target.Id))!,
                "Test browser",
                null,
                null,
                default);
        Assert.Equal("PIN_SETUP_REQUIRED", challenge.Status);

        var resetHandler = new ResetSecurityPinCommandHandler(
            fixture.Db,
            new SecurityPinResetSupport(fixture.Db, new LoginChallengeTokenService(), fixture.Clock),
            pins,
            new AuthenticationSupport(fixture.Db, new TargetedTokenService(), fixture.Clock));
        await resetHandler.Handle(
            new ResetSecurityPinCommand(new ResetSecurityPinRequest(raw, "333333", "333333")),
            default);
        await Assert.ThrowsAsync<AppException>(() => resetHandler.Handle(
            new ResetSecurityPinCommand(new ResetSecurityPinRequest(raw, "444444", "444444")),
            default));

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new ResetUserSecurityPinCommand(
                admin.Id,
                admin.Id,
                new AdminCredentialResetRequest("Self reset is prohibited", "123456")),
            default));
    }

    [Fact]
    public async Task Account_recovery_preserves_history_and_terminal_state_and_returns_temp_password_once()
    {
        var reviewedAt = new DateTime(2026, 9, 19, 4, 30, 0, DateTimeKind.Utc);
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(new FixedClock(reviewedAt));
        var pins = new SecurityPinService();
        var passwords = new PasswordService();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        admin.SecurityPinHash = pins.Hash("123456");
        var target = await fixture.AddUserAsync("Recovery target");
        target.Phone = "0987654321";
        target.PasswordHash = passwords.Hash("PreviousPass1");
        target.PasswordFailedAttempts = 5;
        target.PasswordLockoutUntil = fixture.Clock.UtcNow.AddMinutes(5);
        await fixture.Db.SaveChangesAsync();
        var session = await fixture.AddSessionAsync(target.Id);
        var submission = await new SubmitAccountRecoveryCommandHandler(fixture.Db).Handle(
            new SubmitAccountRecoveryCommand(new SubmitAccountRecoveryRequest(
                target.FullName, target.Phone, "Lost access to security credentials", target.Email)),
            default);
        Assert.DoesNotContain(target.Email, submission.Message, StringComparison.OrdinalIgnoreCase);
        var recovery = await fixture.Db.AccountRecoveryRequests.SingleAsync();
        // Mỗi request HTTP thật dùng DbContext scope mới; clear mô phỏng đúng ranh giới đó.
        fixture.Db.ChangeTracker.Clear();
        var security = new AdminSecurityGuard(fixture.Db, pins,
            new AccountCredentialLockout(fixture.Db, fixture.Clock));
        var audit = new AdminAuditWriter(fixture.Db);
        var notifications = new NotificationService(fixture.Db);
        await Assert.ThrowsAsync<ForbiddenException>(() => new RequestMoreAccountRecoveryInfoCommandHandler(
            fixture.Db,
            security,
            audit,
            notifications,
            fixture.Clock).Handle(
                new RequestMoreAccountRecoveryInfoCommand(
                    admin.Id,
                    recovery.Id,
                    new AccountRecoveryAdminActionRequest("654321", "Invalid administrator PIN")),
                default));
        await new RequestMoreAccountRecoveryInfoCommandHandler(
            fixture.Db, security, audit, notifications, fixture.Clock).Handle(
            new RequestMoreAccountRecoveryInfoCommand(admin.Id, recovery.Id, new AccountRecoveryAdminActionRequest("123456", "Please provide identity document")),
            default);
        fixture.Db.ChangeTracker.Clear();
        var approved = await new ApproveAccountRecoveryCommandHandler(
            fixture.Db,
            security,
            audit,
            new TemporaryPasswordService(),
            passwords,
            new AuthenticationSupport(fixture.Db, new TargetedTokenService(), fixture.Clock),
            notifications,
            fixture.Clock).Handle(
                new ApproveAccountRecoveryCommand(admin.Id, recovery.Id, new ApproveAccountRecoveryRequest("123456", "Identity evidence verified", target.Id)),
                default);

        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.Users.FindAsync(target.Id);
        Assert.True(passwords.Verify(approved.TemporaryPassword, saved!.PasswordHash));
        Assert.True(saved.MustChangePassword);
        Assert.Equal(0, saved.PasswordFailedAttempts);
        Assert.Null(saved.PasswordLockoutUntil);
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        var finalRecovery = await fixture.Db.AccountRecoveryRequests.Include(item => item.Reviews).SingleAsync();
        Assert.Equal(AccountRecoveryStatus.Approved, finalRecovery.Status);
        Assert.Equal(reviewedAt, finalRecovery.ReviewedAt);
        Assert.Equal(2, finalRecovery.Reviews.Count);
        Assert.NotEmpty(await fixture.Db.Notifications.ToListAsync());
        Assert.NotEmpty(await fixture.Db.OutboxMessages.ToListAsync());
        await Assert.ThrowsAsync<ConflictException>(() => new RejectAccountRecoveryCommandHandler(
            fixture.Db,
            security,
            audit,
            notifications,
            fixture.Clock).Handle(
                new RejectAccountRecoveryCommand(admin.Id, recovery.Id, new AccountRecoveryAdminActionRequest("123456", "Cannot change terminal request")),
                default));
    }

    [Fact]
    public async Task Account_recovery_rejects_and_cannot_leave_terminal_state()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var pins = new SecurityPinService();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        admin.SecurityPinHash = pins.Hash("123456");
        var recovery = new AccountRecoveryRequest
        {
            FullName = "Unknown applicant",
            Phone = "0900000000",
            Description = "Cannot prove account ownership"
        };
        fixture.Db.AccountRecoveryRequests.Add(recovery);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var security = new AdminSecurityGuard(fixture.Db, pins,
            new AccountCredentialLockout(fixture.Db, fixture.Clock));
        var audit = new AdminAuditWriter(fixture.Db);
        var notifications = new NotificationService(fixture.Db);

        var rejected = await new RejectAccountRecoveryCommandHandler(
            fixture.Db,
            security,
            audit,
            notifications,
            fixture.Clock).Handle(
                new RejectAccountRecoveryCommand(
                    admin.Id,
                    recovery.Id,
                    new AccountRecoveryAdminActionRequest("123456", "Identity evidence did not match")),
                default);
        Assert.Equal(nameof(AccountRecoveryStatus.Rejected), rejected.Status);
        fixture.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ConflictException>(() => new RequestMoreAccountRecoveryInfoCommandHandler(
            fixture.Db,
            security,
            audit,
            notifications,
            fixture.Clock).Handle(
                new RequestMoreAccountRecoveryInfoCommand(
                    admin.Id,
                    recovery.Id,
                    new AccountRecoveryAdminActionRequest("123456", "No transition after rejection")),
                default));
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string Email, string RawToken, DateTime ExpiresAt)> PinMessages { get; } = [];
        public Task SendSecurityPinResetAsync(string email, string rawToken, DateTime expiresAt, CancellationToken cancellationToken)
        {
            PinMessages.Add((email, rawToken, expiresAt));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
