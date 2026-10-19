using MediatR;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.Groups.Commands.UpdateGroup;

// Handler sửa metadata nhóm sau khi kiểm tra quyền quản lý.

public sealed class UpdateGroupCommandHandler(
    ISpendWiseDbContext db,
    GroupAccess access) : IRequestHandler<UpdateGroupCommand, GroupDto>
{
    public async Task<GroupDto> Handle(
        UpdateGroupCommand command,
        CancellationToken cancellationToken)
    {
        // Yêu cầu quyền Owner rồi mới sửa tên, mô tả và quy tắc chi tiêu của nhóm.
        var groupName = InputBoundary.RequiredText(command.Request.Name, 160, "Group name");
        var groupDescription = InputBoundary.OptionalText(command.Request.Description, 1000, "Group description");
        await access.RequireOwnerAsync(command.OwnerId, command.GroupId, cancellationToken);
        await access.EnsureMutableAsync(command.GroupId, cancellationToken);
        var group = await db.Groups.FindAsync([command.GroupId], cancellationToken)
            ?? throw new NotFoundException("group_not_found", "Group not found.");

        group.Name = groupName;
        group.Description = groupDescription;
        group.MembersCanCreateExpenses = command.Request.MembersCanCreateExpenses;
        await db.SaveChangesAsync(cancellationToken);

        return await access.ToDtoAsync(group, command.OwnerId, cancellationToken);
    }
}
