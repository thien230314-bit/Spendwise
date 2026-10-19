namespace SpendWise.Application.Identity;

// Token thiết bị chỉ nhận diện trình duyệt; token này không xác thực và không cấp quyền.
public interface IDeviceTrustTokenService
{
    string CreateToken();
    string HashToken(string rawToken);
    bool TryHashToken(string? rawToken, out string tokenHash);
}

public interface IDeviceTrustPolicy
{
    TimeSpan Lifetime { get; }
}
