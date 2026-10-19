using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1FundRequestInput(string Kind, long AmountVnd, Guid RecipientUserId, string Purpose, string IdempotencyKey, string SecurityPin);
public sealed record V1FundRequestDto(Guid Id, Guid FundId, Guid RequestedByUserId, Guid RecipientUserId, string Kind,
    long AmountVnd, string Purpose, string State, Guid? ReviewedByUserId, DateTime? ReviewedAt, string ExecutionAvailability);

public sealed partial class V1FundService
{
    public async Task<V1FundRequestDto> CreateFundRequestAsync(Guid actor, Guid fundId, V1FundRequestInput request, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(request.Purpose, 1000, "Purpose");
        var key = InputBoundary.RequiredText(request.IdempotencyKey, 100, "Idempotency key");
        if (request.Kind is not ("Expense" or "Withdrawal") || request.AmountVnd is <= 0 or > MaximumVnd)
            throw new AppException("invalid_fund_request", "Use Expense/Withdrawal and a positive integer VND amount.");
        await RequireActorAsync(actor, ct); await security.VerifyAsync(actor, request.SecurityPin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([actor, request.RecipientUserId], token);
            var fund = await LockFundAsync(fundId, token); await RequireMemberAsync(actor, fundId, token);
            var replay = await db.V1FundRequests.AsNoTracking().SingleOrDefaultAsync(x => x.FundId == fundId && x.RequestedByUserId == actor && x.IdempotencyKey == key, token);
            if (replay is not null)
            {
                if (replay.Kind != request.Kind || replay.AmountVnd != request.AmountVnd || replay.RecipientUserId != request.RecipientUserId || replay.Purpose != purpose)
                    throw new ConflictException("idempotency_conflict", "Request key belongs to different intent.");
                return MapRequest(replay);
            }
            if (fund.ActivityClosedAt is not null) throw new ConflictException("activity_closed", "Activity is closed.");
            if (request.Kind == "Withdrawal" && (fund.Kind != V1FundKind.Free || request.RecipientUserId != actor))
                throw new ForbiddenException("withdrawal_not_allowed", "Free fund members may request only their own withdrawal.");
            if (request.Kind == "Expense" && actor != fund.OwnerUserId && !await db.V1FundMemberships.AnyAsync(m => m.FundId == fundId && m.UserId == actor && m.LeftAt == null && m.CanSpend, token))
                throw new ForbiddenException("fund_spending_permission_required", "Spending permission is required.");
            await RequireNoActiveElectionAsync(fundId, token);
            var item = new V1FundRequest { FundId = fundId, RequestedByUserId = actor, RecipientUserId = request.RecipientUserId,
                Kind = request.Kind, AmountVnd = request.AmountVnd, Purpose = purpose, IdempotencyKey = key, OwnershipRevision = fund.OwnershipRevision };
            db.V1FundRequests.Add(item);
            await ActivityAsync(fundId, actor, "FundRequestCreated", token, request.RecipientUserId, referenceId: item.Id);
            return MapRequest(item);
        }, ct);
    }

    public async Task<V1FundRequestDto> ReviewFundRequestAsync(Guid actor, Guid id, bool approved, string reason, string pin, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason");
        return await ChangeFundRequestAsync(actor, id, pin, async (fund, item, token) =>
        {
            RequireOwner(fund, actor);
            var state = approved ? "ApprovedAwaitingFinanceContract" : "Rejected";
            if (item.State == state && item.ReviewedByUserId == actor && item.ReviewReason == purpose) return MapRequest(item);
            if (item.State != "AwaitingOwnerReview" || item.OwnershipRevision != fund.OwnershipRevision)
                throw new ConflictException("fund_request_unavailable", "Request changed or ownership changed.");
            // Review records intent only: no reservation/payment exists until the finance contract is approved.
            item.State = state; item.ReviewedByUserId = actor; item.ReviewedAt = clock.UtcNow; item.ReviewReason = purpose;
            await ActivityAsync(fund.Id, actor, approved ? "FundRequestReviewedFinanceBlocked" : "FundRequestRejected", token, item.RequestedByUserId, referenceId: id);
            return MapRequest(item);
        }, ct);
    }

    public Task<V1FundRequestDto> CancelFundRequestAsync(Guid actor, Guid id, string pin, CancellationToken ct) =>
        ChangeFundRequestAsync(actor, id, pin, async (fund, item, token) =>
        {
            if (actor != item.RequestedByUserId) throw new ForbiddenException("fund_request_owner_required", "Only the requester can cancel.");
            if (item.State == "Cancelled") return MapRequest(item);
            if (item.State is not ("AwaitingOwnerReview" or "ApprovedAwaitingFinanceContract")) throw new ConflictException("fund_request_unavailable", "Request is final.");
            item.State = "Cancelled"; item.CancelledAt = clock.UtcNow;
            await ActivityAsync(fund.Id, actor, "FundRequestCancelled", token, referenceId: id); return MapRequest(item);
        }, ct);

    public async Task<IReadOnlyList<V1FundRequestDto>> FundRequestsAsync(Guid actor, Guid fundId, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize); await RequireActorAsync(actor, ct); await RequireMemberAsync(actor, fundId, ct);
        var rows = await db.V1FundRequests.AsNoTracking().Where(x => x.FundId == fundId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return rows.Select(MapRequest).ToList();
    }

    private async Task<V1FundRequestDto> ChangeFundRequestAsync(Guid actor, Guid id, string pin,
        Func<V1Fund, V1FundRequest, CancellationToken, Task<V1FundRequestDto>> change, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct); await security.VerifyAsync(actor, pin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([actor], token);
            var fundId = await db.V1FundRequests.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.FundId).SingleOrDefaultAsync(token)
                ?? throw new NotFoundException("fund_request_not_found", "Request not found.");
            var fund = await LockFundAsync(fundId, token); await RequireMemberAsync(actor, fundId, token);
            var item = await db.V1FundRequests.SingleAsync(x => x.Id == id, token);
            if (db is DbContext context) await context.Entry(item).ReloadAsync(token);
            var result = await change(fund, item, token); await db.SaveChangesAsync(token); return result;
        }, ct);
    }
    private static V1FundRequestDto MapRequest(V1FundRequest x) => new(x.Id, x.FundId, x.RequestedByUserId, x.RecipientUserId,
        x.Kind, x.AmountVnd, x.Purpose, x.State, x.ReviewedByUserId, x.ReviewedAt, "FINANCE_BLOCKED");
}
