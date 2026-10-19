using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Admin;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1OwnerResponseDto(Guid FundId, DateTime? RequiredSince, DateTime? Deadline, bool Overdue, DateTime? RespondedAt, Guid? CaseId);

public sealed partial class V1FundService
{
    public async Task<V1OwnerResponseDto> OwnerResponseAsync(Guid actor, Guid fundId, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct); await RequireMemberAsync(actor, fundId, ct); var f = await FundAsync(fundId, ct);
        return new(fundId, f.ActivityClosedAt, f.ActivityClosedAt is { } since ? await V1MaintenanceGate.DeadlineAsync(db, since, TimeSpan.FromDays(7), clock.UtcNow, ct, "Groups") : null,
            await OwnerResponseOverdueAsync(f, ct), f.OwnerRespondedAt, f.OwnerResponseCaseId);
    }
    private async Task<bool> OwnerResponseOverdueAsync(V1Fund fund, CancellationToken ct) => fund.Kind == V1FundKind.Fixed
        && fund.ActivityClosedAt is { } since && fund.OwnerRespondedAt is null
        && await V1MaintenanceGate.ActiveElapsedAsync(db, since, clock.UtcNow, ct, "Groups") >= TimeSpan.FromDays(7);

    public Task<Guid> EscalateOwnerResponseAsync(Guid actor, Guid fundId, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        await LockUsersAsync([actor], token); var fund = await LockFundAsync(fundId, token); await RequireMemberAsync(actor, fundId, token);
        if (!await OwnerResponseOverdueAsync(fund, token)) throw new ConflictException("owner_response_not_overdue", "Owner response is not overdue.");
        if (actor == fund.OwnerUserId || !await db.V1FundContributions.AnyAsync(c => c.FundId == fundId && c.UserId == actor && c.AmountVnd > 0, token))
            throw new ForbiddenException("financial_interest_required", "A waiting member with a financial interest is required.");
        if (fund.OwnerResponseCaseId is { } existing) return existing;
        var ticket = new SupportCase { LinkedUserId = actor, Category = "V1FundGovernance", Subject = "Owner response overdue: " + fund.Code,
            Description = $"Fund {fund.Id}; closed activity {fund.ActivityClosedAt:O}; owner response overdue. No financial approval or transfer has occurred." };
        ticket.TicketNumber = "V1-" + ticket.Id.ToString("N"); db.SupportCases.Add(ticket);
        var name = await db.Users.Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(token);
        db.CaseHistories.Add(new CaseHistory { CaseKind = CaseKind.Support, CaseId = ticket.Id, ActorId = actor, ActorDisplayNameSnapshot = name,
            ActorRole = "User", Action = "Created", Message = "Owner response overdue; linked fund " + fund.Code, NewStatus = ticket.Status.ToString() });
        await db.SaveChangesAsync(token);
        await db.V1Funds.Where(f => f.Id == fundId).ExecuteUpdateAsync(s => s.SetProperty(f => f.OwnerResponseCaseId, (Guid?)ticket.Id), token);
        await ActivityAsync(fundId, actor, "OwnerResponseEscalated", token, referenceId: ticket.Id); return ticket.Id;
    }, ct);
}
