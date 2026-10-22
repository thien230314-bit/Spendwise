namespace SpendWise.Application.Social;

public sealed record FriendContactDto(
    Guid UserId,
    string FullName,
    string SpendId,
    string? Phone,
    string State,
    string? AvatarUrl = null);

public sealed record SendFriendRequest(string Contact, string? Message = null, string? ContactKind = null);
public sealed record FriendRequestDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string SpendId,
    string? Message,
    string Status,
    DateTime CreatedAt,
    string? AvatarUrl = null);
public sealed record FriendDto(
    Guid UserId,
    string FullName,
    string SpendId,
    string? Phone,
    string? Nickname,
    bool IsFavorite,
    DateTime FriendsSince,
    string? AvatarUrl = null)
{
    public IReadOnlyList<FriendCommonGroupDto> CommonGroups { get; init; } = [];
}
public sealed record FriendCommonGroupDto(Guid Id, string Name, string Model, string? Code);
public sealed record UpdateFriendPreferenceRequest(string? Nickname, bool IsFavorite);

public sealed record UserDiscoveryDto(
    Guid UserId,
    string FullName,
    string? AvatarUrl,
    string SpendId,
    string RelationshipState);
