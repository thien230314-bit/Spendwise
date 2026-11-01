using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1MemberEquityDto(
    Guid UserId,
    string FullName,
    string Role,
    long ContributedVnd,
    long PaidExpensesVnd,
    long NetEquityVnd,
    long EligibleRefundVnd);

public sealed record V1FundEquityDto(
    Guid FundId,
    string FundName,
    string FundKind,
    long TotalBalanceVnd,
    long ActiveReservedVnd,
    long AvailableBalanceVnd,
    V1MemberEquityDto CurrentUserEquity,
    IReadOnlyList<V1MemberEquityDto> MemberEquities);

public sealed record V1FundTransactionDto(
    Guid Id,
    Guid FundId,
    string Type,
    long AmountVnd,
    string Description,
    DateTime OccurredAt,
    string? ActorName,
    Guid? RelatedRequestId);

public sealed partial class V1FundService
{
    public async Task<V1FundEquityDto> EquityAsync(Guid actor, Guid fundId, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct);
        var fund = await db.V1Funds.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fundId, ct)
            ?? throw new NotFoundException("fund_not_found", "Fund not found.");

        await RequireMemberAsync(actor, fundId, ct);

        var treasury = await db.V1FundTreasuries.AsNoTracking().SingleOrDefaultAsync(t => t.FundId == fundId, ct)
            ?? throw new NotFoundException("fund_treasury_not_found", "Fund treasury not found.");

        var treasuryAccount = await FreshAccountAsync(treasury.LedgerAccountId, ct);
        var totalBalance = (long)treasuryAccount.Balance;

        // Active reservation: Expense requests in ApprovedUnused (< 7 days) or PaymentProcessing
        var activeReserved = await db.V1FundRequests.AsNoTracking()
            .Where(r => r.FundId == fundId && r.Kind == "Expense"
                && ((r.State == "ApprovedUnused" && (r.ReviewedAt == null || r.ReviewedAt.Value.AddDays(7) > clock.UtcNow))
                    || r.State == "PaymentProcessing"))
            .SumAsync(r => r.AmountVnd, ct);

        var availableBalance = Math.Max(0, totalBalance - activeReserved);

        var membersWithUsers = await (
            from m in db.V1FundMemberships.AsNoTracking()
            join u in db.Users.AsNoTracking() on m.UserId equals u.Id
            where m.FundId == fundId && m.LeftAt == null
            orderby m.Seat
            select new { Membership = m, User = u }
        ).ToListAsync(ct);

        var memberEquities = new List<V1MemberEquityDto>();

        if (fund.Kind == V1FundKind.Free)
        {
            var equityCalc = await CalculateFreeFundEquityAsync(fund, treasury, null, ct);
            var equityDict = equityCalc.MemberEquities.ToDictionary(e => e.UserId);

            foreach (var m in membersWithUsers)
            {
                var role = m.User.Id == fund.OwnerUserId ? "Owner" : "Member";
                if (equityDict.TryGetValue(m.User.Id, out var eq))
                {
                    var eligibleRefund = equityCalc.RefundEntitlements.GetValueOrDefault(m.User.Id, 0L);
                    memberEquities.Add(new V1MemberEquityDto(
                        m.User.Id,
                        m.User.FullName,
                        role,
                        eq.TotalContributedVnd,
                        eq.TotalSharedExpenseChargedVnd,
                        eq.EquityVnd,
                        eligibleRefund
                    ));
                }
                else
                {
                    memberEquities.Add(new V1MemberEquityDto(
                        m.User.Id,
                        m.User.FullName,
                        role,
                        0,
                        0,
                        0,
                        0
                    ));
                }
            }
        }
        else // Fixed Fund
        {
            var paidDict = await ActivityPaidAsync(fundId, ct);
            var obligations = await db.V1FundObligations.AsNoTracking()
                .Where(o => o.FundId == fundId && o.Revision == treasury.Revision)
                .ToListAsync(ct);

            foreach (var m in membersWithUsers)
            {
                var role = m.User.Id == fund.OwnerUserId ? "Owner" : "Member";
                var paid = paidDict.GetValueOrDefault(m.Membership.Id, 0L);
                memberEquities.Add(new V1MemberEquityDto(
                    m.User.Id,
                    m.User.FullName,
                    role,
                    paid,
                    0,
                    paid,
                    paid
                ));
            }
        }

        var currentEquity = memberEquities.FirstOrDefault(e => e.UserId == actor)
            ?? new V1MemberEquityDto(actor, "Me", actor == fund.OwnerUserId ? "Owner" : "Member", 0, 0, 0, 0);

        return new V1FundEquityDto(
            fund.Id,
            fund.Name,
            fund.Kind.ToString(),
            totalBalance,
            activeReserved,
            availableBalance,
            currentEquity,
            memberEquities
        );
    }

    public async Task<V1FundRequestDto> GetFundRequestAsync(Guid actor, Guid requestId, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct);
        var req = await db.V1FundRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new NotFoundException("fund_request_not_found", "Fund request not found.");

        var fund = await db.V1Funds.AsNoTracking().SingleOrDefaultAsync(f => f.Id == req.FundId, ct)
            ?? throw new NotFoundException("fund_not_found", "Fund not found.");

        var isOwner = fund.OwnerUserId == actor;
        var isRequester = req.RequestedByUserId == actor;

        if (isOwner && fund.OwnershipRevision > 0 && !isRequester)
        {
            var becameOwnerAt = await GetOwnerTenureStartedAtAsync(fund.Id, actor, fund.OwnershipRevision, ct);
            if (!becameOwnerAt.HasValue || req.CreatedAt < becameOwnerAt.Value || req.OwnershipRevision < fund.OwnershipRevision)
            {
                throw new ForbiddenException();
            }
        }

        if (!isOwner && !isRequester)
        {
            throw new ForbiddenException();
        }

        return MapRequest(req);
    }

    public async Task<IReadOnlyList<V1FundTransactionDto>> FundTransactionsAsync(
        Guid actor, Guid fundId, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        await RequireActorAsync(actor, ct);
        await RequireCurrentOrPastMemberAsync(actor, fundId, ct);

        var fund = await db.V1Funds.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fundId, ct)
            ?? throw new NotFoundException("fund_not_found", "Fund not found.");
        var isOwner = fund.OwnerUserId == actor;
        DateTime? becameOwnerAt = null;
        if (isOwner && fund.OwnershipRevision > 0)
        {
            becameOwnerAt = await GetOwnerTenureStartedAtAsync(fund.Id, actor, fund.OwnershipRevision, ct);
        }

        var isCurrentMember = isOwner || await db.V1FundMemberships.AsNoTracking()
            .AnyAsync(m => m.FundId == fundId && m.UserId == actor && m.LeftAt == null, ct);

        var requests = await db.V1FundRequests.AsNoTracking()
            .Where(r => r.FundId == fundId)
            .Select(r => new { r.Id, r.Kind, r.RequestedByUserId, r.CreatedAt, r.OwnershipRevision })
            .ToDictionaryAsync(r => r.Id, ct);
        var requestIds = requests.Keys.ToList();
        var actorRequestIds = requests.Values.Where(r => r.RequestedByUserId == actor).Select(r => r.Id).ToHashSet();
        var oldTenureRequestIds = requests.Values.Where(r => r.OwnershipRevision < fund.OwnershipRevision && r.RequestedByUserId != actor).Select(r => r.Id).ToHashSet();

        var rows = await (
            from ft in db.FinancialTransactions.AsNoTracking()
            join u in db.Users.AsNoTracking() on ft.OwnerUserId equals u.Id into uGroup
            from user in uGroup.DefaultIfEmpty()
            where (ft.RelatedBusinessId == fundId || (ft.RelatedBusinessId.HasValue && requestIds.Contains(ft.RelatedBusinessId.Value)))
                  && ft.Status == FinancialTransactionStatus.Completed
                  && ((isOwner && fund.OwnershipRevision == 0)
                      || (isOwner && fund.OwnershipRevision > 0 && (!ft.RelatedBusinessId.HasValue || !oldTenureRequestIds.Contains(ft.RelatedBusinessId.Value)) && (!becameOwnerAt.HasValue || ft.OccurredAt >= becameOwnerAt.Value))
                      || db.V1FundMemberships.Any(m => m.FundId == fundId && m.UserId == actor
                          && m.JoinedAt <= ft.OccurredAt && (m.LeftAt == null || ft.OccurredAt <= m.LeftAt)))
                  && (isCurrentMember || ft.OwnerUserId == actor || (ft.RelatedBusinessId.HasValue && actorRequestIds.Contains(ft.RelatedBusinessId.Value)))
            orderby ft.OccurredAt descending, ft.Id descending
            select new
            {
                Transaction = ft,
                FullName = user != null ? user.FullName : "Hệ thống",
                RelatedRequestId = (ft.RelatedBusinessId.HasValue && requestIds.Contains(ft.RelatedBusinessId.Value)) ? ft.RelatedBusinessId : null
            }
        )
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);

        return rows.Select(r =>
        {
            var req = r.RelatedRequestId.HasValue && requests.TryGetValue(r.RelatedRequestId.Value, out var foundReq) ? foundReq : null;
            var isHistoricalBeforeOwnership = isOwner && fund.OwnershipRevision > 0 &&
                ((req != null && req.OwnershipRevision < fund.OwnershipRevision) ||
                 (becameOwnerAt.HasValue && r.Transaction.OccurredAt < becameOwnerAt.Value));
            var isPrivileged = (!isHistoricalBeforeOwnership && isOwner) || (req != null && req.RequestedByUserId == actor);

            var description = r.Transaction.Description ?? "";
            if (!isPrivileged)
            {
                if (req != null && req.Kind == "Withdrawal")
                {
                    description = "Rút vốn thành viên";
                }
                else if (isHistoricalBeforeOwnership)
                {
                    description = "Chi tiêu quỹ";
                }
            }

            var relatedRequestId = isPrivileged ? r.RelatedRequestId : null;

            return new V1FundTransactionDto(
                r.Transaction.Id,
                fundId,
                r.Transaction.Type.ToString(),
                (long)r.Transaction.Amount,
                description,
                r.Transaction.OccurredAt,
                r.FullName,
                relatedRequestId
            );
        }).ToList();
    }
}
