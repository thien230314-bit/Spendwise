using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.Register;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo đảm đăng ký tạo OTP email nhưng chưa cấp session/PIN challenge.
public sealed class RegistrationAtomicityTests
{
    [Fact]
    public async Task Registration_creates_complete_zero_balance_account_state()
    {
        // Arrange: handler dùng SQLite relational và token/password service kiểm soát được.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = Handler(fixture);

        // Act: đăng ký một tài khoản hợp lệ.
        await handler.Handle(Command("complete"), default);

        // Assert: User, Wallet, LedgerAccount, OTP hash và email outbox cùng tồn tại.
        fixture.Db.ChangeTracker.Clear();
        var user = await fixture.Db.Users.SingleAsync();
        var wallet = await fixture.Db.Wallets
            .Include(item => item.LedgerAccount)
            .SingleAsync();
        var verification = await fixture.Db.EmailVerificationTokens.SingleAsync();
        Assert.Equal(user.Id, wallet.OwnerUserId);
        Assert.Equal(0, wallet.AvailableBalance);
        Assert.Equal(0, wallet.LedgerAccount.Balance);
        Assert.Equal(user.Id, verification.UserId);
        Assert.Equal(EmailVerificationPurpose.Registration, verification.Purpose);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Single(await fixture.Db.EmailOutboxMessages.ToListAsync());
        Assert.Empty(await fixture.Db.LoginChallenges.ToListAsync());
        Assert.Empty(await fixture.Db.UserSessions.ToListAsync());
        Assert.Empty(await fixture.Db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Wallet_insert_failure_leaves_no_partial_user()
    {
        // Arrange: trigger mô phỏng lỗi khi tạo ví chính.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        await AddFailureTriggerAsync(fixture, "Wallets");

        // Act + Assert: toàn bộ registration transaction phải rollback.
        await Assert.ThrowsAsync<DbUpdateException>(() => Handler(fixture).Handle(
            Command("wallet-failure"),
            default));
        await AssertNoRegistrationStateAsync(fixture);
    }

    [Fact]
    public async Task Ledger_account_insert_failure_leaves_no_partial_user()
    {
        // Arrange: LedgerAccount là phần bắt buộc của ví ban đầu.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        await AddFailureTriggerAsync(fixture, "LedgerAccounts");

        // Act + Assert: lỗi ledger không được để User/Wallet tồn tại riêng lẻ.
        await Assert.ThrowsAsync<DbUpdateException>(() => Handler(fixture).Handle(
            Command("ledger-failure"),
            default));
        await AssertNoRegistrationStateAsync(fixture);
    }

    [Fact]
    public async Task Email_outbox_insert_failure_leaves_no_partial_user()
    {
        // Arrange: E01 outbox cũng thuộc transaction đăng ký.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        await AddFailureTriggerAsync(fixture, "EmailOutboxMessages");

        // Act + Assert: lỗi challenge rollback cả User, Wallet và LedgerAccount.
        await Assert.ThrowsAsync<DbUpdateException>(() => Handler(fixture).Handle(
            Command("challenge-failure"),
            default));
        await AssertNoRegistrationStateAsync(fixture);
    }

    private static RegisterCommandHandler Handler(
        TargetedCorrectnessFixture fixture)
    {
        var passwords = new RegistrationPasswordService();
        return new RegisterCommandHandler(
            fixture.Db,
            passwords,
            EmailVerificationTestFactory.Create(fixture.Db, fixture.Clock));
    }

    private static RegisterCommand Command(string suffix) =>
        new(
            new RegisterRequest(
                "Registration Test",
                $"{suffix}@test.local",
                IdentityTestData.NewPhone(),
                "StrongPass1",
                "StrongPass1"),
            "127.0.0.1",
            "Tests");

    private static Task AddFailureTriggerAsync(
        TargetedCorrectnessFixture fixture,
        string table)
    {
        // SQL chỉ chọn từ ba literal nội bộ; không ghép identifier từ client input.
        var sql = table switch
        {
            "Wallets" =>
                """
                CREATE TRIGGER fail_registration_wallet
                BEFORE INSERT ON Wallets
                BEGIN
                    SELECT RAISE(ABORT, 'forced registration failure');
                END;
                """,
            "LedgerAccounts" =>
                """
                CREATE TRIGGER fail_registration_ledger
                BEFORE INSERT ON LedgerAccounts
                BEGIN
                    SELECT RAISE(ABORT, 'forced registration failure');
                END;
                """,
            "EmailOutboxMessages" =>
                """
                CREATE TRIGGER fail_registration_email_outbox
                BEFORE INSERT ON EmailOutboxMessages
                BEGIN
                    SELECT RAISE(ABORT, 'forced email outbox failure');
                END;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        return fixture.Db.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task AssertNoRegistrationStateAsync(
        TargetedCorrectnessFixture fixture)
    {
        // ChangeTracker còn entity Added sau exception nên phải đọc lại database sạch.
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.Users.ToListAsync());
        Assert.Empty(await fixture.Db.Wallets.ToListAsync());
        Assert.Empty(await fixture.Db.LedgerAccounts.ToListAsync());
        Assert.Empty(await fixture.Db.UserSessions.ToListAsync());
        Assert.Empty(await fixture.Db.RefreshTokens.ToListAsync());
        Assert.Empty(await fixture.Db.LoginChallenges.ToListAsync());
        Assert.Empty(await fixture.Db.EmailVerificationTokens.ToListAsync());
        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
    }

    private sealed class RegistrationPasswordService : IPasswordService
    {
        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string password, string encodedHash) =>
            encodedHash == Hash(password);
    }
}
