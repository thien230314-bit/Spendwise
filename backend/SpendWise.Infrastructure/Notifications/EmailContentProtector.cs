using Microsoft.AspNetCore.DataProtection;

namespace SpendWise.Infrastructure.Notifications;

// Nội dung email nhạy cảm được mã hóa trước khi lưu và chỉ giải mã ngay lúc gửi.
public interface IEmailContentProtector
{
    string Protect(string value);
    string Unprotect(string value);
}

public sealed class EmailContentProtector(IDataProtectionProvider provider) : IEmailContentProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("SpendWise.EmailOutbox.v1");

    public string Protect(string value) => protector.Protect(value);

    public string Unprotect(string value) => protector.Unprotect(value);
}
