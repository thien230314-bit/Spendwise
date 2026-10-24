using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Wallets.Commands.FreezeWallet;
using SpendWise.Application.Features.Wallets.Commands.CloseWallet;
using SpendWise.Application.Features.Wallets.Commands.CreateWallet;
using SpendWise.Application.Features.Wallets.Commands.UpdateWallet;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;
using SpendWise.Application.Features.Banking;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ invariant: có ví Active thì phải có đúng một ví default.
public sealed class WalletDefaultInvariantTests
{
    [Fact]
    public async Task Database_rejects_two_default_wallets_for_one_user()
    {
        // Arrange: tạo một người dùng và hai ví cùng yêu cầu làm default.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Unique default owner");
        var first = WalletFactory.Create(user.Id, "First", WalletType.Main, true);
        var second = WalletFactory.Create(user.Id, "Second", WalletType.Savings, true);
        fixture.Db.Wallets.AddRange(first, second);

        // Act và Assert: filtered unique index chặn trạng thái có hai default.
        await Assert.ThrowsAsync<DbUpdateException>(
            () => fixture.Db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("Savings")]
    [InlineData("Emergency")]
    public async Task Main_wallet_cannot_be_converted_to_another_type(string requestedType)
    {
        // Arrange: Main là ví hệ thống đã được tạo trong lúc đăng ký.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Main type owner");
        var main = await fixture.AddWalletAsync(user.Id, "Main", true);

        // Act: thử đổi Main thành một loại ví phụ.
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateWalletCommandHandler(fixture.Db).Handle(
                new UpdateWalletCommand(
                    user.Id,
                    main.Id,
                    new UpdateWalletRequest("Renamed Main", requestedType, true)),
                default));

        // Assert: loại và tên cũ đều được giữ nguyên khi request bị từ chối.
        Assert.Equal("main_wallet_type_protected", error.Code);
        fixture.Db.ChangeTracker.Clear();
        var stored = (await fixture.Db.Wallets.FindAsync(main.Id))!;
        Assert.Equal(WalletType.Main, stored.Type);
        Assert.Equal("Main", stored.Name);
    }

    [Theory]
    [InlineData(WalletType.Savings)]
    [InlineData(WalletType.Emergency)]
    public async Task Non_main_wallet_cannot_be_converted_to_main(WalletType originalType)
    {
        // Arrange: tạo Main hệ thống và một ví phụ đúng loại ban đầu.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Secondary type owner");
        await fixture.AddWalletAsync(user.Id, "Main", true);
        var secondary = await fixture.AddWalletAsync(
            user.Id,
            "Secondary",
            false,
            type: originalType);

        // Act: thử gán loại Main cho ví phụ.
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateWalletCommandHandler(fixture.Db).Handle(
                new UpdateWalletCommand(
                    user.Id,
                    secondary.Id,
                    new UpdateWalletRequest("Secondary", "Main", false)),
                default));

