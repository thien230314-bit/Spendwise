using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.BankTransactions.Commands.ReclassifyBankTransaction;
using SpendWise.Application.Features.Evidence;
using SpendWise.Application.Features.Evidence.Queries.GetEvidence;
using SpendWise.Application.Features.Goals.Commands;
using SpendWise.Application.Features.Goals.Commands.ContributeGoal;
using SpendWise.Application.Features.Goals.Commands.CreateGoal;
using SpendWise.Application.Features.Goals.Commands.WithdrawGoal;
using SpendWise.Application.Features.RecurringTransactions;
using SpendWise.Application.Features.RecurringTransactions.Commands.DeleteRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.RecordRecurringOccurrence;
using SpendWise.Application.Features.Transactions.Commands;
using SpendWise.Application.Features.Transactions.Commands.UpdateTransaction;
using SpendWise.Application.Features.Transfers.Commands.TransferBetweenWallets;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Finance;
using SpendWise.Application.Planning;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Planning;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Planning;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ PIN, evidence, correction và recurring không tự động ghi tiền.
public sealed class FinancialIntegrityPhaseTests
{
    private const string Pin = "123456";
    private const string WrongPin = "654321";
    private static readonly SecurityPinService Pins = new();

    [Theory]
    [InlineData(CategoryKind.Income)]
    [InlineData(CategoryKind.Expense)]
    public async Task Manual_transaction_requires_current_pin_and_evidence_without_side_effects(
        CategoryKind kind)
    {
        // Arrange: tạo người dùng có PIN, ví, danh mục và một evidence hợp lệ.
        await using var fixture = await IntegrityFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Manual owner");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", 1_000, true);
        var category = await fixture.AddCategoryAsync(user.Id, kind, kind.ToString());
        var evidence = await fixture.AddEvidenceAsync(user.Id, $"{kind}.png");
        var creator = fixture.ManualCreator();
        var type = kind == CategoryKind.Income ? CategoryKind.Income : CategoryKind.Expense;

        // Act: PIN sai bị chặn trước khi ledger hoặc evidence thay đổi.
        var wrongPin = await Assert.ThrowsAsync<AppException>(() => creator.CreateAsync(
            user.Id,
            new CreateManualTransactionRequest(
                wallet.Id, category.Id, 100, "Manual", SecurityPin: WrongPin,
                EvidenceIds: [evidence.Id]),
            type,
            default));
        Assert.Equal("pin_verification_failed", wrongPin.Code);
        Assert.Empty(fixture.Db.LedgerTransactions);
        Assert.Empty(fixture.Db.FinancialTransactions);

        // Act: PIN đúng nhưng không có evidence vẫn bị từ chối.
        var missingEvidence = await Assert.ThrowsAsync<AppException>(() => creator.CreateAsync(
            user.Id,
            new CreateManualTransactionRequest(
                wallet.Id, category.Id, 100, "Manual", SecurityPin: Pin,
                EvidenceIds: []),
            type,
            default));
        Assert.Equal("invalid_evidence_count", missingEvidence.Code);
        Assert.Empty(fixture.Db.LedgerTransactions);

        // Act: PIN và evidence hợp lệ cho phép ghi giao dịch đúng một lần.
        var result = await creator.CreateAsync(
            user.Id,
            new CreateManualTransactionRequest(
                wallet.Id, category.Id, 100, "Manual", IdempotencyKey: $"manual-{kind}",
                SecurityPin: Pin, EvidenceIds: [evidence.Id]),
            type,
            default);

