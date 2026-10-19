using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.UpdateRecurringTransaction;

// Sửa cấu hình chạy tương lai của giao dịch định kỳ thuộc người dùng.
public sealed class UpdateRecurringTransactionCommandHandler(
    ISpendWiseDbContext db,
    IClock clock) : IRequestHandler<UpdateRecurringTransactionCommand, RecurringDto>
{
    public async Task<RecurringDto> Handle(
        UpdateRecurringTransactionCommand command,
        CancellationToken cancellationToken)
    {
        // Chuyển chuỗi API sang enum của Domain và kiểm tra số tiền dương.
        var kindIsValid = Enum.TryParse<CategoryKind>(command.Request.Kind, true, out var categoryKind)
            && Enum.IsDefined(categoryKind);
        var frequencyIsValid = Enum.TryParse<RecurringFrequency>(command.Request.Frequency, true, out var frequency)
            && Enum.IsDefined(frequency);
        var description = InputBoundary.OptionalText(command.Request.Description, 500, "Description") ?? string.Empty;
        var title = InputBoundary.RequiredText(command.Request.Title ?? command.Request.Description, 160, "Title");
        if (!kindIsValid
            || !frequencyIsValid)
        {
            throw new AppException("invalid_recurring", "Recurring transaction values are invalid.");
        }
        InputBoundary.PositiveMoney(command.Request.Amount);
        InputBoundary.FutureDate(command.Request.NextRunAt, clock.UtcNow, "Next run time");

        // Chỉ chủ sở hữu mới được sửa định nghĩa định kỳ.
        var recurring = await db.RecurringTransactions.SingleOrDefaultAsync(
            candidate => candidate.Id == command.RecurringTransactionId
                && candidate.OwnerUserId == command.UserId,
            cancellationToken)
            ?? throw new NotFoundException("recurring_not_found", "Recurring transaction not found.");

        // Ví phải thuộc người dùng; danh mục phải đúng loại và còn được phép sử dụng.
        var validWallet = await db.Wallets.AnyAsync(
            wallet => wallet.Id == command.Request.WalletId
                && wallet.OwnerUserId == command.UserId
                && wallet.Status != WalletStatus.Closed,
            cancellationToken);
        var validCategory = await db.Categories.AnyAsync(
            category => category.Id == command.Request.CategoryId
                && category.Kind == categoryKind
                && category.IsActive
                && (category.IsSystem || category.OwnerUserId == command.UserId),
            cancellationToken);
        if (!validWallet || !validCategory)
        {
            throw new AppException("invalid_recurring_reference", "Wallet or category is invalid.");
        }

        // Chỉ cấu hình cho lần chạy sau được sửa; LastRunAt và lịch sử cũ giữ nguyên.
        recurring.WalletId = command.Request.WalletId;
        recurring.CategoryId = command.Request.CategoryId;
        recurring.Kind = categoryKind;
        recurring.Amount = command.Request.Amount;
        recurring.Title = title;
        recurring.Description = description;
        recurring.Frequency = frequency;
        recurring.NextRunAt = command.Request.NextRunAt.ToUniversalTime();
        recurring.AnchorDay = command.Request.NextRunAt.Day;
        recurring.AnchorMonth = command.Request.NextRunAt.Month;
        recurring.IsSubscription = command.Request.IsSubscription;
        await db.SaveChangesAsync(cancellationToken);

        return RecurringTransactionMappings.ToDto(recurring, clock.UtcNow);
    }
}
