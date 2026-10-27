using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Application.Notifications;
using SpendWise.Application.Social;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Social;

namespace SpendWise.Application.Features.Friends;

public sealed record SearchFriendContactQuery(Guid UserId, string Contact, string? Kind = null) : IRequest<FriendContactDto>;
public sealed record DiscoverUsersQuery(Guid UserId, string Query, int Page = 1, int PageSize = 20) : IRequest<PagedResult<UserDiscoveryDto>>;
public sealed record GetFriendsQuery(Guid UserId) : IRequest<IReadOnlyList<FriendDto>>;
public sealed record GetIncomingFriendRequestsQuery(Guid UserId) : IRequest<IReadOnlyList<FriendRequestDto>>;
public sealed record GetOutgoingFriendRequestsQuery(Guid UserId) : IRequest<IReadOnlyList<FriendRequestDto>>;
public sealed record SendFriendRequestCommand(Guid UserId, SendFriendRequest Request) : IRequest<FriendRequestDto>;
public sealed record CancelFriendRequestCommand(Guid UserId, Guid RequestId) : IRequest<FriendRequestDto>;
public sealed record AcceptFriendRequestCommand(Guid UserId, Guid RequestId) : IRequest<FriendDto>;
public sealed record RejectFriendRequestCommand(Guid UserId, Guid RequestId) : IRequest<FriendRequestDto>;
public sealed record UnfriendCommand(Guid UserId, Guid FriendUserId) : IRequest;
public sealed record UpdateFriendPreferenceCommand(Guid UserId, Guid FriendUserId, UpdateFriendPreferenceRequest Request) : IRequest<FriendDto>;

internal static class FriendFeatureSupport
{
    public static (Guid Lower, Guid Higher) Pair(Guid first, Guid second) =>
        first.CompareTo(second) < 0 ? (first, second) : (second, first);

    public static async Task<string> StateAsync(
        ISpendWiseDbContext db,
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == targetUserId)
        {
            return "SELF";
        }

        var pair = Pair(currentUserId, targetUserId);
        if (await db.Friendships.AsNoTracking().AnyAsync(
            item => item.LowerUserId == pair.Lower
                && item.HigherUserId == pair.Higher
                && item.EndedAt == null,
            cancellationToken))
        {
            return "FRIENDS";
        }

        var pending = await db.FriendRequests.AsNoTracking().SingleOrDefaultAsync(
            item => item.LowerUserId == pair.Lower
                && item.HigherUserId == pair.Higher
                && item.Status == FriendRequestStatus.Pending,
            cancellationToken);
        if (pending is null)
        {
            return "NONE";
        }

