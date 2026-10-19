using SpendWise.Domain.Common;
using SpendWise.Domain.Identity;

namespace SpendWise.Domain.Social;

// Lưu đầy đủ vòng đời lời mời kết bạn để thao tác từ chối hoặc hủy vẫn có lịch sử.
public enum FriendRequestStatus { Pending, Accepted, Rejected, Cancelled }

public sealed class FriendRequest : BaseEntity
{
    public Guid RequesterUserId { get; set; }
    public User RequesterUser { get; set; } = null!;
    public Guid AddresseeUserId { get; set; }
    public User AddresseeUser { get; set; } = null!;
    // Hai cột canonical giúp database chặn hai lời mời Pending theo hai chiều.
    public Guid LowerUserId { get; set; }
    public Guid HigherUserId { get; set; }
    public string? Message { get; set; }
    public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;
    public DateTime? ResolvedAt { get; set; }
}

public sealed class Friendship : BaseEntity
{
    // Cặp UserId luôn được lưu theo thứ tự để chỉ có một Friendship cho mỗi cặp.
    public Guid LowerUserId { get; set; }
    public Guid HigherUserId { get; set; }
    public Guid AcceptedRequestId { get; set; }
    public FriendRequest AcceptedRequest { get; set; } = null!;
    public DateTime AcceptedAt { get; set; }
    // EndedAt giữ lịch sử unfriend và cho phép tái kích hoạt cùng quan hệ sau này.
    public DateTime? EndedAt { get; set; }
    public List<FriendPreference> Preferences { get; set; } = [];
}

public sealed class FriendPreference : BaseEntity
{
    public Guid FriendshipId { get; set; }
    public Friendship Friendship { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    // Nickname và favorite chỉ thuộc người đặt, không chia sẻ sang người bạn.
    public string? Nickname { get; set; }
    public bool IsFavorite { get; set; }
}
