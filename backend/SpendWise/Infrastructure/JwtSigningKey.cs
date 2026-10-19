using System.Text;

namespace SpendWise.API.Infrastructure;

// Kiểm tra khóa ký JWT trước khi API bắt đầu nhận request.
public static class JwtSigningKey
{
    private const int MinimumKeyBytes = 32;

    public static byte[] Validate(string value)
    {
        // HS256 yêu cầu khóa hiệu dụng tối thiểu 256 bit, tương đương 32 byte UTF-8.
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                "Jwt:Key must contain at least 256 bits (32 UTF-8 bytes). " +
                "Supply it through secure configuration.");
        }

        return bytes;
    }
}