        return pending.RequesterUserId == currentUserId
            ? "REQUEST_SENT"
            : "REQUEST_RECEIVED";
    }

    public static Task<Friendship> ActivateFriendshipAsync(
        ISpendWiseDbContext db,
        FriendRequest request,
        DateTime now,
        CancellationToken cancellationToken) => db.ExecuteInTransactionAsync(async token =>
    {
        // All admissions lock both participants before counting, including reciprocal requests.
        foreach (var id in new[] { request.LowerUserId, request.HigherUserId }.Order())
        {
            var eligible = await db.Users.Where(u => u.Id == id && u.Status == UserStatus.Active
                    && u.SystemRole == SystemRole.User)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, u => u.Status), token);
            if (eligible != 1) throw new ForbiddenException("friend_contact_unavailable", "An active User is required.");
            if (await db.Friendships.CountAsync(f => f.EndedAt == null
                    && (f.LowerUserId == id || f.HigherUserId == id), token) >= 100)
                throw new ConflictException("friend_limit_reached", "Each User may have at most 100 friends.");
        }
        var claimed = await db.FriendRequests.Where(r => r.Id == request.Id && r.Status == FriendRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, FriendRequestStatus.Accepted)
                .SetProperty(r => r.ResolvedAt, now), token);
        if (claimed != 1) throw new ConflictException("friend_request_unavailable", "Friend request is no longer pending.");
        var friendship = await ActivateCoreAsync(db, request, now, token);
        await db.SaveChangesAsync(token);
        return friendship;
    }, cancellationToken);

    private static async Task<Friendship> ActivateCoreAsync(
        ISpendWiseDbContext db, FriendRequest request, DateTime now, CancellationToken cancellationToken)
    {
        var friendship = await db.Friendships
            .Include(item => item.Preferences)
            .SingleOrDefaultAsync(
                item => item.LowerUserId == request.LowerUserId
                    && item.HigherUserId == request.HigherUserId,
                cancellationToken);
        if (friendship is null)
        {
            friendship = new Friendship
            {
                LowerUserId = request.LowerUserId,
                HigherUserId = request.HigherUserId,
                AcceptedRequestId = request.Id,
                AcceptedAt = now,
                Preferences =
                [
                    new FriendPreference { UserId = request.LowerUserId },
                    new FriendPreference { UserId = request.HigherUserId }
                ]
            };
            db.Friendships.Add(friendship);
        }
        else
        {
            friendship.AcceptedRequestId = request.Id;
            friendship.AcceptedAt = now;
            friendship.EndedAt = null;
            if (friendship.Preferences.All(item => item.UserId != request.LowerUserId))
            {
                friendship.Preferences.Add(new FriendPreference { UserId = request.LowerUserId });
            }
            if (friendship.Preferences.All(item => item.UserId != request.HigherUserId))
            {
                friendship.Preferences.Add(new FriendPreference { UserId = request.HigherUserId });
            }
        }

        request.Status = FriendRequestStatus.Accepted;
        request.ResolvedAt = now;
        return friendship;
    }

    public static async Task<FriendDto> FriendDtoAsync(
        ISpendWiseDbContext db,
        Guid currentUserId,
        Guid friendUserId,
        CancellationToken cancellationToken)
    {
        var pair = Pair(currentUserId, friendUserId);
        var result = await (
            from friendship in db.Friendships.AsNoTracking()
            join user in db.Users.AsNoTracking() on friendUserId equals user.Id
            join preference in db.FriendPreferences.AsNoTracking()
                on new { FriendshipId = friendship.Id, UserId = currentUserId }
                equals new { preference.FriendshipId, preference.UserId }
            where friendship.LowerUserId == pair.Lower
                && friendship.HigherUserId == pair.Higher
                && friendship.EndedAt == null
            select new FriendDto(
                user.Id,
                user.FullName,
                user.SpendId,
                user.Phone,
                preference.Nickname,
                preference.IsFavorite,
                friendship.AcceptedAt,
                user.AvatarUrl))
            .SingleAsync(cancellationToken);
        var common = await CommonGroupsAsync(db, currentUserId, [friendUserId], cancellationToken);
        return result with { CommonGroups = common.GetValueOrDefault(friendUserId) ?? [] };
    }

    public static async Task<Dictionary<Guid, IReadOnlyList<FriendCommonGroupDto>>> CommonGroupsAsync(
        ISpendWiseDbContext db, Guid userId, IReadOnlyCollection<Guid> friendIds, CancellationToken ct)
    {
        var legacy = await db.GroupMembers.AsNoTracking().Where(m => friendIds.Contains(m.UserId)
                && m.Group.Status != SpendWise.Domain.Groups.GroupStatus.Closed
                && db.GroupMembers.Any(own => own.GroupId == m.GroupId && own.UserId == userId))
            .Select(m => new { m.UserId, m.GroupId, m.Group.Name }).ToListAsync(ct);
        var current = await db.V1FundMemberships.AsNoTracking().Where(m => friendIds.Contains(m.UserId) && m.LeftAt == null
                && db.V1Funds.Any(f => f.Id == m.FundId && f.DissolvedAt == null)
                && db.V1FundMemberships.Any(own => own.FundId == m.FundId && own.UserId == userId && own.LeftAt == null))
            .Join(db.V1Funds.AsNoTracking(), m => m.FundId, f => f.Id, (m, f) => new { m.UserId, f.Id, f.Name, f.Code }).ToListAsync(ct);
        return friendIds.Distinct().ToDictionary(id => id, id => (IReadOnlyList<FriendCommonGroupDto>)
            legacy.Where(m => m.UserId == id).Select(m => new FriendCommonGroupDto(m.GroupId, m.Name, "Legacy", null))
                .Concat(current.Where(m => m.UserId == id).Select(m => new FriendCommonGroupDto(m.Id, m.Name, "V1", m.Code)))
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Id).ToList());
    }

    public static FriendRequestDto RequestDto(FriendRequest request, User other) => new(
        request.Id,
        other.Id,
        other.FullName,
        other.SpendId,
        request.Message,
        request.Status.ToString(),
        request.CreatedAt,
        other.AvatarUrl);

    public static async Task<bool> ResolvePendingAsync(ISpendWiseDbContext db, FriendRequest request,
        FriendRequestStatus outcome, DateTime now, CancellationToken ct)
    {
        var changed = await db.FriendRequests.Where(r => r.Id == request.Id && r.Status == FriendRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, outcome).SetProperty(r => r.ResolvedAt, now)
                .SetProperty(r => r.UpdatedAt, now), ct);
        if (changed != 1 && !await db.FriendRequests.AsNoTracking().AnyAsync(r => r.Id == request.Id && r.Status == outcome, ct))
            throw new ConflictException("friend_request_unavailable", "Friend request is no longer pending.");
        request.Status = outcome;
        request.ResolvedAt = now;
        return changed == 1;
    }
}

