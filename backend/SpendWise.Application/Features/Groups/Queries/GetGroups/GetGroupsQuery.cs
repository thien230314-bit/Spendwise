using MediatR;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.Groups.Queries.GetGroups;

// Query lấy các nhóm mà người dùng hiện tại là thành viên.

public sealed record GetGroupsQuery(Guid UserId) : IRequest<IReadOnlyList<GroupDto>>;
