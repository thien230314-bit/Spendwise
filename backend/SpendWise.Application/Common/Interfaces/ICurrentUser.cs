namespace SpendWise.Application.Common.Interfaces;

// Interface này đọc UserId, vai trò, ngôn ngữ và phiên từ request đã xác thực.

public interface ICurrentUser
{
    Guid? UserId { get; }
    string SystemRole { get; }
    string Locale { get; }
    string? SessionId { get; }
}
