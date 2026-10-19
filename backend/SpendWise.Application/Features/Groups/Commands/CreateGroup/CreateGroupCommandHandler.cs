using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Groups.Commands.CreateGroup;

// Handler tạo nhóm mới và gán người dùng hiện tại làm chủ sở hữu.

public sealed class CreateGroupCommandHandler(
    ISpendWiseDbContext db,
    IClock clock,
    GroupAccess access,
    FinancialSecurityGuard security) : IRequestHandler<CreateGroupCommand, GroupDto>
{
    public async Task<GroupDto> Handle(
        CreateGroupCommand command,
        CancellationToken cancellationToken)
    {
        // Tạo nhóm, membership Owner và tài khoản ledger quỹ nhóm cùng nhau.
        var request = command.Request;
        await security.VerifyAsync(command.UserId, request.SecurityPin, cancellationToken);
        if (!Enum.TryParse<GroupType>(request.Type, true, out var type)
            || !Enum.IsDefined(type))
        {
            throw new AppException("invalid_group", "Group name or type is invalid.");
        }
        var groupName = InputBoundary.RequiredText(request.Name, 160, "Group name");
        var groupDescription = InputBoundary.OptionalText(request.Description, 1000, "Group description");
        if (!string.Equals(request.Currency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException("unsupported_currency", "Groups currently support VND only.");
        }
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await GroupAdmission.LockUsersAsync(db, [command.UserId], token);
            await GroupAdmission.RequireCapacityAsync(db, command.UserId, token);
            var eligibleOwner = await db.Users.AsNoTracking().AnyAsync(user => user.Id == command.UserId
                && user.SystemRole == SystemRole.User && user.Status == UserStatus.Active, cancellationToken);
            if (!eligibleOwner)
            {
                throw new ForbiddenException("group_owner_ineligible", "Only an active normal user can create a group.");
            }

            var group = new Group
            {
                Name = groupName,
                Description = groupDescription,
                Type = type,
                Currency = "VND"
            };
            group.Members.Add(new GroupMember
            {
                UserId = command.UserId,
                Role = GroupRole.Owner,
                JoinedAt = clock.UtcNow
            });

            var account = new LedgerAccount
            {
                Code = $"GROUP-FUND:{group.Id:N}",
                Name = $"{group.Name} Shared Fund",
                Type = LedgerAccountType.SharedFund,
                GroupId = group.Id
            };
            group.SharedFund = new SharedFund
            {
                GroupId = group.Id,
                LedgerAccount = account,
                LedgerAccountId = account.Id
            };

            db.Groups.Add(group);
            await db.SaveChangesAsync(cancellationToken);

            return await access.ToDtoAsync(group, command.UserId, cancellationToken);
        }, cancellationToken);
    }
}
