namespace SpendWise.Application.Common.Interfaces;

// Interface này xác nhận access token còn gắn với tài khoản và phiên đang hoạt động.
public interface IAccessSessionValidator
{
    Task<bool> IsValidAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);
}
