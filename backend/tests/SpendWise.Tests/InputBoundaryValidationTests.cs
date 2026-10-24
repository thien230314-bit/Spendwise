using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.Register;
using SpendWise.Application.Features.Goals.Commands.CreateGoal;
using SpendWise.Application.Features.Goals.Commands;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Transactions.Queries.GetTransactions;
using SpendWise.Application.Features.Users.Commands.UpdatePreferences;
using SpendWise.Application.Features.Wallets.Commands.CreateWallet;
using SpendWise.Application.Finance;
using SpendWise.Application.Identity;
using SpendWise.Application.Planning;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

// Các test đại diện bảo đảm input sai trả 4xx có chủ ý trước khi EF ghi SQL.
public sealed class InputBoundaryValidationTests
{
    [Fact]
    public async Task Registration_RejectsInvalidEmail()
    {
        // Arrange: các trường khác hợp lệ để chỉ kiểm tra email.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = RegistrationHandler(fixture);

        // Act: email không có cấu trúc hộp thư.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            Registration(new RegisterRequest(
                "Boundary User",
                "not-an-email",
                IdentityTestData.NewPhone(),
                "StrongPass1",
                "StrongPass1")),
            default));

        // Assert: handler trả mã lỗi rõ ràng và không tạo User.
        Assert.Equal("invalid_email", error.Code);
        Assert.Empty(fixture.Db.Users);
    }

    [Fact]
    public async Task Registration_RejectsInvalidPhoneAndPasswordMismatch()
    {
        // Arrange: tạo handler đăng ký trên SQLite thật.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = RegistrationHandler(fixture);

        // Act + Assert: phone không bắt đầu bằng 0 bị từ chối.
        var phoneError = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            Registration(new RegisterRequest(
                "Boundary User",
                "boundary@test.local",
                "1234567890",
                "StrongPass1",
                "StrongPass1")),
            default));
        Assert.Equal("invalid_phone", phoneError.Code);

        // Act + Assert: password xác nhận khác password bị từ chối trước khi hash.
        var passwordError = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            Registration(new RegisterRequest(
                "Boundary User",
                "boundary@test.local",
                IdentityTestData.NewPhone(),
                "StrongPass1",
                "DifferentPass1")),
            default));
        Assert.Equal("password_mismatch", passwordError.Code);
    }

    [Fact]
    public async Task Wallet_RejectsOversizedName()
    {
        // Arrange: tên dài hơn cột Wallet.Name một ký tự.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = new CreateWalletCommandHandler(fixture.Db, new SpendWise.Application.Features.Banking.ActiveBankGuard(fixture.Db));

        // Act + Assert: lỗi validation xuất hiện thay vì DbUpdateException.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CreateWalletCommand(
                Guid.NewGuid(),
                new CreateWalletRequest(new string('W', 101), "Main")),
            default));
        Assert.Equal("invalid_wallet_name", error.Code);
    }

    [Fact]
    public async Task Preferences_NormalizesSupportedThemeAndRejectsUnknownTheme()
    {
        // Arrange: người dùng hiện tại và handler preferences.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Theme user");
        var handler = new UpdatePreferencesCommandHandler(fixture.Db);

        // Act: contract hiện tại không phân biệt hoa/thường với theme hợp lệ.
        var updated = await handler.Handle(
            new UpdatePreferencesCommand(
                user.Id,
                new UpdatePreferencesRequest("vi-VN", "DARK", "VND")),
            default);
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new UpdatePreferencesCommand(
                user.Id,
                new UpdatePreferencesRequest("vi-VN", "neon", "VND")),
            default));

        // Assert: giá trị hợp lệ được chuẩn hóa, giá trị lạ bị từ chối.
        Assert.Equal("dark", updated.PreferredTheme);
        Assert.Equal("invalid_theme", error.Code);
    }

    [Fact]
    public async Task TransactionQuery_RejectsInvalidPageAndPageSize()
    {
        // Arrange: query không cần dữ liệu vì pagination được kiểm tra trước LINQ SQL.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = new GetTransactionsQueryHandler(fixture.Db);

        // Act + Assert: không tự ép page sai thành giá trị khác mà trả lỗi 4xx rõ ràng.
        var pageError = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new GetTransactionsQuery(Guid.NewGuid(), new TransactionQuery(Page: 0)),
            default));
        var sizeError = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new GetTransactionsQuery(Guid.NewGuid(), new TransactionQuery(PageSize: 101)),
            default));
        Assert.Equal("invalid_pagination", pageError.Code);
        Assert.Equal("invalid_pagination", sizeError.Code);
    }

    [Fact]
    public void Money_RejectsExcessPrecisionAndStorageOverflow()
    {
        // Act + Assert: decimal có hơn hai số lẻ không khớp decimal(18,2).
        var precisionError = Assert.Throws<AppException>(() => InputBoundary.PositiveMoney(1.001m));
        // Act + Assert: số vượt 16 chữ số nguyên không được đẩy xuống SQL Server.
        var rangeError = Assert.Throws<AppException>(() =>
            InputBoundary.PositiveMoney(InputBoundary.MaximumMoney + 0.01m));
        Assert.Equal("invalid_amount", precisionError.Code);
        Assert.Equal("invalid_amount", rangeError.Code);
    }

    [Fact]
    public async Task Goal_RejectsPastDeadline()
    {
        // Arrange: Goal có tên và số tiền hợp lệ nhưng deadline đã qua.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Past goal owner");
        var movement = new GoalMovementExecutor(fixture.Db,
            new LedgerService(fixture.Db, fixture.Clock), fixture.Clock, fixture.Security,
            new SpendWise.Application.Features.Banking.ActiveBankGuard(fixture.Db));
        var handler = new CreateGoalCommandHandler(fixture.Db, fixture.Security, movement, fixture.Clock);

        // Act + Assert: date sai bị chặn trước khi tạo LedgerAccount cho Goal.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CreateGoalCommand(
                user.Id,
                new CreateGoalRequest("Past goal", 100m, DateTime.UtcNow.AddMinutes(-1),
                    SecurityPin: TargetedCorrectnessFixture.SecurityPin)),
            default));
        Assert.Equal("invalid_date", error.Code);
        Assert.Empty(fixture.Db.Goals);
        Assert.Empty(fixture.Db.LedgerAccounts);
    }

    private static RegisterCommandHandler RegistrationHandler(TargetedCorrectnessFixture fixture)
    {
        var challenges = new LoginChallengeSupport(
            fixture.Db,
            new LoginChallengeTokenService(),
            fixture.Clock);
        return new RegisterCommandHandler(
            fixture.Db,
            new PasswordService(),
            EmailVerificationTestFactory.Create(fixture.Db, fixture.Clock));
    }

    private static RegisterCommand Registration(RegisterRequest request) =>
        new(request, "127.0.0.1", "Boundary tests");
}