public sealed class SearchFriendContactQueryHandler(ISpendWiseDbContext db)
    : IRequestHandler<SearchFriendContactQuery, FriendContactDto>
{
    public async Task<FriendContactDto> Handle(SearchFriendContactQuery query, CancellationToken cancellationToken)
    {
        var contact = InputBoundary.RequiredText(query.Contact, 256, "Contact");
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == query.UserId && u.Status == UserStatus.Active
            && u.SystemRole == SystemRole.User, cancellationToken))
            throw new ForbiddenException("friend_contact_unavailable", "An active User is required.");
        if (query.Kind is not (null or "auto" or "email" or "phone" or "spendId"))
            throw new AppException("invalid_contact_kind", "Use auto, email, phone or spendId.");
        var isPhone = InputBoundary.TryNormalizePhone(contact, out var phone);
        var normalizedEmail = InputBoundary.IsValidEmail(contact) ? contact.ToUpperInvariant() : null;
        var kind = query.Kind is null or "auto" ? isPhone ? "phone" : normalizedEmail is not null ? "email" : "spendId" : query.Kind;
        var spendId = contact.ToUpperInvariant();

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
            item => (kind == "phone" ? isPhone && item.Phone == phone
                : kind == "email" ? normalizedEmail != null && item.NormalizedEmail == normalizedEmail : item.NormalizedSpendId == spendId)
                && item.Status == UserStatus.Active
                && item.SystemRole == SystemRole.User,
            cancellationToken)
            ?? throw new NotFoundException("contact_not_found", "Eligible contact not found.");
        if (user.Id == query.UserId)
        {
            throw new AppException("friend_self_forbidden", "You cannot add yourself as a friend.");
        }

        var state = await FriendFeatureSupport.StateAsync(db, query.UserId, user.Id, cancellationToken);
        return new FriendContactDto(user.Id, user.FullName, user.SpendId, kind == "spendId" && state != "FRIENDS" ? null : user.Phone, state, user.AvatarUrl);
    }
}

public sealed class GetFriendsQueryHandler(ISpendWiseDbContext db)
    : IRequestHandler<GetFriendsQuery, IReadOnlyList<FriendDto>>
{
    public async Task<IReadOnlyList<FriendDto>> Handle(GetFriendsQuery query, CancellationToken cancellationToken)
    {
        var friendships = await db.Friendships.AsNoTracking()
            .Where(item => item.EndedAt == null
                && (item.LowerUserId == query.UserId || item.HigherUserId == query.UserId))
            .Select(item => new { item.Id, item.AcceptedAt, FriendUserId = item.LowerUserId == query.UserId ? item.HigherUserId : item.LowerUserId })
            .ToListAsync(cancellationToken);
        var ids = friendships.Select(item => item.FriendUserId).ToList();
        var users = await db.Users.AsNoTracking().Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var preferences = await db.FriendPreferences.AsNoTracking()
            .Where(item => item.UserId == query.UserId && friendships.Select(friendship => friendship.Id).Contains(item.FriendshipId))
            .ToDictionaryAsync(item => item.FriendshipId, cancellationToken);
        var common = await FriendFeatureSupport.CommonGroupsAsync(db, query.UserId, ids, cancellationToken);

        return friendships
            .Where(item => users.ContainsKey(item.FriendUserId))
            .Select(item =>
            {
                var user = users[item.FriendUserId];
                preferences.TryGetValue(item.Id, out var preference);
                return new FriendDto(user.Id, user.FullName, user.SpendId, user.Phone,
                    preference?.Nickname, preference?.IsFavorite ?? false, item.AcceptedAt, user.AvatarUrl) { CommonGroups = common.GetValueOrDefault(user.Id) ?? [] };
            })
            .OrderByDescending(item => item.IsFavorite)
            .ThenBy(item => item.Nickname ?? item.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.UserId)
            .ToList();
    }
}