        // Assert: handler trả Conflict và giữ nguyên loại ví phụ.
        Assert.Equal("main_wallet_type_protected", error.Code);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(originalType, (await fixture.Db.Wallets.FindAsync(secondary.Id))!.Type);
    }

    [Fact]
    public async Task First_non_main_wallet_automatically_becomes_default_for_legacy_user()
    {
        // Arrange: người dùng chưa có ví nào.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("First wallet owner");
        var handler = new CreateWalletCommandHandler(fixture.Db, new ActiveBankGuard(fixture.Db));

        // Act: client không chủ động chọn ví đầu tiên làm default.
        var result = await handler.Handle(
            new CreateWalletCommand(user.Id, new CreateWalletRequest("Savings", "Savings", false)),
            default);

        // Assert: hệ thống tự bảo đảm ví Active duy nhất là default.
        Assert.True(result.IsDefault);
        Assert.Single(await ActiveDefaultsAsync(fixture, user.Id));
    }

    [Fact]
    public async Task Create_repairs_active_wallets_without_default()
    {
        // Arrange: mô phỏng dữ liệu cũ có ví Active nhưng thiếu default.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Repair owner");
        await fixture.AddWalletAsync(user.Id, "Old", false);
        var handler = new CreateWalletCommandHandler(fixture.Db, new ActiveBankGuard(fixture.Db));

        // Act: tạo thêm ví mà không yêu cầu default.
        var result = await handler.Handle(
            new CreateWalletCommand(user.Id, new CreateWalletRequest("New", "Savings", false)),
            default);

        // Assert: ví mới trở thành default và toàn bộ dữ liệu có đúng một default Active.
        var defaults = await ActiveDefaultsAsync(fixture, user.Id);
        Assert.Single(defaults);
        Assert.Equal(result.Id, defaults[0].Id);
    }

    [Fact]
    public async Task Setting_another_wallet_default_leaves_exactly_one()
    {
        // Arrange: ví Main đang là default và ví Savings đang Active.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Switch owner");
        var main = await fixture.AddWalletAsync(user.Id, "Main", true);
        var savings = await fixture.AddWalletAsync(
            user.Id,
            "Savings",
            false,
            type: WalletType.Savings);
        var handler = new UpdateWalletCommandHandler(fixture.Db);

        // Act: chọn ví Savings làm default qua Update metadata.
        await handler.Handle(
            new UpdateWalletCommand(
                user.Id,
                savings.Id,
                new UpdateWalletRequest("Savings", "Savings", true)),
            default);

        // Assert: cờ default chuyển đúng một lần, không thay số dư.
        fixture.Db.ChangeTracker.Clear();
        var defaults = await ActiveDefaultsAsync(fixture, user.Id);
        Assert.Single(defaults);
        Assert.Equal(savings.Id, defaults[0].Id);
        Assert.False((await fixture.Db.Wallets.FindAsync(main.Id))!.IsDefault);
    }

    [Fact]
    public async Task Unsetting_default_promotes_another_active_wallet()
    {
        // Arrange: có một default và một ví Active dự phòng.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Promote owner");
        var current = await fixture.AddWalletAsync(user.Id, "Current", true);
        var fallback = await fixture.AddWalletAsync(user.Id, "Fallback", false);
        var handler = new UpdateWalletCommandHandler(fixture.Db);

        // Act: bỏ cờ default khỏi ví hiện tại.
        await handler.Handle(
            new UpdateWalletCommand(
                user.Id,
                current.Id,
                new UpdateWalletRequest("Current", "Main", false)),
            default);

        // Assert: fallback Active được chọn theo thứ tự ổn định.
        fixture.Db.ChangeTracker.Clear();
        var defaults = await ActiveDefaultsAsync(fixture, user.Id);
        Assert.Single(defaults);
        Assert.Equal(fallback.Id, defaults[0].Id);
    }

    [Fact]
    public async Task Unsetting_only_active_default_returns_conflict()
    {
        // Arrange: người dùng chỉ có một ví Active đang là default.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Only wallet owner");
        var wallet = await fixture.AddWalletAsync(user.Id, "Only", true);
        var handler = new UpdateWalletCommandHandler(fixture.Db);

        // Act: yêu cầu bỏ default sẽ phá invariant.
        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new UpdateWalletCommand(
                user.Id,
                wallet.Id,
                new UpdateWalletRequest("Only", "Main", false)),
            default));

        // Assert: API trả Conflict rõ ràng và dữ liệu vẫn giữ default cũ.
        Assert.Equal("default_wallet_required", error.Code);
        Assert.Equal(409, error.StatusCode);
        fixture.Db.ChangeTracker.Clear();
        Assert.True((await fixture.Db.Wallets.FindAsync(wallet.Id))!.IsDefault);
    }

    [Fact]
    public async Task Closing_default_promotes_another_active_wallet()
    {
        // Arrange: default có số dư bằng 0 và còn một ví Active dự phòng.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Close owner");
        var current = await fixture.AddWalletAsync(
            user.Id,
            "Current",
            true,
            type: WalletType.Savings);
        var fallback = await fixture.AddWalletAsync(user.Id, "Fallback", false);
        var handler = new CloseWalletCommandHandler(fixture.Db);

        // Act: đóng ví default theo lifecycle hiện có.
        await handler.Handle(new CloseWalletCommand(user.Id, current.Id), default);

        // Assert: ví đóng mất cờ default và fallback được chọn, không xóa ví nào.
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(WalletStatus.Closed, (await fixture.Db.Wallets.FindAsync(current.Id))!.Status);
        var defaults = await ActiveDefaultsAsync(fixture, user.Id);
        Assert.Single(defaults);
        Assert.Equal(fallback.Id, defaults[0].Id);
        Assert.Equal(2, await fixture.Db.Wallets.CountAsync());
    }

    [Fact]
    public async Task Main_wallet_cannot_be_closed_even_with_zero_balance()
    {
        // Arrange: chỉ còn một ví Active, default và không có số dư.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Final wallet owner");
        var wallet = await fixture.AddWalletAsync(user.Id, "Final", true);
        var handler = new CloseWalletCommandHandler(fixture.Db);

        // Act: thử đóng ví Main dù số dư đã bằng 0.
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CloseWalletCommand(user.Id, wallet.Id), default));

        // Assert: Main vẫn Active/default và API trả Conflict rõ ràng.
        Assert.Equal("main_wallet_protected", error.Code);
        fixture.Db.ChangeTracker.Clear();
        var stored = (await fixture.Db.Wallets.FindAsync(wallet.Id))!;
        Assert.Equal(WalletStatus.Active, stored.Status);
        Assert.True(stored.IsDefault);
    }

    [Fact]
    public async Task Freezing_default_promotes_another_active_wallet()
    {
        // Arrange: admin đóng băng default khi còn một ví Active dự phòng.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin");
        var owner = await fixture.AddUserAsync("Freeze owner");
        var current = await fixture.AddWalletAsync(owner.Id, "Current", true);
        var fallback = await fixture.AddWalletAsync(owner.Id, "Fallback", false);
        var handler = new FreezeWalletCommandHandler(
            fixture.Db,
            new AdminAuditWriter(fixture.Db));

        // Act: đóng băng ví default trong cùng transaction với audit.
        await handler.Handle(
            new FreezeWalletCommand(
                admin.Id,
                current.Id,
                new FreezeWalletRequest(true, "Security review")),
            default);

        // Assert: ví Frozen không còn default và fallback Active nhận cờ.
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(WalletStatus.Frozen, (await fixture.Db.Wallets.FindAsync(current.Id))!.Status);
        var defaults = await ActiveDefaultsAsync(fixture, owner.Id);
        Assert.Single(defaults);
        Assert.Equal(fallback.Id, defaults[0].Id);
    }

    [Fact]
    public async Task Unfreezing_when_no_active_wallet_makes_wallet_default()
    {
        // Arrange: người dùng chỉ có một ví Frozen và không có default Active.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin");
        var owner = await fixture.AddUserAsync("Unfreeze owner");
        var wallet = await fixture.AddWalletAsync(
            owner.Id,
            "Frozen",
            false,
            WalletStatus.Frozen);
        var handler = new FreezeWalletCommandHandler(
            fixture.Db,
            new AdminAuditWriter(fixture.Db));

        // Act: admin mở khóa ví.
        await handler.Handle(
            new FreezeWalletCommand(
                admin.Id,
                wallet.Id,
                new FreezeWalletRequest(false, "Review completed")),
            default);

        // Assert: ví vừa Active trở thành default duy nhất.
        fixture.Db.ChangeTracker.Clear();
        var defaults = await ActiveDefaultsAsync(fixture, owner.Id);
        Assert.Single(defaults);
        Assert.Equal(wallet.Id, defaults[0].Id);
    }

    [Fact]
    public async Task Database_failure_rolls_back_close_and_default_transition()
    {
        // Arrange: trigger mô phỏng lỗi DB khi fallback sắp nhận cờ default.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Rollback owner");
        var current = await fixture.AddWalletAsync(
            user.Id,
            "Current",
            true,
            type: WalletType.Savings);
        var fallback = await fixture.AddWalletAsync(user.Id, "Fallback", false);
        await fixture.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER fail_wallet_fallback
            BEFORE UPDATE ON Wallets
            WHEN NEW.Name = 'Fallback' AND NEW.IsDefault = 1
            BEGIN
                SELECT RAISE(ABORT, 'forced wallet failure');
            END;
            """);
        var handler = new CloseWalletCommandHandler(fixture.Db);

        // Act: SaveChanges lỗi sau khi handler đã đổi trạng thái trong memory.
        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(
            new CloseWalletCommand(user.Id, current.Id),
            default));

        // Assert: database transaction rollback cả Close lẫn chuyển default.
        fixture.Db.ChangeTracker.Clear();
        var storedCurrent = await fixture.Db.Wallets.FindAsync(current.Id);
        var storedFallback = await fixture.Db.Wallets.FindAsync(fallback.Id);
        Assert.Equal(WalletStatus.Active, storedCurrent!.Status);
        Assert.True(storedCurrent.IsDefault);
        Assert.Equal(WalletStatus.Active, storedFallback!.Status);
        Assert.False(storedFallback.IsDefault);
    }

    private static Task<List<Wallet>> ActiveDefaultsAsync(
        TargetedCorrectnessFixture fixture,
        Guid userId)
    {
        // Chỉ đếm cờ default trên các ví đang Active của đúng người dùng.
        return fixture.Db.Wallets
            .AsNoTracking()
            .Where(wallet => wallet.OwnerUserId == userId
                && wallet.Status == WalletStatus.Active
                && wallet.IsDefault)
            .ToListAsync();
    }
}
