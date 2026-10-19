using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.Groups.Queries.GetGroups;

// Handler lấy các nhóm mà người dùng hiện tại là thành viên.

public sealed class GetGroupsQueryHandler(
    GroupAccess access) : IRequestHandler<GetGroupsQuery, IReadOnlyList<GroupDto>>
{
    public async Task<IReadOnlyList<GroupDto>> Handle(
        GetGroupsQuery query,
        CancellationToken cancellationToken)
    {
        // Chỉ lấy các nhóm đang hoạt động mà người dùng là thành viên.
        return await access.ProjectGroupsForUser(query.UserId)
            .ToListAsync(cancellationToken);
    }
}