public sealed class GetIncomingFriendRequestsQueryHandler(ISpendWiseDbContext db)
    : IRequestHandler<GetIncomingFriendRequestsQuery, IReadOnlyList<FriendRequestDto>>
{
    public async Task<IReadOnlyList<FriendRequestDto>> Handle(GetIncomingFriendRequestsQuery query, CancellationToken cancellationToken) =>
        await db.FriendRequests.AsNoTracking()
            .Where(item => item.AddresseeUserId == query.UserId && item.Status == FriendRequestStatus.Pending)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new FriendRequestDto(item.Id, item.RequesterUserId, item.RequesterUser.FullName,
                item.RequesterUser.SpendId, item.Message, item.Status.ToString(), item.CreatedAt, item.RequesterUser.AvatarUrl))
            .ToListAsync(cancellationToken);
}

public sealed class GetOutgoingFriendRequestsQueryHandler(ISpendWiseDbContext db)
    : IRequestHandler<GetOutgoingFriendRequestsQuery, IReadOnlyList<FriendRequestDto>>
{
    public async Task<IReadOnlyList<FriendRequestDto>> Handle(GetOutgoingFriendRequestsQuery query, CancellationToken cancellationToken) =>
        await db.FriendRequests.AsNoTracking()
            .Where(item => item.RequesterUserId == query.UserId && item.Status == FriendRequestStatus.Pending)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new FriendRequestDto(item.Id, item.AddresseeUserId, item.AddresseeUser.FullName,
                item.AddresseeUser.SpendId, item.Message, item.Status.ToString(), item.CreatedAt, item.AddresseeUser.AvatarUrl))
            .ToListAsync(cancellationToken);
}

public sealed class DiscoverUsersQueryHandler(ISpendWiseDbContext db)
    : IRequestHandler<DiscoverUsersQuery, PagedResult<UserDiscoveryDto>>
{
    public async Task<PagedResult<UserDiscoveryDto>> Handle(DiscoverUsersQuery query, CancellationToken cancellationToken)
    {
        var rawQuery = query.Query?.Trim();
        if (string.IsNullOrWhiteSpace(rawQuery) || rawQuery.Length < 2 || rawQuery.Length > 80)
        {
            throw new AppException("invalid_input", "Search query must be between 2 and 80 characters.");
        }

        if (query.Page < 1 || query.Page > 100_000 || query.PageSize < 1 || query.PageSize > 50)
        {
            throw new AppException("invalid_pagination", "Page must be between 1 and 100,000 and pageSize must be between 1 and 50.");
        }

        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == query.UserId && u.Status == UserStatus.Active
            && u.SystemRole == SystemRole.User, cancellationToken))
        {
            throw new ForbiddenException("user_unavailable", "An active User is required.");
        }

        var baseQuery = db.Users.AsNoTracking()
            .Where(u => u.Id != query.UserId
                && u.Status == UserStatus.Active
                && u.SystemRole == SystemRole.User
                && u.FullName.Contains(rawQuery));

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var pagedUsers = await baseQuery
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.AvatarUrl,
                u.SpendId
            })
            .ToListAsync(cancellationToken);

        var targetIds = pagedUsers.Select(u => u.Id).ToList();
        HashSet<Guid> friendSet = [];
        HashSet<Guid> sentSet = [];
        HashSet<Guid> receivedSet = [];

        if (targetIds.Count > 0)
        {
            var friendships = await db.Friendships.AsNoTracking()
                .Where(f => f.EndedAt == null && (
                    (f.LowerUserId == query.UserId && targetIds.Contains(f.HigherUserId)) ||
                    (f.HigherUserId == query.UserId && targetIds.Contains(f.LowerUserId))))
                .Select(f => f.LowerUserId == query.UserId ? f.HigherUserId : f.LowerUserId)
                .ToListAsync(cancellationToken);
            friendSet = friendships.ToHashSet();

            var pendingRequests = await db.FriendRequests.AsNoTracking()
                .Where(r => r.Status == FriendRequestStatus.Pending && (
                    (r.RequesterUserId == query.UserId && targetIds.Contains(r.AddresseeUserId)) ||
                    (r.AddresseeUserId == query.UserId && targetIds.Contains(r.RequesterUserId))))
                .Select(r => new { r.RequesterUserId, r.AddresseeUserId })
                .ToListAsync(cancellationToken);

            sentSet = pendingRequests.Where(r => r.RequesterUserId == query.UserId).Select(r => r.AddresseeUserId).ToHashSet();
            receivedSet = pendingRequests.Where(r => r.AddresseeUserId == query.UserId).Select(r => r.RequesterUserId).ToHashSet();
        }

        var items = pagedUsers.Select(u =>
        {
            var state = friendSet.Contains(u.Id) ? "FRIENDS"
                : sentSet.Contains(u.Id) ? "REQUEST_SENT"
                : receivedSet.Contains(u.Id) ? "REQUEST_RECEIVED"
                : "NONE";

            return new UserDiscoveryDto(
                u.Id,
                u.FullName,
                u.AvatarUrl,
                u.SpendId,
                state);
        }).ToList();

        return new PagedResult<UserDiscoveryDto>(items, query.Page, query.PageSize, totalCount);
    }
}

