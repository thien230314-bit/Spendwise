using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.BankTransactions.Commands.ReclassifyBankTransaction;

public sealed class ReclassifyBankTransactionCommandHandler(ISpendWiseDbContext db)
    : IRequestHandler<ReclassifyBankTransactionCommand, BankTransactionDto>
{
    public async Task<BankTransactionDto> Handle(
        ReclassifyBankTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var transaction = await db.BankTransactions
            .Include(item => item.BankConnection)
            .Include(item => item.Category)
            .SingleOrDefaultAsync(item => item.Id == command.BankTransactionId
                && item.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("bank_transaction_not_found", "Bank transaction not found.");
        var expectedKind = transaction.Direction == BankTransactionDirection.Credit
            ? CategoryKind.Income
            : CategoryKind.Expense;
        var category = await db.Categories.SingleOrDefaultAsync(item => item.Id == command.Request.CategoryId
            && item.Kind == expectedKind
            && item.IsActive
            && (item.IsSystem || item.OwnerUserId == command.UserId), cancellationToken)
            ?? throw new AppException("invalid_category", "Category must preserve the bank transaction direction.");

        var previousCategoryId = transaction.CategoryId;
        transaction.CategoryId = category.Id;
        transaction.Category = category;
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = command.UserId,
            Action = "RECLASSIFY_BANK_TRANSACTION",
            TargetType = "BankTransaction",
            TargetId = transaction.Id.ToString(),
            Reason = "User changed imported bank transaction category.",
            MetadataJson = JsonSerializer.Serialize(new { previousCategoryId, categoryId = category.Id })
        });
        await db.SaveChangesAsync(cancellationToken);
        return BankingMappings.ToTransactionDto(transaction);
    }
}
