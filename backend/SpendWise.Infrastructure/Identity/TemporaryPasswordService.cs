using System.Security.Cryptography;
using SpendWise.Application.Identity;

namespace SpendWise.Infrastructure.Identity;

// Tạo mật khẩu tạm ngẫu nhiên; giá trị thô chỉ tồn tại để trả đúng một lần.
public sealed class TemporaryPasswordService : ITemporaryPasswordService
{
    public string CreateStrongPassword()
    {
        var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return $"Tmp!aA1{random}";
    }
}
