using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Categories.Commands.ArchiveCategory;
using SpendWise.Application.Features.Categories.Commands.CreateCategory;
using SpendWise.Application.Features.Categories.Queries.GetCategories;
using SpendWise.Application.Features.RecurringTransactions.Commands.CreateRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Queries.GetRecurringTransactionById;
using SpendWise.Application.Features.Wallets.Commands.CreateWallet;
using SpendWise.Application.Features.Wallets.Commands.UpdateWallet;
using SpendWise.Application.Features.Transactions.Queries.GetTransactionDetails;
using SpendWise.Application.Finance;
using SpendWise.Application.Planning;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using Xunit;

namespace SpendWise.Tests;

// Bảo vệ các tên/field mà frontend đã khóa trước khi tích hợp.
public sealed class LockedFrontendContractTests
{
    private static readonly string[] RequiredSystemKeys =
    [
        "categories.salary", "categories.p2p", "categories.investments", "categories.income_other",
        "categories.food", "categories.shopping", "categories.transport", "categories.entertainment",
        "categories.utilities", "categories.education", "categories.savings_goals", "categories.expense_other"
    ];

    [Fact]
    public async Task Wallet_contract_maps_main_to_spending_and_accepts_locked_input_names()
    {
        // Arrange: mỗi ví đi qua handler thật thay vì ánh xạ enum trực tiếp trong test.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Wallet contract");
        var main = await fixture.AddWalletAsync(user.Id, "Daily", true);
        var handler = new CreateWalletCommandHandler(fixture.Db, new SpendWise.Application.Features.Banking.ActiveBankGuard(fixture.Db));

        // Act: Main do hệ thống tạo; client chỉ tạo hai loại ví phụ.
        var spending = await new UpdateWalletCommandHandler(fixture.Db).Handle(
            new UpdateWalletCommand(user.Id, main.Id, new("Daily", "spending", true)),
            default);
        var savings = await handler.Handle(new CreateWalletCommand(user.Id, new("Reserve", "savings", false)), default);
        var emergency = await handler.Handle(new CreateWalletCommand(user.Id, new("Emergency", "emergency", false)), default);

        // Assert: output không lộ tên enum Main cũ và input sai bị từ chối.
        Assert.Equal("spending", spending.Type);
        Assert.Equal("savings", savings.Type);
        Assert.Equal("emergency", emergency.Type);
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CreateWalletCommand(user.Id, new("Invalid", "credit", false)), default));
    }

    [Fact]
    public async Task Ef_model_and_clean_database_contain_authoritative_system_categories()
    {
        // Arrange: EnsureCreated chỉ dùng EF model; DevelopmentSeeder không hề được gọi.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var categoryType = fixture.Db.Model.FindEntityType(typeof(Category));
        var recurringType = fixture.Db.Model.FindEntityType(typeof(SpendWise.Domain.Planning.RecurringTransaction));

        // Act: đọc seed product configuration từ database sạch.
        var keys = await fixture.Db.Categories.Where(item => item.IsSystem && item.IsActive)
            .OrderBy(item => item.Key).Select(item => item.Key).ToArrayAsync();

        // Assert: Key/Title là required, Key unique và đủ đúng 12 key đã khóa.
        Assert.False(categoryType!.FindProperty(nameof(Category.Key))!.IsNullable);
        Assert.Contains(categoryType.GetIndexes(), index => index.IsUnique
            && index.Properties.Single().Name == nameof(Category.Key));
        Assert.False(recurringType!.FindProperty("Title")!.IsNullable);
        Assert.Equal(RequiredSystemKeys.OrderBy(item => item), keys);
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Custom_category_has_stable_key_and_delete_chooses_archive_or_hard_delete()
    {
        // Arrange: tạo hai danh mục qua use case thật; một danh mục sẽ được Budget tham chiếu.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Category contract");
        var create = new CreateCategoryCommandHandler(fixture.Db);
        var referenced = await create.Handle(new CreateCategoryCommand(user.Id,
            new CreateCategoryRequest("Food custom", "Expense")), default);
        var unused = await create.Handle(new CreateCategoryCommand(user.Id,
            new CreateCategoryRequest("Unused custom", "Expense")), default);
        fixture.Db.Budgets.Add(new SpendWise.Domain.Planning.Budget
        {
            OwnerUserId = user.Id, CategoryId = referenced.Id, Year = 2026, Month = 9, LimitAmount = 100_000
        });
        await fixture.Db.SaveChangesAsync();

        // Act: endpoint semantics dựa trên tham chiếu thật, không xóa lịch sử.
        var archive = new ArchiveCategoryCommandHandler(fixture.Db);
        var archived = await archive.Handle(new ArchiveCategoryCommand(user.Id, referenced.Id), default);
        var deleted = await archive.Handle(new ArchiveCategoryCommand(user.Id, unused.Id), default);

        // Assert: key custom ổn định sau khi lưu; list mặc định ẩn archive, includeArchived hiển thị lại.
        Assert.StartsWith("custom:", referenced.Key, StringComparison.Ordinal);
        Assert.True(archived.Archived);
        Assert.False(deleted.Archived);
        Assert.Null(await fixture.Db.Categories.FindAsync(unused.Id));
        var list = new GetCategoriesQueryHandler(fixture.Db);
        Assert.DoesNotContain(await list.Handle(new GetCategoriesQuery(user.Id, null), default), item => item.Id == referenced.Id);
        Assert.Contains(await list.Handle(new GetCategoriesQuery(user.Id, null, true), default), item => item.Id == referenced.Id && !item.IsActive);
    }

    [Fact]
    public async Task Recurring_title_and_description_round_trip_independently_without_posting_money()
    {
        // Arrange: tạo ví, chọn danh mục hệ thống và ghi nhận số giao dịch trước thao tác.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Recurring contract");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", true, balance: 500_000);
        var category = await fixture.Db.Categories.SingleAsync(item => item.Key == "categories.food");
        var before = await fixture.Db.FinancialTransactions.CountAsync();

        // Act: create chỉ lưu định nghĩa lịch, không tự ghi tiền.
        var created = await new CreateRecurringTransactionCommandHandler(fixture.Db, fixture.Clock).Handle(
            new CreateRecurringTransactionCommand(user.Id, new CreateRecurringRequest(
                wallet.Id, category.Id, "Expense", 50_000, "Monthly internet invoice", "Monthly",
                DateTime.UtcNow.AddDays(10), true, "Internet")), default);
        var detail = await new GetRecurringTransactionByIdQueryHandler(fixture.Db, fixture.Clock)
            .Handle(new GetRecurringTransactionByIdQuery(user.Id, created.Id), default);

        // Assert: hai trường không bị gộp và không có financial posting mới.
        Assert.Equal("Internet", detail.Title);
        Assert.Equal("Monthly internet invoice", detail.Description);
        Assert.Equal(before, await fixture.Db.FinancialTransactions.CountAsync());
    }

    [Fact]
    public async Task Transaction_detail_returns_evidence_structured_corrections_actor_and_scoped_grants()
    {
        // Arrange: dựng một giao dịch đã sửa có evidence gốc, evidence bổ sung và grant của Admin.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Transaction owner");
        var stranger = await fixture.AddUserAsync("Transaction stranger");
        var admin = await fixture.AddUserAsync("Correction admin", role: SystemRole.Admin);
        var wallet = await fixture.AddWalletAsync(owner.Id, "Main wallet", true, balance: 1_000_000);
        var category = await fixture.Db.Categories.SingleAsync(item => item.Key == "categories.food");
        var originalLedger = Ledger("original");
        var reversalLedger = Ledger("reversal");
        var correctedLedger = Ledger("corrected");
        var transaction = new FinancialTransaction
        {
            OwnerUserId = owner.Id,
            WalletId = wallet.Id,
            Wallet = wallet,
            CategoryId = category.Id,
            Category = category,
            LedgerTransactionId = correctedLedger.Id,
            LedgerTransaction = correctedLedger,
            Type = FinancialTransactionType.Expense,
            Amount = 2_000,
            Description = "Corrected lunch",
            OccurredAt = DateTime.UtcNow,
            IsManual = true,
            CorrectionAllowance = 2,
            CorrectionCount = 1
        };
        transaction.Evidence.Add(Evidence(owner.Id, "original.png"));
        var correction = new TransactionCorrection
        {
            Transaction = transaction,
            TransactionId = transaction.Id,
            ActorUserId = admin.Id,
            CorrectionNumber = 1,
            Reason = "Amount corrected",
            OriginalValuesJson = "{}",
            CorrectedValuesJson = "{}",
            TransactionType = FinancialTransactionType.Expense,
            OriginalCategoryId = category.Id,
            CorrectedCategoryId = category.Id,
            OriginalDescription = "Lunch",
            CorrectedDescription = "Corrected lunch",
            OriginalWalletId = wallet.Id,
            CorrectedWalletId = wallet.Id,
            OriginalAmount = 1_000,
            CorrectedAmount = 2_000,
            OriginalOccurredAt = transaction.OccurredAt.AddMinutes(-5),
            CorrectedOccurredAt = transaction.OccurredAt,
            OriginalLedgerTransactionId = originalLedger.Id,
            ReversalLedgerTransactionId = reversalLedger.Id,
            CorrectedLedgerTransactionId = correctedLedger.Id
        };
        correction.Evidence.Add(Evidence(owner.Id, "correction.png"));
        transaction.Corrections.Add(correction);
        var grant = new SupportCase
        {
            TicketNumber = "SUP-GRANT",
            LinkedUserId = owner.Id,
            LinkedTransactionId = transaction.Id,
            Category = "transaction_edit",
            Subject = "Correction",
            Description = "Approved",
            CorrectionGranted = true,
            CorrectionGrantedAt = DateTime.UtcNow,
            CorrectionGrantedByAdminId = admin.Id,
            CorrectionGrantReason = "Verified receipt"
        };
        var unrelated = new SupportCase
        {
            TicketNumber = "SUP-OTHER",
            LinkedUserId = stranger.Id,
            LinkedTransactionId = transaction.Id,
            Category = "transaction_edit",
            Subject = "Other user",
            Description = "Must not leak",
            CorrectionGranted = true,
            CorrectionGrantedAt = DateTime.UtcNow,
            CorrectionGrantedByAdminId = admin.Id,
            CorrectionGrantReason = "Unrelated"
        };
        fixture.Db.AddRange(originalLedger, reversalLedger, correctedLedger, transaction, grant, unrelated);
        await fixture.Db.SaveChangesAsync();

        // Act: query theo owner và transaction id.
        var handler = new GetTransactionDetailsQueryHandler(fixture.Db);
        var detail = await handler.Handle(new(owner.Id, transaction.Id), default);

        // Assert: DTO có đủ evidence/snapshot/tên actor và chỉ grant của owner.
        Assert.Single(detail.Evidence!);
        var correctionDto = Assert.Single(detail.Corrections!);
        Assert.Single(correctionDto.Evidence);
        Assert.Equal("Correction admin", correctionDto.ActorName);
        Assert.Equal("Admin", correctionDto.ActorRole);
        Assert.Equal("Ăn uống", correctionDto.Before!.Category);
        Assert.Equal("Main wallet", correctionDto.Before.WalletName);
        Assert.Equal(1_000, correctionDto.Before.Amount);
        Assert.Equal(2_000, correctionDto.After!.Amount);
        var grantDto = Assert.Single(detail.CorrectionGrants!);
        Assert.Equal(grant.Id, grantDto.SupportCaseId);
        Assert.Equal("Verified receipt", grantDto.Reason);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(stranger.Id, transaction.Id), default));
    }

    private static LedgerTransaction Ledger(string suffix) => new()
    {
        Reference = $"TEST-{suffix}-{Guid.NewGuid():N}",
        BusinessType = "Test",
        BusinessId = Guid.NewGuid(),
        Description = suffix,
        Status = LedgerTransactionStatus.Completed,
        CompletedAt = DateTime.UtcNow
    };

    private static EvidenceAttachment Evidence(Guid ownerId, string name) => new()
    {
        OwnerUserId = ownerId,
        OriginalFileName = name,
        ContentType = "image/png",
        Length = 8,
        ContentHash = Guid.NewGuid().ToString("N"),
        Content = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
    };
}
