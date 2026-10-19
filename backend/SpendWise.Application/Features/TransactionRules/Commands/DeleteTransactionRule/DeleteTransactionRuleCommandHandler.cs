using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.TransactionRules.Commands.DeleteTransactionRule;

// Xóa cấu hình quy tắc thuộc người dùng mà không thay đổi giao dịch đã phân loại.
public sealed class DeleteTransactionRuleCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<DeleteTransactionRuleCommand>
{
    public async Task Handle(DeleteTransactionRuleCommand command, CancellationToken cancellationToken)
    {
        // Tìm quy tắc trong phạm vi sở hữu của người dùng.
        var rule = await db.TransactionRules.SingleOrDefaultAsync(
            candidate => candidate.Id == command.TransactionRuleId
                && candidate.OwnerUserId == command.UserId,
            cancellationToken)
            ?? throw new NotFoundException("transaction_rule_not_found", "Transaction rule not found.");

        // Quy tắc chỉ là cấu hình nên có thể xóa mà không ảnh hưởng giao dịch cũ.
        db.TransactionRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
    }
}
