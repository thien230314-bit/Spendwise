using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Notifications;
using SpendWise.Application.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record CreateV1FundRequest(string Name, string Kind, long? TargetVnd, string SecurityPin);
public sealed record V1FundDto(Guid Id, string Code, string Name, string Kind, Guid FounderUserId,
    Guid OwnerUserId, long? TargetVnd, string FinanceAvailability, DateTime? ActivityClosedAt = null);
public sealed record V1InvitationDto(Guid Id, Guid FundId, Guid InvitedUserId, string Status, DateTime ExpiresAt, Guid? InvitedByUserId = null);
public sealed record V1OwnershipOfferDto(Guid Id, Guid FundId, Guid ToUserId, bool Accepted);
public sealed record V1MemberDto(Guid UserId, int Seat, bool IsOwner, bool CanSpend, DateTime JoinedAt);
public sealed record V1ActivityDto(Guid Id, Guid? ActorUserId, Guid? SubjectUserId, string Event, string? Detail, DateTime CreatedAt, Guid? ReferenceId);

public sealed partial class V1FundService(ISpendWiseDbContext db, IClock clock, FinancialSecurityGuard security, INotificationService notifications, ILedgerService ledger,
    Microsoft.Extensions.Options.IOptions<V1ElectionPolicy>? electionOptions = null, IV1InactivityEvidence? inactivityEvidence = null, ICurrentUser? requestUser = null)
{
    public async Task<V1FundDto> CreateAsync(Guid userId, CreateV1FundRequest request, CancellationToken ct)
    {
        var name = InputBoundary.RequiredText(request.Name, 160, "Name");
        if (!Enum.TryParse<V1FundKind>(request.Kind, out var kind) || !Enum.IsDefined(kind) || kind.ToString() != request.Kind
            || (kind == V1FundKind.Fixed && (request.TargetVnd is null or <= 0 or > 9_999_999_999_999_999L))
            || (kind == V1FundKind.Free && request.TargetVnd is not null))
            throw new AppException("invalid_fund_configuration", "Use Fixed with positive integer VND target, or Free without target.");
        await RequireActorAsync(userId, ct);
        await security.VerifyAsync(userId, request.SecurityPin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([userId], token);
            await RequireCapacityAsync(userId, token);
            var allocated = await db.V1FundCodeCounters.Where(c => c.Id == 1 && c.NextValue > 0 && c.NextValue < long.MaxValue)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.NextValue, c => c.NextValue + 1), token);
            if (allocated != 1) throw new ConflictException("fund_code_unavailable", "Fund code allocator is unavailable.");
            var number = await db.V1FundCodeCounters.Where(c => c.Id == 1).Select(c => c.NextValue - 1).SingleAsync(token);
            var fund = new V1Fund(name, "SPW" + number.ToString("D6", System.Globalization.CultureInfo.InvariantCulture), kind, userId, request.TargetVnd);
            db.V1Funds.Add(fund);
            db.V1FundMemberships.Add(new V1FundMembership { FundId = fund.Id, UserId = userId, Seat = 1, JoinedAt = clock.UtcNow });
            await ActivityAsync(fund.Id, userId, "Created", token);
            await db.SaveChangesAsync(token);
            return Map(fund);
        }, ct);
    }

    public async Task<V1FundDto> GetAsync(Guid userId, Guid fundId, CancellationToken ct)
    {
        await RequireActorAsync(userId, ct);
        await RequireMemberAsync(userId, fundId, ct);
        return Map(await FundAsync(fundId, ct));
    }

    public async Task<IReadOnlyList<V1FundDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        await RequireActorAsync(userId, ct);
        var funds = await db.V1Funds.AsNoTracking().Where(f => db.V1FundMemberships.Any(m => m.FundId == f.Id
                && m.UserId == userId && m.LeftAt == null)).OrderBy(f => f.CreatedAt).ToListAsync(ct);
        return funds.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<V1MemberDto>> MembersAsync(Guid userId, Guid fundId, CancellationToken ct)
    {
        await GetAsync(userId, fundId, ct);
        var fund = await FundAsync(fundId, ct);
        return await db.V1FundMemberships.AsNoTracking().Where(m => m.FundId == fundId && m.LeftAt == null)
            .OrderBy(m => m.Seat).Select(m => new V1MemberDto(m.UserId, m.Seat, m.UserId == fund.OwnerUserId, m.CanSpend, m.JoinedAt)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<V1ActivityDto>> ActivitiesAsync(Guid userId, Guid fundId, int page, int pageSize, CancellationToken ct)
    {
        InputBoundary.Page(page, pageSize);
        if (page - 1 > int.MaxValue / pageSize) throw new AppException("invalid_pagination", "Page is too large.");
        await RequireActorAsync(userId, ct);
        if (!await db.V1FundMemberships.AsNoTracking().AnyAsync(m => m.FundId == fundId && m.UserId == userId, ct))
            throw new NotFoundException("fund_not_found", "Fund not found.");
        // Historical readers see only intervals in which they were members, never later activity.
        return await db.V1FundActivities.AsNoTracking().Where(a => a.FundId == fundId
                && db.V1FundMemberships.Any(m => m.FundId == fundId && m.UserId == userId && m.JoinedAt <= a.CreatedAt
                    && (m.LeftAt == null || a.CreatedAt <= m.LeftAt)))
            .OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new V1ActivityDto(a.Id, a.ActorUserId, a.SubjectUserId, a.Event, a.Detail, a.CreatedAt, a.ReferenceId)).ToListAsync(ct);
    }

    public async Task<V1InvitationDto> InviteAsync(Guid ownerId, Guid fundId, Guid targetId, CancellationToken ct) =>
        await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([ownerId, targetId], token);
            var fund = await LockFundAsync(fundId, token);
            await RequireMemberAsync(ownerId, fundId, token);
            var proposed = fund.Kind == V1FundKind.Fixed && fund.OwnerUserId != ownerId;
            await RequireCapacityAsync(targetId, token);
            await RequireRoomAsync(fund, token);
            if (await db.V1FundMemberships.AnyAsync(m => m.FundId == fundId && m.UserId == targetId && m.LeftAt == null, token))
                throw new ConflictException("already_member", "User is already a member.");
            if (!await db.Friendships.AsNoTracking().AnyAsync(f => f.EndedAt == null
                    && ((f.LowerUserId == ownerId && f.HigherUserId == targetId) || (f.HigherUserId == ownerId && f.LowerUserId == targetId)), token))
                throw new ForbiddenException("accepted_friend_required", "Invite an accepted friend.");
            var existing = await db.V1FundInvitations.AsNoTracking().FirstOrDefaultAsync(i => i.FundId == fundId
                && i.InvitedUserId == targetId && (i.Status == V1InvitationStatus.Pending || i.Status == V1InvitationStatus.Proposed), token);
            if (existing is not null && await InvitationDeadlineAsync(existing, token) > clock.UtcNow) return await MapInvitationAsync(existing, token);
            if (existing is not null)
                await db.V1FundInvitations.Where(i => i.Id == existing.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, V1InvitationStatus.Cancelled), token);
            var invitation = new V1FundInvitation { FundId = fundId, InvitedByUserId = ownerId, InvitedUserId = targetId,
                Status = proposed ? V1InvitationStatus.Proposed : V1InvitationStatus.Pending, ExpiresAt = clock.UtcNow.AddDays(7) };
            db.V1FundInvitations.Add(invitation);
            await ActivityAsync(fundId, ownerId, proposed ? "InvitationProposed" : "Invited", token, targetId,
                referenceId: invitation.Id);
            await db.SaveChangesAsync(token);
            return Map(invitation);
        }, ct);

    public async Task<IReadOnlyList<V1InvitationDto>> InvitationsAsync(Guid userId, CancellationToken ct, int page = 1, int pageSize = 20)
    {
        ValidatePage(page, pageSize);
        await RequireActorAsync(userId, ct);
        var rows = db.V1FundInvitations.AsNoTracking().Where(i => i.InvitedUserId == userId && i.Status == V1InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id);
        return await AvailableInvitationsAsync(rows, page, pageSize, ct);
    }

    public async Task<V1FundDto> AcceptAsync(Guid userId, Guid inviteId, CancellationToken ct) =>
        await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([userId], token);
            var invite = await db.V1FundInvitations.AsNoTracking().SingleOrDefaultAsync(i => i.Id == inviteId && i.InvitedUserId == userId, token)
                ?? throw new NotFoundException("fund_invitation_not_found", "Invitation not found.");
            var fund = await LockFundAsync(invite.FundId, token);
            invite = await db.V1FundInvitations.AsNoTracking().SingleAsync(i => i.Id == inviteId, token);
            if (invite.Status == V1InvitationStatus.Accepted)
            {
                await RequireMemberAsync(userId, fund.Id, token);
                return Map(fund);
            }
            if (invite.Status != V1InvitationStatus.Pending || await InvitationDeadlineAsync(invite, token) <= clock.UtcNow)
                throw new ConflictException("fund_invitation_unavailable", "Invitation is no longer available.");
            await RequireCapacityAsync(userId, token);
            await RequireRoomAsync(fund, token);
            if (await db.V1FundMemberships.AnyAsync(m => m.FundId == fund.Id && m.UserId == userId && m.LeftAt == null, token))
                throw new ConflictException("already_member", "User is already a member.");
            var occupied = await db.V1FundMemberships.Where(m => m.FundId == fund.Id && m.LeftAt == null).Select(m => m.Seat).ToListAsync(token);
            var seat = Enumerable.Range(1, fund.Kind == V1FundKind.Fixed ? 30 : 6).First(n => !occupied.Contains(n));
            await RequireNoActiveElectionAsync(fund.Id, token);
            db.V1FundMemberships.Add(new V1FundMembership { FundId = fund.Id, UserId = userId, Seat = seat, JoinedAt = clock.UtcNow });
            await db.V1FundInvitations.Where(i => i.Id == inviteId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, V1InvitationStatus.Accepted)
                .SetProperty(i => i.AcceptedAt, clock.UtcNow), token);
            await ActivityAsync(fund.Id, userId, "Joined", token, referenceId: invite.Id);
            await db.SaveChangesAsync(token);
            return Map(fund);
        }, ct);

    public async Task SetSpendingPermissionAsync(Guid ownerId, Guid fundId, Guid memberId, bool enabled, string pin, CancellationToken ct)
    {
        await RequireActorAsync(ownerId, ct);
        await security.VerifyAsync(ownerId, pin, ct);
        await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([ownerId], token);
            var fund = await LockFundAsync(fundId, token);
            RequireOwner(fund, ownerId);
            if (fund.Kind != V1FundKind.Fixed) throw new ConflictException("fixed_fund_required", "Free funds use withdrawal approval, not direct spending permission.");
            var changed = await db.V1FundMemberships.Where(m => m.FundId == fundId && m.UserId == memberId && m.LeftAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.CanSpend, enabled), token);
            if (changed != 1) throw new NotFoundException("fund_member_not_found", "Active member not found.");
            await ActivityAsync(fundId, ownerId, "SpendingPermissionChanged", token, memberId, enabled ? "ON" : "OFF");
            await db.SaveChangesAsync(token);
            return true;
        }, ct);
    }

    public async Task<V1OwnershipOfferDto> OfferOwnershipAsync(Guid ownerId, Guid fundId, Guid recipientId, string pin, CancellationToken ct)
    {
        await RequireActorAsync(ownerId, ct);
        await security.VerifyAsync(ownerId, pin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([ownerId, recipientId], token);
            var fund = await LockFundAsync(fundId, token);
            RequireOwner(fund, ownerId);
            if (ownerId == recipientId) throw new AppException("ownership_self_forbidden", "Choose another member.");
            await RequireMemberAsync(recipientId, fundId, token);
            var existing = await db.V1OwnershipOffers.AsNoTracking().SingleOrDefaultAsync(o => o.FundId == fundId
                && o.FromUserId == ownerId && o.ToUserId == recipientId && o.ExpectedOwnershipRevision == fund.OwnershipRevision
                && o.AcceptedAt == null, token);
            if (existing is not null) return new V1OwnershipOfferDto(existing.Id, fundId, recipientId, false);
            var offer = new V1OwnershipOffer { FundId = fundId, FromUserId = ownerId, ToUserId = recipientId,
                ExpectedOwnershipRevision = fund.OwnershipRevision };
            db.V1OwnershipOffers.Add(offer);
            await ActivityAsync(fundId, ownerId, "OwnershipOffered", token, recipientId, referenceId: offer.Id);
            await db.SaveChangesAsync(token);
            return new V1OwnershipOfferDto(offer.Id, fundId, recipientId, false);
        }, ct);
    }

    public async Task AcceptOwnershipAsync(Guid userId, Guid offerId, string pin, CancellationToken ct)
    {
        await RequireActorAsync(userId, ct);
        await security.VerifyAsync(userId, pin, ct);
        await db.ExecuteInTransactionAsync(async token =>
        {
            var offer = await db.V1OwnershipOffers.AsNoTracking().SingleOrDefaultAsync(o => o.Id == offerId && o.ToUserId == userId, token)
                ?? throw new NotFoundException("ownership_offer_not_found", "Ownership offer not found.");
            await LockUsersAsync([userId], token);
            var fund = await LockFundAsync(offer.FundId, token);
            offer = await db.V1OwnershipOffers.AsNoTracking().SingleAsync(o => o.Id == offerId, token);
            if (offer.AcceptedAt is not null)
            {
                if (fund.OwnerUserId != userId) throw new ConflictException("ownership_offer_unavailable", "Ownership has changed again.");
                return true;
            }
            RequireOwner(fund, offer.FromUserId);
            await RequireNoActiveElectionAsync(fund.Id, token);
            if (offer.ExpectedOwnershipRevision != fund.OwnershipRevision)
                throw new ConflictException("ownership_offer_unavailable", "Ownership offer belongs to an earlier tenure.");
            await RequireMemberAsync(userId, fund.Id, token);
            await db.V1Funds.Where(f => f.Id == fund.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.OwnerUserId, userId)
                .SetProperty(f => f.OwnershipRevision, f => f.OwnershipRevision + 1), token);
            await db.V1OwnershipOffers.Where(o => o.Id == offerId).ExecuteUpdateAsync(s => s.SetProperty(o => o.AcceptedAt, clock.UtcNow), token);
            await ActivityAsync(fund.Id, userId, "OwnershipAccepted", token, offer.FromUserId, referenceId: offer.Id);
            await db.SaveChangesAsync(token);
            return true;
        }, ct);
    }

    public async Task<IReadOnlyList<V1OwnershipOfferDto>> OwnershipOffersAsync(Guid userId, CancellationToken ct)
    {
        await RequireActorAsync(userId, ct);
        return await db.V1OwnershipOffers.AsNoTracking().Where(o => o.ToUserId == userId && o.AcceptedAt == null
                && db.V1Funds.Any(f => f.Id == o.FundId && f.DissolvedAt == null && f.OwnerUserId == o.FromUserId
                    && f.OwnershipRevision == o.ExpectedOwnershipRevision)
                && db.V1FundMemberships.Any(m => m.FundId == o.FundId && m.UserId == userId && m.LeftAt == null))
            .OrderBy(o => o.CreatedAt).Select(o => new V1OwnershipOfferDto(o.Id, o.FundId, o.ToUserId, false)).ToListAsync(ct);
    }

    private async Task RequireActorAsync(Guid userId, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.SystemRole == SystemRole.User
                && u.Status == UserStatus.Active && u.EmailVerifiedAt != null && !u.MustChangePassword, ct))
            throw new ForbiddenException("active_user_required", "An active verified User is required.");
    }

    private async Task LockUsersAsync(IEnumerable<Guid> users, CancellationToken ct)
    {
        await GroupAdmission.LockUsersAsync(db, users, ct);
        foreach (var id in users.Distinct().Order())
        {
            await RequireActorAsync(id, ct);
        }
        if (requestUser is not null)
        {
            if (requestUser.UserId is not { } actor || !users.Contains(actor)) throw new ForbiddenException("active_user_session_required", "Request actor is not admitted.");
            await AdmitActorSessionAsync(actor, ct);
        }
    }

    private async Task AdmitActorSessionAsync(Guid actor, CancellationToken ct)
    {
        if (requestUser is null) return;
        if (requestUser.UserId != actor || !Guid.TryParse(requestUser.SessionId, out var session)
            || await db.UserSessions.Where(s => s.Id == session && s.UserId == actor && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, s => s.RevokedAt), ct) != 1)
            throw new ForbiddenException("active_user_session_required", "Request session is unavailable.");
    }

    private async Task<V1Fund> LockFundAsync(Guid fundId, CancellationToken ct)
    {
        if (await db.V1Funds.Where(f => f.Id == fundId && f.DissolvedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.UpdatedAt, f => f.UpdatedAt), ct) != 1)
            throw new NotFoundException("fund_not_found", "Active fund not found.");
        return await FundAsync(fundId, ct);
    }

    private Task<V1Fund> FundAsync(Guid id, CancellationToken ct) => db.V1Funds.AsNoTracking().SingleAsync(f => f.Id == id, ct);

    private async Task RequireMemberAsync(Guid userId, Guid fundId, CancellationToken ct)
    {
        if (!await db.V1FundMemberships.AsNoTracking().AnyAsync(m => m.FundId == fundId && m.UserId == userId && m.LeftAt == null, ct))
            throw new NotFoundException("fund_not_found", "Fund not found.");
    }

    private static void RequireOwner(V1Fund fund, Guid actor)
    {
        if (fund.OwnerUserId != actor) throw new ForbiddenException("fund_owner_required", "Current owner is required.");
    }

    private Task RequireCapacityAsync(Guid userId, CancellationToken ct) => GroupAdmission.RequireCapacityAsync(db, userId, ct);

    private async Task RequireRoomAsync(V1Fund fund, CancellationToken ct)
    {
        if (await db.V1FundMemberships.CountAsync(m => m.FundId == fund.Id && m.LeftAt == null, ct) >= (fund.Kind == V1FundKind.Fixed ? 30 : 6))
            throw new ConflictException("fund_member_limit", "Fund has reached its member limit.");
    }

    private async Task ActivityAsync(Guid fundId, Guid? actor, string kind, CancellationToken ct,
        Guid? subject = null, string? detail = null, Guid? referenceId = null, IEnumerable<Guid>? audience = null)
    {
        var activity = new V1FundActivity { FundId = fundId, ActorUserId = actor, Event = kind, SubjectUserId = subject, Detail = detail, ReferenceId = referenceId };
        db.V1FundActivities.Add(activity);
        if (actor is { } userId) await RecordActionAsync(userId, ct);
        await db.SaveChangesAsync(ct);
        var recipients = kind switch
        {
            "Invited" => new List<Guid> { subject!.Value },
            "InvitationProposed" => await db.V1Funds.Where(f => f.Id == fundId).Select(f => f.OwnerUserId).ToListAsync(ct),
            "InvitationCancelled" => new List<Guid> { subject!.Value },
            _ => await db.V1FundMemberships.Where(m => m.FundId == fundId && m.LeftAt == null).Select(m => m.UserId).ToListAsync(ct)
        };
        await notifications.CreateManyAsync((audience ?? recipients).Where(id => id != actor).Distinct().Select(id =>
            new CreateNotificationRequest(id, "V1FundActivity", "Fund activity", kind,
                new { FundId = fundId, ActivityId = activity.Id, ActorUserId = actor, SubjectUserId = subject, Event = kind, ReferenceId = referenceId })).ToList(), ct);
    }

    private static V1FundDto Map(V1Fund f) => new(f.Id, f.Code, f.Name, f.Kind.ToString(), f.FounderUserId, f.OwnerUserId, f.TargetVnd, f.ActivityClosedAt is null ? "CONTRIBUTIONS_ONLY" : "ACTIVITY_CLOSED", f.ActivityClosedAt);
    private static V1InvitationDto Map(V1FundInvitation i) => new(i.Id, i.FundId, i.InvitedUserId, i.Status.ToString(), i.ExpiresAt, i.InvitedByUserId);
}
