using MediatR;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.Groups.Queries.GetGroupDetails;

// Query lấy chi tiết nhóm sau khi kiểm tra tư cách thành viên.

public sealed record GetGroupDetailsQuery(Guid UserId, Guid GroupId) : IRequest<GroupDto>;
