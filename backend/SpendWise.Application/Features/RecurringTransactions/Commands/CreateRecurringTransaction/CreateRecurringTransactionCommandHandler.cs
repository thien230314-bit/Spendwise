using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.CreateRecurringTransaction;

// Handler tạo cấu hình giao dịch định kỳ mà không sửa lịch sử đã phát sinh.

public sealed class CreateRecurringTransactionCommandHandler(
    ISpendWiseDbContext db,
    IClock clock) : IRequestHandler<CreateRecurringTransactionCommand, RecurringDto>
{
    public async Task<RecurringDto> Handle(
        CreateRecurringTransactionCommand command,
        CancellationToken cancellationToken)
    {
        // Xác nhận ví, danh mục, chu kỳ và số tiền trước khi tạo lịch tương lai.
        var kindIsValid = Enum.TryParse<CategoryKind>(
            command.Request.Kind,
            true,
            out var categoryKind)
            && Enum.IsDefined(categoryKind);
        var frequencyIsValid = Enum.TryParse<RecurringFrequency>(
            command.Request.Frequency,
            true,
            out var frequency)
            && Enum.IsDefined(frequency);
        var description = InputBoundary.OptionalText(command.Request.Description, 500, "Description") ?? string.Empty;
        var title = InputBoundary.RequiredText(command.Request.Title ?? command.Request.Description, 160, "Title");
        if (!kindIsValid
            || !frequencyIsValid)
        {
            throw new AppException(
                "invalid_recurring",
                "Recurring transaction values are invalid.");
        }
        InputBoundary.PositiveMoney(command.Request.Amount);
        InputBoundary.FutureDate(command.Request.NextRunAt, clock.UtcNow, "Next run time");

        // Ví phải thuộc người dùng và chưa đóng; danh mục phải còn hoạt động.
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
            throw new AppException(
                "invalid_recurring_reference",
                "Wallet or category is invalid.");
        }

        var transaction = new RecurringTransaction
        {
            OwnerUserId = command.UserId,
            WalletId = command.Request.WalletId,
            CategoryId = command.Request.CategoryId,
            Kind = categoryKind,
            Amount = command.Request.Amount,
            Title = title,
            Description = description,
            Frequency = frequency,
            NextRunAt = command.Request.NextRunAt.ToUniversalTime(),
            AnchorDay = command.Request.NextRunAt.Day,
            AnchorMonth = command.Request.NextRunAt.Month,
            IsSubscription = command.Request.IsSubscription
        };
        db.RecurringTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        return RecurringTransactionMappings.ToDto(transaction, clock.UtcNow);
    }
}
