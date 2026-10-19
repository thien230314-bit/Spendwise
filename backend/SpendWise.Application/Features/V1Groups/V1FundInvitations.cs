using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Groups;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed partial class V1FundService
{
    public async Task<IReadOnlyList<V1InvitationDto>> InvitationProposalsAsync(Guid userId, Guid fundId, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        await RequireActorAsync(userId, ct);
        var fund = await db.V1Funds.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fundId && f.DissolvedAt == null, ct)
            ?? throw new NotFoundException("fund_not_found", "Fund not found.");
        RequireOwner(fund, userId);
        return await AvailableInvitationsAsync(db.V1FundInvitations.AsNoTracking().Where(i => i.FundId == fundId
                && i.Status == V1InvitationStatus.Proposed).OrderBy(i => i.CreatedAt).ThenBy(i => i.Id), page, pageSize, ct);
    }

    public Task<V1InvitationDto> ReviewInvitationAsync(Guid ownerId, Guid inviteId, bool approved, CancellationToken ct) =>
        db.ExecuteInTransactionAsync(async token =>
        {
            var invite = await InvitationAsync(inviteId, token);
            var participants = new[] { ownerId, invite.InvitedUserId, invite.InvitedByUserId };
            await GroupAdmission.LockUsersAsync(db, participants, token);
            await RequireActorAsync(ownerId, token);
            await AdmitActorSessionAsync(ownerId, token);
            if (approved)
            {
                await RequireActorAsync(invite.InvitedUserId, token);
                await RequireActorAsync(invite.InvitedByUserId, token);
            }
            var fund = await LockFundAsync(invite.FundId, token);
            RequireOwner(fund, ownerId);
            invite = await InvitationAsync(inviteId, token);
            var outcome = approved ? V1InvitationStatus.Pending : V1InvitationStatus.Rejected;
            if (invite.Status == outcome) return await MapInvitationAsync(invite, token);
            if (fund.Kind != V1FundKind.Fixed || invite.Status != V1InvitationStatus.Proposed || await InvitationDeadlineAsync(invite, token) <= clock.UtcNow)
                throw new ConflictException("fund_invitation_unavailable", "Proposal is no longer available.");
            if (approved)
            {
                await RequireCapacityAsync(invite.InvitedUserId, token);
                await RequireRoomAsync(fund, token);
                await RequireMemberAsync(invite.InvitedByUserId, fund.Id, token);
                if (await db.V1FundMemberships.AnyAsync(m => m.FundId == fund.Id && m.UserId == invite.InvitedUserId && m.LeftAt == null, token))
                    throw new ConflictException("already_member", "User is already a member.");
                if (!await db.Friendships.AsNoTracking().AnyAsync(f => f.EndedAt == null
                    && ((f.LowerUserId == invite.InvitedByUserId && f.HigherUserId == invite.InvitedUserId)
                        || (f.HigherUserId == invite.InvitedByUserId && f.LowerUserId == invite.InvitedUserId)), token))
                    throw new ForbiddenException("accepted_friend_required", "Inviter and recipient must still be friends.");
            }
            await db.V1FundInvitations.Where(i => i.Id == inviteId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, outcome), token);
            await ActivityAsync(fund.Id, ownerId, approved ? "InvitationApproved" : "InvitationRejected", token,
                invite.InvitedUserId, referenceId: invite.Id);
            if (approved)
                await ActivityAsync(fund.Id, ownerId, "Invited", token, invite.InvitedUserId, referenceId: invite.Id);
            invite.Status = outcome;
            return await MapInvitationAsync(invite, token);
        }, ct);

    public Task<V1InvitationDto> ResolveInvitationAsync(Guid actorId, Guid inviteId, bool cancel, CancellationToken ct) =>
        db.ExecuteInTransactionAsync(async token =>
        {
            var invite = await InvitationAsync(inviteId, token);
            await GroupAdmission.LockUsersAsync(db, [actorId, invite.InvitedUserId, invite.InvitedByUserId], token);
            await RequireActorAsync(actorId, token);
            await AdmitActorSessionAsync(actorId, token);
            var fund = await LockFundAsync(invite.FundId, token);
            invite = await InvitationAsync(inviteId, token);
            if (cancel ? actorId != invite.InvitedByUserId && actorId != fund.OwnerUserId : actorId != invite.InvitedUserId)
                throw new NotFoundException("fund_invitation_not_found", "Invitation not found.");
            if (cancel) await RequireMemberAsync(actorId, fund.Id, token);
            var outcome = cancel ? V1InvitationStatus.Cancelled : V1InvitationStatus.Rejected;
            if (invite.Status == outcome) return await MapInvitationAsync(invite, token);
            if (await InvitationDeadlineAsync(invite, token) <= clock.UtcNow || (invite.Status != V1InvitationStatus.Pending
                && !(cancel && invite.Status == V1InvitationStatus.Proposed)))
                throw new ConflictException("fund_invitation_unavailable", "Invitation is no longer available.");
            var wasProposal = invite.Status == V1InvitationStatus.Proposed;
            await db.V1FundInvitations.Where(i => i.Id == inviteId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, outcome), token);
            await ActivityAsync(fund.Id, actorId, cancel ? wasProposal ? "ProposalCancelled" : "InvitationCancelled" : "InvitationDeclined",
                token, invite.InvitedUserId, referenceId: invite.Id);
            invite.Status = outcome;
            return await MapInvitationAsync(invite, token);
        }, ct);

    private async Task<V1FundInvitation> InvitationAsync(Guid id, CancellationToken ct) =>
        await db.V1FundInvitations.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new NotFoundException("fund_invitation_not_found", "Invitation not found.");

    private Task<DateTime> InvitationDeadlineAsync(V1FundInvitation invite, CancellationToken ct) =>
        Operations.V1MaintenanceGate.DeadlineAsync(db, invite.CreatedAt, invite.ExpiresAt - invite.CreatedAt, clock.UtcNow, ct, "Groups");

    private async Task<V1InvitationDto> MapInvitationAsync(V1FundInvitation invite, CancellationToken ct) =>
        Map(invite) with { ExpiresAt = await InvitationDeadlineAsync(invite, ct) };

    private async Task<IReadOnlyList<V1InvitationDto>> AvailableInvitationsAsync(IQueryable<V1FundInvitation> query, int page, int size, CancellationToken ct)
    {
        // Expiry must be evaluated after maintenance compensation, before pagination.
        var result = new List<V1InvitationDto>(); var skip = (page - 1) * size; var offset = 0;
        while (result.Count < size)
        {
            var batch = await query.Skip(offset).Take(100).ToListAsync(ct);
            if (batch.Count == 0) break;
            foreach (var invite in batch)
            {
                var dto = await MapInvitationAsync(invite, ct);
                if (dto.ExpiresAt <= clock.UtcNow) continue;
                if (skip > 0) { skip--; continue; }
                result.Add(dto); if (result.Count == size) break;
            }
            offset += batch.Count;
        }
        return result;
    }

    private static void ValidatePage(int page, int pageSize)
    {
        InputBoundary.Page(page, pageSize);
        if (page - 1 > int.MaxValue / pageSize) throw new AppException("invalid_pagination", "Page is too large.");
    }
}