public sealed class SendFriendRequestCommandHandler(
    ISpendWiseDbContext db,
    IClock clock,
    INotificationService notifications,
    SearchFriendContactQueryHandler search)
    : IRequestHandler<SendFriendRequestCommand, FriendRequestDto>
{
    public async Task<FriendRequestDto> Handle(SendFriendRequestCommand command, CancellationToken cancellationToken)
    {
        var message = InputBoundary.OptionalText(command.Request.Message, 300, "Message");
        var contact = await search.Handle(new SearchFriendContactQuery(command.UserId, command.Request.Contact, command.Request.ContactKind), cancellationToken);
        var pair = FriendFeatureSupport.Pair(command.UserId, contact.UserId);
        if (await db.Friendships.AnyAsync(item => item.LowerUserId == pair.Lower
                && item.HigherUserId == pair.Higher && item.EndedAt == null, cancellationToken))
        {
            throw new ConflictException("already_friends", "Users are already friends.");
        }

        var pending = await db.FriendRequests
            .Include(item => item.RequesterUser)
            .Include(item => item.AddresseeUser)
            .SingleOrDefaultAsync(item => item.LowerUserId == pair.Lower
                && item.HigherUserId == pair.Higher
                && item.Status == FriendRequestStatus.Pending, cancellationToken);
        if (pending?.RequesterUserId == command.UserId)
        {
            return FriendFeatureSupport.RequestDto(pending, pending.AddresseeUser);
        }

        if (pending is not null)
        {
            await FriendFeatureSupport.ActivateFriendshipAsync(db, pending, clock.UtcNow, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await notifications.CreateAsync(pending.RequesterUserId, "FriendAccepted", "Friend request accepted",
                $"{pending.AddresseeUser.FullName} accepted your friend request.", new { pending.Id }, cancellationToken);
            return FriendFeatureSupport.RequestDto(pending, pending.RequesterUser);
        }

        var target = await db.Users.SingleAsync(item => item.Id == contact.UserId, cancellationToken);
        var request = new FriendRequest
        {
            RequesterUserId = command.UserId,
            AddresseeUserId = target.Id,
            LowerUserId = pair.Lower,
            HigherUserId = pair.Higher,
            Message = message
        };
        db.FriendRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateAsync(target.Id, "FriendRequest", "Friend request",
            "You received a friend request.", new { request.Id, request.RequesterUserId }, cancellationToken);
        return FriendFeatureSupport.RequestDto(request, target);
    }
}

public sealed class AcceptFriendRequestCommandHandler(ISpendWiseDbContext db, IClock clock, INotificationService notifications)
    : IRequestHandler<AcceptFriendRequestCommand, FriendDto>
{
    public async Task<FriendDto> Handle(AcceptFriendRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await db.FriendRequests.Include(item => item.RequesterUser)
            .SingleOrDefaultAsync(item => item.Id == command.RequestId && item.AddresseeUserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("friend_request_not_found", "Friend request not found.");
        if (request.Status != FriendRequestStatus.Pending)
        {
            throw new ConflictException("friend_request_unavailable", "Friend request is no longer pending.");
        }

        await FriendFeatureSupport.ActivateFriendshipAsync(db, request, clock.UtcNow, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateAsync(request.RequesterUserId, "FriendAccepted", "Friend request accepted",
            "Your friend request was accepted.", new { request.Id }, cancellationToken);
        return await FriendFeatureSupport.FriendDtoAsync(db, command.UserId, request.RequesterUserId, cancellationToken);
    }
}

public sealed class RejectFriendRequestCommandHandler(ISpendWiseDbContext db, IClock clock, INotificationService notifications)
    : IRequestHandler<RejectFriendRequestCommand, FriendRequestDto>
{
    public async Task<FriendRequestDto> Handle(RejectFriendRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await db.FriendRequests.AsNoTracking().Include(item => item.RequesterUser)
            .SingleOrDefaultAsync(item => item.Id == command.RequestId && item.AddresseeUserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("friend_request_not_found", "Friend request not found.");
        if (request.Status == FriendRequestStatus.Pending)
        {
            if (await FriendFeatureSupport.ResolvePendingAsync(db, request, FriendRequestStatus.Rejected, clock.UtcNow, cancellationToken))
                await notifications.CreateAsync(request.RequesterUserId, "FriendRejected", "Friend request declined",
                    "Your friend request was declined.", new { request.Id }, cancellationToken);
        }
        else if (request.Status != FriendRequestStatus.Rejected)
        {
            throw new ConflictException("friend_request_unavailable", "Friend request cannot be rejected.");
        }
        return FriendFeatureSupport.RequestDto(request, request.RequesterUser);
    }
}

public sealed class CancelFriendRequestCommandHandler(ISpendWiseDbContext db, IClock clock)
    : IRequestHandler<CancelFriendRequestCommand, FriendRequestDto>
{
    public async Task<FriendRequestDto> Handle(CancelFriendRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await db.FriendRequests.AsNoTracking().Include(item => item.AddresseeUser)
            .SingleOrDefaultAsync(item => item.Id == command.RequestId && item.RequesterUserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("friend_request_not_found", "Friend request not found.");
        if (request.Status == FriendRequestStatus.Pending)
        {
            await FriendFeatureSupport.ResolvePendingAsync(db, request, FriendRequestStatus.Cancelled, clock.UtcNow, cancellationToken);
        }
        else if (request.Status != FriendRequestStatus.Cancelled)
        {
            throw new ConflictException("friend_request_unavailable", "Friend request cannot be cancelled.");
        }
        return FriendFeatureSupport.RequestDto(request, request.AddresseeUser);
    }
}

public sealed class UnfriendCommandHandler(ISpendWiseDbContext db, IClock clock)
    : IRequestHandler<UnfriendCommand>
{
    public async Task Handle(UnfriendCommand command, CancellationToken cancellationToken)
    {
        var pair = FriendFeatureSupport.Pair(command.UserId, command.FriendUserId);
        var friendship = await db.Friendships.SingleOrDefaultAsync(item => item.LowerUserId == pair.Lower
            && item.HigherUserId == pair.Higher && item.EndedAt == null, cancellationToken)
            ?? throw new NotFoundException("friendship_not_found", "Active friendship not found.");
        friendship.EndedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UpdateFriendPreferenceCommandHandler(ISpendWiseDbContext db)
    : IRequestHandler<UpdateFriendPreferenceCommand, FriendDto>
{
    public async Task<FriendDto> Handle(UpdateFriendPreferenceCommand command, CancellationToken cancellationToken)
    {
        var pair = FriendFeatureSupport.Pair(command.UserId, command.FriendUserId);
        var friendship = await db.Friendships.AsNoTracking().SingleOrDefaultAsync(item => item.LowerUserId == pair.Lower
            && item.HigherUserId == pair.Higher && item.EndedAt == null, cancellationToken)
            ?? throw new NotFoundException("friendship_not_found", "Active friendship not found.");
        var preference = await db.FriendPreferences.SingleAsync(item => item.FriendshipId == friendship.Id
            && item.UserId == command.UserId, cancellationToken);
        preference.Nickname = InputBoundary.OptionalText(command.Request.Nickname, 160, "Nickname");
        preference.IsFavorite = command.Request.IsFavorite;
        await db.SaveChangesAsync(cancellationToken);
        return await FriendFeatureSupport.FriendDtoAsync(db, command.UserId, command.FriendUserId, cancellationToken);
    }
}
