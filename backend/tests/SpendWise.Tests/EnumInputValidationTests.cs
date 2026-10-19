using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Users.Commands.ChangeUserStatus;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Categories.Commands.CreateCategory;
using SpendWise.Application.Features.Categories.Queries.GetCategories;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Groups.Commands.CreateGroup;
using SpendWise.Application.Features.RecurringTransactions.Commands.CreateRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.UpdateRecurringTransaction;
using SpendWise.Application.Features.Transactions.Queries.GetTransactions;
using SpendWise.Application.Features.Wallets.Commands.CreateWallet;
using SpendWise.Application.Features.Wallets.Commands.UpdateWallet;
using SpendWise.Application.Finance;
using SpendWise.Application.Groups;
using SpendWise.Application.Planning;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

// Các test này ngăn chuỗi số ngoài enum đi vào handler từ request hoặc query string.
public sealed class EnumInputValidationTests
{
    [Fact]
    public async Task Change_user_status_rejects_undefined_numeric_value()
    {
        // Arrange: tạo admin, user và dependency thật của handler.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        var pinService = new SecurityPinService();
        admin.SecurityPinHash = pinService.Hash("123456");
        await fixture.Db.SaveChangesAsync();
        var target = await fixture.AddUserAsync("Status target");
        var tokens = new TargetedTokenService();
        var handler = new ChangeUserStatusCommandHandler(
            fixture.Db,
            new AuthenticationSupport(fixture.Db, tokens, fixture.Clock),
            new AdminAuditWriter(fixture.Db),
            new AdminSecurityGuard(fixture.Db, pinService,
                new AccountCredentialLockout(fixture.Db, fixture.Clock)));

        // Act: "999" parse được về số nhưng không thuộc UserStatus.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new ChangeUserStatusCommand(
                admin.Id,
                target.Id,
                new ChangeUserStatusRequest("999", "Invalid status test", "123456")),
            default));

        // Assert: lỗi được xử lý như client input 400 và không sửa trạng thái.
        Assert.Equal("invalid_admin_action", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Create_wallet_rejects_undefined_numeric_type()
    {
        // Arrange: CreateWallet kiểm tra type trước khi ghi database.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = new CreateWalletCommandHandler(fixture.Db);

        // Act: gửi WalletType ngoài enum.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CreateWalletCommand(
                Guid.NewGuid(),
                new CreateWalletRequest("Invalid", "999")),
            default));

        // Assert: giữ nguyên mã lỗi Wallet hiện có.
        Assert.Equal("invalid_wallet_type", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Update_wallet_rejects_undefined_numeric_type()
    {
        // Arrange: UpdateWallet phải dừng trước khi tìm hoặc sửa ví.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = new UpdateWalletCommandHandler(fixture.Db);

        // Act: gửi WalletType số không được định nghĩa.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new UpdateWalletCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new UpdateWalletRequest("Invalid", "999")),
            default));

        // Assert: không biến numeric enum thành metadata hợp lệ.
        Assert.Equal("invalid_wallet", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Create_group_rejects_undefined_numeric_type()
    {
        // Arrange: dùng GroupAccess thật nhưng validation phải dừng trước khi ghi nhóm.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Invalid group owner");
        var handler = new CreateGroupCommandHandler(
            fixture.Db,
            fixture.Clock,
            new GroupAccess(fixture.Db),
            fixture.Security);

        // Act: gửi GroupType ngoài enum.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CreateGroupCommand(
                user.Id,
                new CreateGroupRequest("Invalid group", null, "999", SecurityPin: TargetedCorrectnessFixture.SecurityPin)),
            default));

        // Assert: handler trả validation 400 bằng mã lỗi cũ.
        Assert.Equal("invalid_group", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Create_category_rejects_undefined_numeric_kind()
    {
        // Arrange: CategoryKind được kiểm tra trước unique query.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = new CreateCategoryCommandHandler(fixture.Db);

        // Act: gửi CategoryKind numeric không được định nghĩa.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CreateCategoryCommand(
                Guid.NewGuid(),
                new CreateCategoryRequest("Invalid", "999")),
            default));

        // Assert: không tạo Category mang enum ngoài domain.
        Assert.Equal("invalid_category_kind", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Create_recurring_rejects_undefined_numeric_frequency()
    {
        // Arrange: reference ids không cần tồn tại vì enum phải bị từ chối trước.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = new CreateRecurringTransactionCommandHandler(fixture.Db, fixture.Clock);

        // Act: gửi RecurringFrequency ngoài enum.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CreateRecurringTransactionCommand(
                Guid.NewGuid(),
                RecurringRequest("Expense", "999")),
            default));

        // Assert: giữ nguyên validation code của recurring configuration.
        Assert.Equal("invalid_recurring", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Update_recurring_rejects_undefined_numeric_frequency()
    {
        // Arrange: Update cũng phải kiểm tra enum trước khi tìm definition.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = new UpdateRecurringTransactionCommandHandler(fixture.Db, fixture.Clock);

        // Act: gửi RecurringFrequency numeric không được định nghĩa.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new UpdateRecurringTransactionCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                UpdateRecurringRequest("Expense", "999")),
            default));

        // Assert: không ghi giá trị ngoài enum vào lịch tương lai.
        Assert.Equal("invalid_recurring", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

 #if RETIRED_LEGACY_TESTS
    [Fact]
    public async Task Create_shared_expense_rejects_undefined_numeric_split_method()
    {
        // Arrange: tạo nhóm hợp lệ để request đi đến bước parse enum, chưa gọi Ledger.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var (ownerId, groupId) = await AddGroupAsync(fixture);
        var handler = SharedExpenseHandler(fixture);

        // Act: SplitMethod "999" phải bị chặn trước thuật toán chia tiền.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CreateSharedExpenseCommand(
                ownerId,
                groupId,
                SharedExpenseRequest(ownerId, "User", "999", "invalid-split")),
            default));

        // Assert: không rơi vào nhánh chia mặc định hoặc tạo posting.
        Assert.Equal("invalid_shared_expense", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Create_shared_expense_rejects_undefined_numeric_payer_type()
    {
        // Arrange: nhóm hợp lệ giúp kiểm tra trực tiếp ExpensePayerType từ client.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var (ownerId, groupId) = await AddGroupAsync(fixture);
        var handler = SharedExpenseHandler(fixture);

        // Act: PaidByType "999" không được phép rơi vào nhánh SharedFund.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CreateSharedExpenseCommand(
                ownerId,
                groupId,
                SharedExpenseRequest(ownerId, "999", "Equal", "invalid-payer")),
            default));

        // Assert: từ chối bằng validation 400 trước mọi thao tác tài chính.
        Assert.Equal("invalid_shared_expense", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

 #endif
    [Fact]
    public async Task Category_query_rejects_undefined_numeric_kind()
    {
        // Arrange: kind là query string tùy chọn nhưng nếu có thì phải hợp lệ.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = new GetCategoriesQueryHandler(fixture.Db);

        // Act: dùng numeric CategoryKind ngoài enum trong bộ lọc.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new GetCategoriesQuery(Guid.NewGuid(), "999"),
            default));

        // Assert: không âm thầm bỏ qua hoặc chạy query với enum 999.
        Assert.Equal("invalid_category_kind", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Transaction_query_rejects_undefined_numeric_type()
    {
        // Arrange: Type đến trực tiếp từ query string của API giao dịch.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = new GetTransactionsQueryHandler(fixture.Db);

        // Act: gửi FinancialTransactionType numeric không được định nghĩa.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new GetTransactionsQuery(
                Guid.NewGuid(),
                new TransactionQuery(Type: "999")),
            default));

        // Assert: trả mã lỗi rõ ràng thay vì lọc bằng giá trị enum ngoài domain.
        Assert.Equal("invalid_transaction_type", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    private static CreateRecurringRequest RecurringRequest(string kind, string frequency) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            kind,
            100,
            "Recurring test",
            frequency,
            DateTime.UtcNow.AddDays(1));

    private static UpdateRecurringRequest UpdateRecurringRequest(string kind, string frequency) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            kind,
            100,
            "Recurring test",
            frequency,
            DateTime.UtcNow.AddDays(1));

 #if RETIRED_LEGACY_TESTS
    private static async Task<(Guid OwnerId, Guid GroupId)> AddGroupAsync(
        TargetedCorrectnessFixture fixture)
    {
        // Tạo đúng membership Owner và SharedFund qua use case hiện có.
        var owner = await fixture.AddUserAsync("Group owner");
        var handler = new CreateGroupCommandHandler(
            fixture.Db,
            fixture.Clock,
            new GroupAccess(fixture.Db),
            fixture.Security);
        var group = await handler.Handle(
            new CreateGroupCommand(
                owner.Id,
                new CreateGroupRequest("Enum group", null, "Family", SecurityPin: TargetedCorrectnessFixture.SecurityPin)),
            default);
        return (owner.Id, group.Id);
    }

    private static CreateSharedExpenseCommandHandler SharedExpenseHandler(
        TargetedCorrectnessFixture fixture) =>
        new(
            fixture.Db,
            new NeverLedgerService(),
            fixture.Clock,
            new NeverOperationPolicy(),
            new GroupAccess(fixture.Db));

    private static CreateSharedExpenseRequest SharedExpenseRequest(
        Guid ownerId,
        string payerType,
        string splitMethod,
        string idempotencyKey) =>
        new(
            100,
            "Food",
            "Enum validation",
            payerType,
            ownerId,
            null,
            splitMethod,
            [new ExpenseSplitInput(ownerId, 100)],
            IdempotencyKey: idempotencyKey);
 #endif

    // Các dependency này không được gọi vì enum invalid phải dừng handler trước posting.
    private sealed class NeverLedgerService : ILedgerService
    {
        public Task<LedgerPostingResult> PostAsync(
            Guid sourceAccountId,
            Guid destinationAccountId,
            decimal amount,
            string currency,
            string businessType,
            Guid businessId,
            string? idempotencyScope,
            string? idempotencyKey,
            string description,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Ledger must not be called for invalid enum input.");

        public Task<LedgerPostingResult> PostEntriesAsync(
            IReadOnlyList<LedgerPostingEntry> entries,
            string currency,
            string businessType,
            Guid businessId,
            string? idempotencyScope,
            string? idempotencyKey,
            string description,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Ledger must not be called for invalid enum input.");

        public Task<LedgerPostingResult> ReverseAsync(
            Guid originalLedgerTransactionId,
            Guid reversalBusinessId,
            string idempotencyKey,
            string reason,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Ledger must not be called for invalid enum input.");
    }

    private sealed class NeverOperationPolicy : IOperationPolicy
    {
        public Task EnsureFeatureEnabledAsync(
            string featureName,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Policy must not be called for invalid enum input.");

        public Task<decimal> GetDecimalAsync(
            string key,
            decimal fallback,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Policy must not be called for invalid enum input.");
    }
}
