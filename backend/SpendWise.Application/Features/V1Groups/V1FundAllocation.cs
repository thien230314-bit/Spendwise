using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1SeatAllocationDto(Guid UserId, int Seat, long RequiredVnd);
public sealed record V1ContributionAllocationDto(Guid FundId, long TargetVnd, string Basis, bool PostingEnabled,
    IReadOnlyList<V1SeatAllocationDto> Members);

public sealed partial class V1FundService
{
    public Task<V1ContributionAllocationDto> ContributionAllocationAsync(Guid userId, Guid fundId, CancellationToken ct) =>
        db.ExecuteInTransactionAsync(async token =>
        {
            await RequireActorAsync(userId, token);
            var fund = await LockFundAsync(fundId, token);
            await RequireMemberAsync(userId, fundId, token);
            if (fund.Kind != V1FundKind.Fixed || fund.TargetVnd is null)
                throw new ConflictException("fixed_fund_required", "Free funds have no fixed contribution obligation.");
            var members = await db.V1FundMemberships.AsNoTracking().Where(m => m.FundId == fundId && m.LeftAt == null)
                .OrderBy(m => m.Seat).Select(m => new { m.UserId, m.Seat }).ToListAsync(token);
            var allocation = FixedFundAllocation.Allocate(fund.TargetVnd.Value, members.Select(m => m.Seat).ToArray());
            // Preview is not a persisted obligation revision, entitlement, balance or payment result.
            return new V1ContributionAllocationDto(fundId, fund.TargetVnd.Value, "CurrentMembershipPreview", false,
                members.Select(m => new V1SeatAllocationDto(m.UserId, m.Seat, allocation[m.Seat])).ToList());
        }, ct);
}
