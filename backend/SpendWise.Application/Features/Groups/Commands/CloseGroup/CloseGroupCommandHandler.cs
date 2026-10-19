using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups.Commands.CloseGroup;

// Handler đóng nhóm theo vòng đời thay vì xóa dữ liệu và lịch sử liên quan.

public sealed class CloseGroupCommandHandler(
    ISpendWiseDbContext db,
    GroupAccess access,
    IClock clock)
{
    public async Task Handle(
        CloseGroupCommand command,
        CancellationToken cancellationToken)
    {
        // Quyền Owner và số dư quỹ hiện hành được kiểm tra trong cùng transaction.
        await db.ExecuteInTransactionAsync(async token =>
        {
            await access.RequireOwnerAsync(command.OwnerId, command.GroupId, token);
            var group = await db.Groups
                .Include(item => item.SharedFund)
                .ThenInclude(fund => fund.LedgerAccount)
                .SingleOrDefaultAsync(item => item.Id == command.GroupId, token)
                ?? throw new NotFoundException("group_not_found", "Group not found.");
            // Không khóa Group khi tiền vẫn nằm trong quỹ, nếu không thành viên sẽ mất đường truy cập quỹ.
            if (group.SharedFund.Balance != 0
                || group.SharedFund.LedgerAccount.Balance != 0)
            {
                throw new ConflictException(
                    "group_fund_not_empty",
                    "The shared fund must have zero balance before closing the group.");
            }

            group.Status = GroupStatus.Closed;

            // Chạm rowversion của tài khoản quỹ để đóng nhóm tranh chấp đúng với contribution đồng thời.
            group.SharedFund.LedgerAccount.UpdatedAt = clock.UtcNow;
            try
            {
                await db.SaveChangesAsync(token);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(
                    "concurrent_group_financial_operation",
                    "The shared fund changed concurrently. Retry closing the group.");
            }

            return true;
        }, cancellationToken);
    }
}
