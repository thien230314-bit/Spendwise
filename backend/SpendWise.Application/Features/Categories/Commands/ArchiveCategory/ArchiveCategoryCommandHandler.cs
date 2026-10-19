using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Categories.Commands.ArchiveCategory;

// Handler ngừng sử dụng danh mục cho giao dịch mới nhưng giữ tham chiếu lịch sử.

public sealed class ArchiveCategoryCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<ArchiveCategoryCommand, CategoryDeleteResult>
{
    public async Task<CategoryDeleteResult> Handle(
        ArchiveCategoryCommand command,
        CancellationToken cancellationToken)
    {
        // Đặt danh mục cá nhân thành không hoạt động để giữ tham chiếu lịch sử.
        var category = await db.Categories.SingleOrDefaultAsync(
            candidate => candidate.Id == command.CategoryId
                && candidate.OwnerUserId == command.UserId
                && !candidate.IsSystem,
            cancellationToken)
            ?? throw new NotFoundException(
                "category_not_found",
                "Custom category not found.");

        var referenced = await db.FinancialTransactions.AnyAsync(x => x.CategoryId == category.Id, cancellationToken)
            || await db.Budgets.AnyAsync(x => x.CategoryId == category.Id, cancellationToken)
            || await db.RecurringTransactions.AnyAsync(x => x.CategoryId == category.Id, cancellationToken)
            || await db.TransactionRules.AnyAsync(x => x.TargetCategoryId == category.Id, cancellationToken)
            || await db.BankTransactions.AnyAsync(x => x.CategoryId == category.Id, cancellationToken)
            || await db.Goals.AnyAsync(x => x.CategoryId == category.Id, cancellationToken);
        if (referenced)
        {
            category.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            return new CategoryDeleteResult(category.Id, true);
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        return new CategoryDeleteResult(category.Id, false);
    }
}
