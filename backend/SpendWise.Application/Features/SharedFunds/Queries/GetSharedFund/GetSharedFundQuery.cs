using MediatR;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.SharedFunds.Queries.GetSharedFund;

// Query lấy quỹ chung và lịch sử sau khi kiểm tra thành viên nhóm.

public sealed record GetSharedFundQuery(
    Guid UserId,
    Guid GroupId) : IRequest<SharedFundDto>;