        Assert.Equal(1, result.CorrectionAllowance);
        Assert.Equal(0, result.CorrectionCount);
        Assert.True(result.CanCorrect);
        Assert.Single(result.Evidence!);
        Assert.Single(fixture.Db.LedgerTransactions);
        Assert.Single(fixture.Db.FinancialTransactions);
        Assert.Equal(
            result.Id,
            (await fixture.Db.EvidenceAttachments.FindAsync(evidence.Id))!.FinancialTransactionId);
    }

    [Fact]
    public async Task Evidence_validation_enforces_size_type_count_ownership_and_private_read()
    {
        // Arrange: hai người dùng không được chia sẻ evidence cho nhau.
        await using var fixture = await IntegrityFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Evidence owner");
        var stranger = await fixture.AddUserAsync("Evidence stranger");
        var support = new EvidenceSupport(fixture.Db);

        // Assert: file quá 2 MB, MIME/extension không hợp lệ và path traversal đều bị chặn.
        var oversized = new byte[EvidenceSupport.MaximumFileSize + 1];
        oversized[0] = 0x89;
        oversized[1] = 0x50;
        Assert.Equal("invalid_evidence_size", Assert.Throws<AppException>(() =>
            support.Create(owner.Id, new UploadEvidenceData("large.png", "image/png", oversized))).Code);
        Assert.Equal("invalid_evidence_type", Assert.Throws<AppException>(() =>
            support.Create(owner.Id, new UploadEvidenceData("script.svg", "image/svg+xml", "<svg/>"u8.ToArray()))).Code);
        Assert.Equal("invalid_evidence_name", Assert.Throws<AppException>(() =>
            support.Create(owner.Id, new UploadEvidenceData("../receipt.png", "image/png", Png()))).Code);

        var owned = new List<EvidenceAttachment>();
        for (var index = 0; index < 4; index++)
        {
            owned.Add(support.Create(owner.Id,
                new UploadEvidenceData($"receipt-{index}.png", "image/png", Png())));
        }
        var foreign = support.Create(stranger.Id,
            new UploadEvidenceData("foreign.pdf", "application/pdf", "%PDF-1.7"u8.ToArray()));
        fixture.Db.EvidenceAttachments.AddRange(owned);
        fixture.Db.EvidenceAttachments.Add(foreign);
        await fixture.Db.SaveChangesAsync();

        // Assert: một request chỉ nhận tối đa ba file và chỉ file của chính chủ sở hữu.
        Assert.Equal("invalid_evidence_count", (await Assert.ThrowsAsync<AppException>(() =>
            support.LoadOwnedAsync(owner.Id, owned.Select(item => item.Id).ToList(), true, true, default))).Code);
        Assert.Equal(3, (await support.LoadOwnedAsync(
            owner.Id, owned.Take(3).Select(item => item.Id).ToList(), true, true, default)).Count);
        Assert.Equal("invalid_evidence", (await Assert.ThrowsAsync<AppException>(() =>
            support.LoadOwnedAsync(owner.Id, [foreign.Id], true, true, default))).Code);

        var ownerRead = await new GetEvidenceQueryHandler(fixture.Db)
            .Handle(new GetEvidenceQuery(owner.Id, owned[0].Id), default);
        Assert.Equal(Png(), ownerRead.Content);
        await Assert.ThrowsAsync<NotFoundException>(() => new GetEvidenceQueryHandler(fixture.Db)
            .Handle(new GetEvidenceQuery(stranger.Id, owned[0].Id), default));
    }

    [Fact]
    public async Task Manual_transaction_rejects_foreign_evidence_and_rolls_back_failed_link()
    {
        // Arrange: ví không đủ tiền và evidence thuộc hai chủ sở hữu khác nhau.
        await using var fixture = await IntegrityFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Expense owner");
        var stranger = await fixture.AddUserAsync("Other owner");
        var wallet = await fixture.AddWalletAsync(owner.Id, "Main", 50, true);
        var category = await fixture.AddCategoryAsync(owner.Id, CategoryKind.Expense, "Food");
        var ownedEvidence = await fixture.AddEvidenceAsync(owner.Id, "owned.png");
        var foreignEvidence = await fixture.AddEvidenceAsync(stranger.Id, "foreign.png");
        var creator = fixture.ManualCreator();

        var foreignError = await Assert.ThrowsAsync<AppException>(() => creator.CreateAsync(
            owner.Id,
            new CreateManualTransactionRequest(
                wallet.Id, category.Id, 10, "Food", SecurityPin: Pin,
                EvidenceIds: [foreignEvidence.Id]),
            CategoryKind.Expense,
            default));
        Assert.Equal("invalid_evidence", foreignError.Code);

        // Act: ledger từ chối chi vượt số dư trong cùng transaction.
        await Assert.ThrowsAsync<ConflictException>(() => creator.CreateAsync(
            owner.Id,
            new CreateManualTransactionRequest(
                wallet.Id, category.Id, 100, "Too much", SecurityPin: Pin,
                EvidenceIds: [ownedEvidence.Id]),
            CategoryKind.Expense,
            default));

        // Assert: evidence vẫn chưa liên kết và không có posting/lịch sử dở dang.
        fixture.Db.ChangeTracker.Clear();
        var storedEvidence = await fixture.Db.EvidenceAttachments.FindAsync(ownedEvidence.Id);
        Assert.Null(storedEvidence!.FinancialTransactionId);
        Assert.Null(storedEvidence.TransactionCorrectionId);
        Assert.Empty(await fixture.Db.LedgerTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
    }

    [Fact]
    public async Task First_correction_changes_all_allowed_fields_and_preserves_original_ledger()
    {
        // Arrange: tạo Income thủ công và lưu snapshot bút toán gốc trước correction.
        await using var fixture = await IntegrityFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Correction owner");
        var originalWallet = await fixture.AddWalletAsync(user.Id, "Original", 100, true);
        var correctedWallet = await fixture.AddWalletAsync(user.Id, "Corrected", 0, false);
        var originalCategory = await fixture.AddCategoryAsync(user.Id, CategoryKind.Income, "Salary");
        var correctedCategory = await fixture.AddCategoryAsync(user.Id, CategoryKind.Income, "Bonus");
        var originalEvidence = await fixture.AddEvidenceAsync(user.Id, "original.png");
        var creator = fixture.ManualCreator();
        var originalOccurredAt = fixture.Clock.UtcNow.AddDays(-2);
        var original = await creator.CreateAsync(
            user.Id,
            new CreateManualTransactionRequest(
                originalWallet.Id, originalCategory.Id, 400, "Salary", originalOccurredAt,
                "income-correction", Pin, [originalEvidence.Id], "Employer"),
            CategoryKind.Income,
            default);
        var originalLedger = await fixture.Db.LedgerTransactions.AsNoTracking()
            .SingleAsync(item => item.Id == original.LedgerTransactionId);
        var originalEntries = await EntrySnapshotsAsync(fixture.Db, original.LedgerTransactionId);
        var correctionEvidence = await fixture.AddEvidenceAsync(user.Id, "correction.pdf", pdf: true);
        var correctedOccurredAt = fixture.Clock.UtcNow.AddDays(-1);
        fixture.Db.ChangeTracker.Clear();

        // Act: thay toàn bộ các trường frontend cho phép qua workflow correction duy nhất.
        var handler = fixture.CorrectionHandler();
        var result = await handler.Handle(
            new UpdateTransactionCommand(
                user.Id,
                original.Id,
                new UpdateTransactionRequest(
                    correctedCategory.Id,
                    "Corrected salary",
                    Pin,
                    [correctionEvidence.Id],
                    "Receipt and destination were wrong",
                    correctedOccurredAt,
                    correctedWallet.Id,
                    250,
                    "Corrected employer")),
            default);

        // Assert: entitlement tổng vẫn là 1, count tăng lên 1 và snapshot đủ thông tin.
        Assert.Equal(1, result.CorrectionAllowance);
        Assert.Equal(1, result.CorrectionCount);
        Assert.False(result.CanCorrect);
        var correction = Assert.Single(result.Corrections!);
        Assert.Equal("Receipt and destination were wrong", correction.Reason);
        Assert.Single(correction.Evidence);
        using var before = JsonDocument.Parse(correction.OriginalValuesJson);
        using var after = JsonDocument.Parse(correction.CorrectedValuesJson);
        Assert.Equal(400, before.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("Employer", before.RootElement.GetProperty("counterparty").GetString());
        Assert.Equal(250, after.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("Corrected employer", after.RootElement.GetProperty("counterparty").GetString());

        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.FinancialTransactions
            .Include(item => item.Corrections).ThenInclude(item => item.Evidence)
            .Include(item => item.Evidence)
            .SingleAsync(item => item.Id == original.Id);
        Assert.Equal(correctedWallet.Id, stored.WalletId);
        Assert.Equal(correctedCategory.Id, stored.CategoryId);
        Assert.Equal(250, stored.Amount);
        Assert.Equal("Corrected salary", stored.Description);
        Assert.Equal("Corrected employer", stored.CounterpartyName);
        Assert.Equal(correctedOccurredAt, stored.OccurredAt);
        var storedCorrection = Assert.Single(stored.Corrections);
        Assert.Equal(originalCategory.Id, storedCorrection.OriginalCategoryId);
        Assert.Equal(correctedCategory.Id, storedCorrection.CorrectedCategoryId);
        Assert.Equal("Salary", storedCorrection.OriginalDescription);
        Assert.Equal(FinancialTransactionType.Income, storedCorrection.TransactionType);
        Assert.Equal(original.Id, (await fixture.Db.EvidenceAttachments.FindAsync(originalEvidence.Id))!.FinancialTransactionId);
        Assert.Equal(correction.Id, (await fixture.Db.EvidenceAttachments.FindAsync(correctionEvidence.Id))!.TransactionCorrectionId);

        // Assert: entry gốc không đổi, posting gốc được đánh dấu Reversed và các posting mới đều cân bằng.
        var storedOriginalLedger = await fixture.Db.LedgerTransactions.AsNoTracking()
            .SingleAsync(item => item.Id == original.LedgerTransactionId);
        Assert.Equal(originalLedger.Reference, storedOriginalLedger.Reference);
        Assert.Equal(LedgerTransactionStatus.Completed, originalLedger.Status);
        Assert.Equal(LedgerTransactionStatus.Reversed, storedOriginalLedger.Status);
        Assert.Equal(originalEntries, await EntrySnapshotsAsync(fixture.Db, original.LedgerTransactionId));
        var reversalPosting = await fixture.Db.LedgerTransactions.AsNoTracking()
            .SingleAsync(item => item.Id == storedCorrection.ReversalLedgerTransactionId);
        Assert.Equal(original.LedgerTransactionId, reversalPosting.ReversesLedgerTransactionId);
        Assert.Equal(3, await fixture.Db.LedgerTransactions.CountAsync());
        await AssertLedgerBalancedAsync(fixture.Db);
        Assert.Equal(100, (await fixture.Db.Wallets.FindAsync(originalWallet.Id))!.AvailableBalance);
        Assert.Equal(250, (await fixture.Db.Wallets.FindAsync(correctedWallet.Id))!.AvailableBalance);

        // Act và Assert: correction trực tiếp lần hai bị từ chối, không thêm posting.
        var exhausted = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new UpdateTransactionCommand(
                user.Id,
                original.Id,
                new UpdateTransactionRequest(
                    correctedCategory.Id, "Second", Pin, [], "Try again", Amount: 200)),
            default));
        Assert.Equal("CORRECTION_ALLOWANCE_EXHAUSTED", exhausted.Code);
        Assert.Equal(3, await fixture.Db.LedgerTransactions.CountAsync());
    }

    [Fact]
    public async Task Correction_rejects_wrong_pin_semantic_change_and_rolls_back_insufficient_balance()
    {
        // Arrange: tạo Expense 250 trên ví 1.000 để thử correction tăng lên 1.200.
        await using var fixture = await IntegrityFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Rollback owner");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", 1_000, true);
        var expenseCategory = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense, "Food");
        var incomeCategory = await fixture.AddCategoryAsync(user.Id, CategoryKind.Income, "Salary");
        var originalEvidence = await fixture.AddEvidenceAsync(user.Id, "expense.png");
        var transaction = await fixture.ManualCreator().CreateAsync(
            user.Id,
            new CreateManualTransactionRequest(
                wallet.Id, expenseCategory.Id, 250, "Food", IdempotencyKey: "expense-correction",
                SecurityPin: Pin, EvidenceIds: [originalEvidence.Id]),
            CategoryKind.Expense,
            default);
        var additionalEvidence = await fixture.AddEvidenceAsync(user.Id, "extra.png");
        var handler = fixture.CorrectionHandler();

        // Assert: PIN sai dừng trước mọi thay đổi và category Income không thể gán cho Expense.
        Assert.Equal("pin_verification_failed", (await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new UpdateTransactionCommand(user.Id, transaction.Id,
                new UpdateTransactionRequest(expenseCategory.Id, "Wrong PIN", WrongPin, [], "No")),
            default))).Code);
        Assert.Equal("invalid_category", (await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new UpdateTransactionCommand(user.Id, transaction.Id,
                new UpdateTransactionRequest(incomeCategory.Id, "Wrong kind", Pin, [], "No")),
            default))).Code);

        // Act: reversal tạm thời khôi phục 250 nhưng posting 1.200 vẫn thiếu tiền.
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new UpdateTransactionCommand(user.Id, transaction.Id,
                new UpdateTransactionRequest(
                    expenseCategory.Id, "Too expensive", Pin, [additionalEvidence.Id],
                    "Increase beyond balance", Amount: 1_200)),
            default));

        // Assert: transaction ngoài rollback reversal, correction, evidence link và effective view cùng nhau.
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.FinancialTransactions.FindAsync(transaction.Id);
        Assert.Equal(250, stored!.Amount);
        Assert.Equal(0, stored.CorrectionCount);
        Assert.Single(await fixture.Db.LedgerTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.TransactionCorrections.ToListAsync());
        Assert.Null((await fixture.Db.EvidenceAttachments.FindAsync(additionalEvidence.Id))!.TransactionCorrectionId);
        Assert.Equal(750, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
    }

    [Theory]
    [InlineData(CategoryKind.Income, CategoryKind.Expense)]
    [InlineData(CategoryKind.Expense, CategoryKind.Income)]
    public async Task Correction_cannot_cross_income_expense_semantics(
        CategoryKind originalKind,
        CategoryKind oppositeKind)
    {
        // Arrange: tạo giao dịch thủ công và một category thuộc semantic ngược lại.
        await using var fixture = await IntegrityFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Semantic owner");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", 1_000, true);
        var originalCategory = await fixture.AddCategoryAsync(user.Id, originalKind, "Original");
        var oppositeCategory = await fixture.AddCategoryAsync(user.Id, oppositeKind, "Opposite");
        var evidence = await fixture.AddEvidenceAsync(user.Id, "semantic.png");
        var transaction = await fixture.ManualCreator().CreateAsync(
            user.Id,
            new CreateManualTransactionRequest(
                wallet.Id, originalCategory.Id, 100, "Original",
                IdempotencyKey: $"semantic-{originalKind}", SecurityPin: Pin,
                EvidenceIds: [evidence.Id]),
            originalKind,
            default);

        // Act và Assert: Income không thể thành Expense và Expense không thể thành Income.
        var error = await Assert.ThrowsAsync<AppException>(() => fixture.CorrectionHandler().Handle(
            new UpdateTransactionCommand(
                user.Id,
                transaction.Id,
                new UpdateTransactionRequest(oppositeCategory.Id, "Changed", Pin, [], "Wrong semantic")),
            default));
        Assert.Equal("invalid_category", error.Code);
        Assert.Single(fixture.Db.LedgerTransactions);
        Assert.Empty(fixture.Db.TransactionCorrections);
    }

    [Fact]
    public async Task Recurring_processor_materializes_one_occurrence_without_posting_money()
    {
        // Arrange: lịch 31/01 đã quá hạn và một lịch khác đang Paused.
        var now = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        await using var fixture = await IntegrityFixture.CreateAsync(now);
        var user = await fixture.AddUserAsync("Recurring owner");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", 500, true);
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense, "Rent");
        var due = fixture.Recurring(user.Id, wallet.Id, category.Id,
            new DateTime(2025, 1, 31, 9, 0, 0, DateTimeKind.Utc));
        due.AnchorDay = 31;
        due.AnchorMonth = 1;
        var paused = fixture.Recurring(user.Id, wallet.Id, category.Id, now.AddDays(-1));
        paused.Status = RecurringStatus.Paused;
        fixture.Db.RecurringTransactions.AddRange(due, paused);
        await fixture.Db.SaveChangesAsync();
        var processor = new RecurringProcessor(
            fixture.Db,
            new NotificationService(fixture.Db),
            fixture.Clock);

        // Act: chạy worker hai lần trên cùng trạng thái đến hạn.
        Assert.Equal(1, await processor.ProcessDueAsync(default));
        Assert.Equal(0, await processor.ProcessDueAsync(default));

        // Assert: chỉ một occurrence/notification, không có tiền hay ledger tự động phát sinh.
        var occurrence = Assert.Single(await fixture.Db.RecurringOccurrences.ToListAsync());
        Assert.Equal(due.Id, occurrence.RecurringTransactionId);
        Assert.Equal(new DateTime(2025, 1, 31, 9, 0, 0, DateTimeKind.Utc), occurrence.ScheduledFor);
        Assert.Equal(new DateTime(2025, 3, 31, 9, 0, 0, DateTimeKind.Utc), due.NextRunAt);
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.LedgerTransactions.ToListAsync());
        Assert.Equal(500, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        Assert.Single(await fixture.Db.Notifications.ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Recurring_occurrence_record_requires_pin_and_evidence_and_is_idempotent()
    {
        // Arrange: occurrence Due lấy amount/wallet/category từ định nghĩa server.
        await using var fixture = await IntegrityFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Record owner");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", 500, true);
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense, "Subscription");
        var recurring = fixture.Recurring(user.Id, wallet.Id, category.Id, fixture.Clock.UtcNow.AddDays(10));
        var occurrence = new RecurringOccurrence
        {
            RecurringTransaction = recurring,
            RecurringTransactionId = recurring.Id,
            ScheduledFor = fixture.Clock.UtcNow.AddMinutes(-5)
        };
        fixture.Db.AddRange(recurring, occurrence);
        await fixture.Db.SaveChangesAsync();
        var evidence = await fixture.AddEvidenceAsync(user.Id, "subscription.png");
        var handler = fixture.RecordOccurrenceHandler();

        // Assert: evidence trống và PIN sai đều không posting tiền.
        Assert.Equal("invalid_evidence_count", (await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new RecordRecurringOccurrenceCommand(
                user.Id, recurring.Id, occurrence.Id,
                new RecordRecurringOccurrenceRequest(Pin, [], "record-1")),
            default))).Code);
        Assert.Equal("pin_verification_failed", (await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new RecordRecurringOccurrenceCommand(
                user.Id, recurring.Id, occurrence.Id,
                new RecordRecurringOccurrenceRequest(WrongPin, [evidence.Id], "record-1")),
            default))).Code);
        Assert.Empty(fixture.Db.LedgerTransactions);

        // Act: ghi nhận hợp lệ và retry cùng payload.
        var command = new RecordRecurringOccurrenceCommand(
            user.Id, recurring.Id, occurrence.Id,
            new RecordRecurringOccurrenceRequest(Pin, [evidence.Id], "record-1"));
        var first = await handler.Handle(command, default);
        var replay = await handler.Handle(command, default);

        // Assert: một transaction, một posting và occurrence trỏ đúng lịch sử.
        Assert.Equal(first.Id, replay.Id);
        Assert.Single(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.Single(await fixture.Db.LedgerTransactions.ToListAsync());
        Assert.Equal(400, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        var storedOccurrence = await fixture.Db.RecurringOccurrences.FindAsync(occurrence.Id);
        Assert.Equal(RecurringOccurrenceStatus.Recorded, storedOccurrence!.Status);
        Assert.Equal(first.Id, storedOccurrence.RecordedTransactionId);
        Assert.NotNull(storedOccurrence.RecordedAt);
        Assert.Single(await fixture.Db.AuditLogs.ToListAsync());

        // Assert: payload xung đột không thể ghi occurrence lần hai.
        Assert.Equal("idempotency_conflict", (await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            command with
            {
                Request = new RecordRecurringOccurrenceRequest(Pin, [evidence.Id], "different-key")
            },
            default))).Code);
        Assert.Single(await fixture.Db.LedgerTransactions.ToListAsync());

        // Act và Assert: hủy mềm recurring vẫn giữ transaction đã ghi nhận.
        await new DeleteRecurringTransactionCommandHandler(fixture.Db).Handle(
            new DeleteRecurringTransactionCommand(user.Id, recurring.Id), default);
        Assert.Equal(RecurringStatus.Cancelled,
            (await fixture.Db.RecurringTransactions.FindAsync(recurring.Id))!.Status);
        Assert.NotNull(await fixture.Db.FinancialTransactions.FindAsync(first.Id));
    }

    [Fact]
    public void Recurring_schedule_preserves_monthly_and_leap_year_anchors()
    {
        // Monthly giữ anchor 31 dù tháng 2 phải kẹp về ngày cuối tháng.
        var january = new DateTime(2025, 1, 31, 8, 30, 0, DateTimeKind.Utc);
        var february = RecurringSchedule.Next(january, RecurringFrequency.Monthly, 31, 1);
        var march = RecurringSchedule.Next(february, RecurringFrequency.Monthly, 31, 1);
        Assert.Equal(new DateTime(2025, 2, 28, 8, 30, 0, DateTimeKind.Utc), february);
        Assert.Equal(new DateTime(2025, 3, 31, 8, 30, 0, DateTimeKind.Utc), march);

        // Yearly giữ anchor 29/02 để quay lại đúng ngày trong năm nhuận tiếp theo.
        var leap = new DateTime(2024, 2, 29, 8, 30, 0, DateTimeKind.Utc);
        var next = leap;
        for (var index = 0; index < 4; index++)
        {
            next = RecurringSchedule.Next(next, RecurringFrequency.Yearly, 29, 2);
        }
        Assert.Equal(new DateTime(2028, 2, 29, 8, 30, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public async Task Goal_movements_verify_pin_before_contribute_or_withdraw()
    {
        // Arrange: Goal và ví cùng chủ sở hữu, ví có 1.000.
        await using var fixture = await IntegrityFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Goal owner");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", 1_000, true);
        var movement = new GoalMovementExecutor(
            fixture.Db,
            fixture.Ledger,
            fixture.Clock,
            fixture.Security);
        var goal = await new CreateGoalCommandHandler(fixture.Db, fixture.Security, movement, fixture.Clock).Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Laptop", 2_000, SecurityPin: Pin)), default);
        var contribute = new ContributeGoalCommandHandler(movement);
        var withdraw = new WithdrawGoalCommandHandler(movement);

        Assert.Equal("pin_verification_failed", (await Assert.ThrowsAsync<AppException>(() => contribute.Handle(
            new ContributeGoalCommand(user.Id, goal.Id,
                new GoalMovementRequest(wallet.Id, 200, "goal-contribute", WrongPin)),
            default))).Code);
        Assert.Empty(fixture.Db.LedgerTransactions);

        await contribute.Handle(
            new ContributeGoalCommand(user.Id, goal.Id,
                new GoalMovementRequest(wallet.Id, 200, "goal-contribute", Pin)),
            default);
        Assert.Equal("pin_verification_failed", (await Assert.ThrowsAsync<AppException>(() => withdraw.Handle(
            new WithdrawGoalCommand(user.Id, goal.Id,
                new GoalMovementRequest(wallet.Id, 100, "goal-withdraw", WrongPin)),
            default))).Code);
        Assert.Equal(800, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);

        var result = await withdraw.Handle(
            new WithdrawGoalCommand(user.Id, goal.Id,
                new GoalMovementRequest(wallet.Id, 100, "goal-withdraw", Pin)),
            default);
        Assert.Equal(100, result.CurrentAllocatedAmount);
        Assert.Equal(900, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        Assert.Equal(2, await fixture.Db.LedgerTransactions.CountAsync());
    }

    [Fact]
    public async Task Internal_transfer_requires_pin_replays_once_and_cannot_use_correction_bypass()
    {
        // Arrange: hai ví cùng chủ và handler dùng ngưỡng test bằng 0.
        await using var fixture = await IntegrityFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Transfer owner");
        var source = await fixture.AddWalletAsync(user.Id, "Source", 1_000, true);
        var destination = await fixture.AddWalletAsync(user.Id, "Destination", 0, false);
        var handler = new TransferBetweenWalletsCommandHandler(
            fixture.Db,
            fixture.Ledger,
            fixture.Clock,
            new ZeroOperationPolicy(),
            fixture.Security);

        var wrong = new InternalTransferRequest(
            source.Id, destination.Id, 200, "Move", "internal-1", WrongPin);
        Assert.Equal("pin_verification_failed", (await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new TransferBetweenWalletsCommand(user.Id, wrong), default))).Code);
        Assert.Empty(fixture.Db.LedgerTransactions);

        var valid = wrong with { SecurityPin = Pin };
        var first = await handler.Handle(new TransferBetweenWalletsCommand(user.Id, valid), default);
        var replay = await handler.Handle(new TransferBetweenWalletsCommand(user.Id, valid), default);
        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Single(fixture.Db.LedgerTransactions);
        Assert.Single(fixture.Db.FinancialTransactions);
        Assert.Equal(800, source.AvailableBalance);
        Assert.Equal(200, destination.AvailableBalance);

        // PATCH cũ không thể sửa trực tiếp InternalTransfer đã hoàn tất.
        var history = await fixture.Db.FinancialTransactions.SingleAsync();
        var correction = await Assert.ThrowsAsync<ConflictException>(() => fixture.CorrectionHandler().Handle(
            new UpdateTransactionCommand(
                user.Id,
                history.Id,
                new UpdateTransactionRequest(null, "Bypass", Pin, [], "Should fail")),
            default));
        Assert.Equal("immutable_financial_transaction", correction.Code);
    }

    [Fact]
    public async Task Bank_reclassification_preserves_direction_amount_and_manual_allowance_scope()
    {
        // Arrange: imported Credit có amount cố định và hai loại category.
        await using var fixture = await IntegrityFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Bank owner");
        var income = await fixture.AddCategoryAsync(user.Id, CategoryKind.Income, "Bank income");
        var expense = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense, "Bank expense");
        var connection = new BankConnection
        {
            UserId = user.Id,
            Provider = BankProvider.SePayBankHub,
            ExternalAccountId = "bank-account",
            BankBrandName = "Test Bank",
            MaskedAccountNumber = "***1234",
            Status = BankConnectionStatus.Active
        };
        var imported = new BankTransaction
        {
            UserId = user.Id,
            BankConnection = connection,
            BankConnectionId = connection.Id,
            Provider = BankProvider.SePayBankHub,
            ExternalTransactionId = "bank-transaction",
            Direction = BankTransactionDirection.Credit,
            Amount = 777,
            TransactionDate = fixture.Clock.UtcNow,
            Content = "Incoming",
            Source = BankTransactionSource.Webhook,
            ImportedAt = fixture.Clock.UtcNow
        };
        fixture.Db.AddRange(connection, imported);
        await fixture.Db.SaveChangesAsync();
        var handler = new ReclassifyBankTransactionCommandHandler(fixture.Db);

        var result = await handler.Handle(
            new ReclassifyBankTransactionCommand(
                user.Id, imported.Id, new ReclassifyBankTransactionRequest(income.Id)),
            default);
        Assert.Equal(income.Id, result.CategoryId);
        Assert.Equal(777, imported.Amount);
        Assert.Equal(BankTransactionDirection.Credit, imported.Direction);

        Assert.Equal("invalid_category", (await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new ReclassifyBankTransactionCommand(
                user.Id, imported.Id, new ReclassifyBankTransactionRequest(expense.Id)),
            default))).Code);
        Assert.Equal(income.Id, imported.CategoryId);
        Assert.Empty(fixture.Db.TransactionCorrections);
    }

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static async Task<List<EntrySnapshot>> EntrySnapshotsAsync(
        SpendWiseDbContext db,
        Guid ledgerTransactionId) => await db.LedgerEntries.AsNoTracking()
            .Where(item => item.LedgerTransactionId == ledgerTransactionId)
            .OrderBy(item => item.Id)
            .Select(item => new EntrySnapshot(
                item.Id, item.LedgerAccountId, item.Direction, item.Amount, item.Currency, item.Memo))
            .ToListAsync();

    private static async Task AssertLedgerBalancedAsync(SpendWiseDbContext db)
    {
        var postings = await db.LedgerTransactions.AsNoTracking()
            .Include(item => item.Entries)
            .ToListAsync();
        Assert.All(postings, posting =>
        {
            Assert.Equal(
                posting.Entries.Where(item => item.Direction == LedgerEntryDirection.Debit).Sum(item => item.Amount),
                posting.Entries.Where(item => item.Direction == LedgerEntryDirection.Credit).Sum(item => item.Amount));
        });
    }

    private sealed record EntrySnapshot(
        Guid Id,
        Guid LedgerAccountId,
        LedgerEntryDirection Direction,
        decimal Amount,
        string Currency,
        string Memo);

    private sealed class IntegrityFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private IntegrityFixture(SqliteConnection connection, SpendWiseDbContext db, FixedClock clock)
        {
            this.connection = connection;
            Db = db;
            Clock = clock;
            Ledger = new LedgerService(db, clock);
            Security = new FinancialSecurityGuard(db, Pins,
                new AccountCredentialLockout(db, clock));
            Evidence = new EvidenceSupport(db);
        }

        public SpendWiseDbContext Db { get; }
        public FixedClock Clock { get; }
        public LedgerService Ledger { get; }
        public FinancialSecurityGuard Security { get; }
        public EvidenceSupport Evidence { get; }

        public static async Task<IntegrityFixture> CreateAsync(DateTime? now = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var clock = new FixedClock(now ?? new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc));
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new SpendWiseDbContext(options, clock);
            await db.Database.EnsureCreatedAsync();
            return new IntegrityFixture(connection, db, clock);
        }

        public async Task<User> AddUserAsync(string name)
        {
            var unique = Guid.NewGuid().ToString("N");
            var user = new User
            {
                FullName = name,
                Email = $"{unique}@test.local",
                NormalizedEmail = $"{unique}@test.local".ToUpperInvariant(),
                SpendId = unique,
                NormalizedSpendId = unique.ToUpperInvariant(),
                PasswordHash = "test",
                SecurityPinHash = Pins.Hash(Pin)
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            return user;
        }

        public async Task<Wallet> AddWalletAsync(
            Guid ownerUserId,
            string name,
            decimal balance,
            bool isDefault)
        {
            var wallet = WalletFactory.Create(ownerUserId, name, WalletType.Main, isDefault);
            wallet.AvailableBalance = balance;
            wallet.LedgerAccount.Balance = balance;
            Db.Wallets.Add(wallet);
            await Db.SaveChangesAsync();
            return wallet;
        }

        public async Task<Category> AddCategoryAsync(Guid ownerUserId, CategoryKind kind, string name)
        {
            var category = new Category
            {
                OwnerUserId = ownerUserId,
                Key = $"test:{Guid.NewGuid():N}",
                Kind = kind,
                Name = name,
                IsActive = true
            };
            Db.Categories.Add(category);
            await Db.SaveChangesAsync();
            return category;
        }

        public async Task<EvidenceAttachment> AddEvidenceAsync(
            Guid ownerUserId,
            string fileName,
            bool pdf = false)
        {
            var attachment = Evidence.Create(
                ownerUserId,
                pdf
                    ? new UploadEvidenceData(fileName, "application/pdf", "%PDF-1.7"u8.ToArray())
                    : new UploadEvidenceData(fileName, "image/png", Png()));
            Db.EvidenceAttachments.Add(attachment);
            await Db.SaveChangesAsync();
            return attachment;
        }

        public ManualTransactionCreator ManualCreator() => new(
            Db,
            Ledger,
            Clock,
            Security,
            Evidence);

        public UpdateTransactionCommandHandler CorrectionHandler() => new(
            Db,
            Ledger,
            Security,
            Evidence);

        public RecordRecurringOccurrenceCommandHandler RecordOccurrenceHandler() => new(
            Db,
            ManualCreator(),
            Security,
            Clock);

        public RecurringTransaction Recurring(
            Guid ownerUserId,
            Guid walletId,
            Guid categoryId,
            DateTime nextRunAt) => new()
            {
                OwnerUserId = ownerUserId,
                WalletId = walletId,
                CategoryId = categoryId,
                Kind = CategoryKind.Expense,
                Amount = 100,
                Description = "Recurring expense",
                Frequency = RecurringFrequency.Monthly,
                NextRunAt = nextRunAt,
                AnchorDay = nextRunAt.Day,
                AnchorMonth = nextRunAt.Month,
                Status = RecurringStatus.Active
            };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class ZeroOperationPolicy : IOperationPolicy
    {
        public Task EnsureFeatureEnabledAsync(string featureName, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<decimal> GetDecimalAsync(
            string key,
            decimal fallback,
            CancellationToken cancellationToken) => Task.FromResult(0m);
    }
}
