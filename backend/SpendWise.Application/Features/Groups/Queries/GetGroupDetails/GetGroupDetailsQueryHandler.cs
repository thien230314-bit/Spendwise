using MediatR;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.Groups.Queries.GetGroupDetails;

// Handler lấy chi tiết nhóm sau khi kiểm tra tư cách thành viên.

public sealed class GetGroupDetailsQueryHandler(
    ISpendWiseDbContext db,
    GroupAccess access) : IRequestHandler<GetGroupDetailsQuery, GroupDto>
{
    public async Task<GroupDto> Handle(
        GetGroupDetailsQuery query,
        CancellationToken cancellationToken)
    {
        // Kiểm tra membership trước khi trả thông tin nhóm và danh sách thành viên.
        // Thành viên mới được xem chi tiết nhóm; dữ liệu ví cá nhân không nằm trong DTO.
        await access.RequireMemberAsync(query.UserId, query.GroupId, cancellationToken);
        var group = await db.Groups.FindAsync([query.GroupId], cancellationToken)
            ?? throw new NotFoundException("group_not_found", "Group not found.");

        return await access.ToDtoAsync(group, query.UserId, cancellationToken);
    }
}
