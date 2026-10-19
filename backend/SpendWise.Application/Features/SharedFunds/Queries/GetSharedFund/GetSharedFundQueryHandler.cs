using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Groups;

namespace SpendWise.Application.Features.SharedFunds.Queries.GetSharedFund;

// Handler lấy quỹ chung và lịch sử sau khi kiểm tra thành viên nhóm.

public sealed class GetSharedFundQueryHandler(
    ISpendWiseDbContext db,
    GroupAccess access) : IRequestHandler<GetSharedFundQuery, SharedFundDto>
{
    public async Task<SharedFundDto> Handle(
        GetSharedFundQuery query,
        CancellationToken cancellationToken)
    {
        // Kiểm tra membership trước khi trả số dư và yêu cầu đóng góp của quỹ nhóm.
        await access.RequireMemberAsync(query.UserId, query.GroupId, cancellationToken);
        var fund = await db.SharedFunds
            .AsNoTracking()
            .SingleAsync(item => item.GroupId == query.GroupId, cancellationToken);

        return GroupMappings.ToFundDto(fund);
    }
}
