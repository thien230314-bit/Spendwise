using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Goals.Commands.DeleteGoal;

// Chỉ xóa Goal rỗng chưa có lịch sử tài chính để không làm mất dấu tiền.
public sealed class DeleteGoalCommandHandler(
    ISpendWiseDbContext db,
    FinancialSecurityGuard security) : IRequestHandler<DeleteGoalCommand>
{
    public async Task Handle(DeleteGoalCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.SecurityPin, cancellationToken);
        // Xóa Goal và tài khoản dự trữ trong cùng transaction để không tạo dữ liệu mồ côi.
        await db.ExecuteInTransactionAsync(
            async token =>
            {
                // Chỉ chủ sở hữu mới được xóa mục tiêu.
                var goal = await db.Goals.SingleOrDefaultAsync(
                    candidate => candidate.Id == command.GoalId
                        && candidate.OwnerUserId == command.UserId,
                    token)
                    ?? throw new NotFoundException("goal_not_found", "Goal not found.");

                // Goal còn tiền thì phải rút hết trước khi xóa.
                if (goal.CurrentAllocatedAmount != 0)
                {
                    throw new ConflictException(
                        "goal_has_allocated_money",
                        "Withdraw all allocated money before deleting the goal.");
                }

                // Mục tiêu từng có luân chuyển tiền phải được giữ lại để bảo toàn lịch sử tài chính.
                var hasFinancialHistory = await db.GoalMovements.AnyAsync(
                    movement => movement.GoalId == goal.Id,
                    token)
                    || await db.LedgerEntries.AnyAsync(
                        entry => entry.LedgerAccountId == goal.LedgerAccountId,
                        token);
                if (hasFinancialHistory)
                {
                    throw new ConflictException(
                        "goal_has_financial_history",
                        "A goal with financial history cannot be deleted.");
                }

                // Mục tiêu rỗng chưa dùng có thể xóa cùng tài khoản ledger chưa phát sinh bút toán.
                var ledgerAccount = await db.LedgerAccounts.SingleAsync(
                    account => account.Id == goal.LedgerAccountId,
                    token);
                db.Goals.Remove(goal);
                db.LedgerAccounts.Remove(ledgerAccount);
                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);
    }
}
