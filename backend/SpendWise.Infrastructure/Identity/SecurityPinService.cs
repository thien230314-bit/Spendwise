using System.Security.Cryptography;
using SpendWise.Application.Identity;

namespace SpendWise.Infrastructure.Identity;

// PIN dùng salted PBKDF2 riêng, không dùng plaintext hoặc cơ chế lưu trữ phía frontend.
public sealed class SecurityPinService : ISecurityPinService
{
    private const int Iterations = 310_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string pin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            pin,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return $"pin-pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string pin, string encodedHash)
    {
        var parts = encodedHash.Split('$');
        if (parts.Length != 4
            || parts[0] != "pin-pbkdf2-sha256"
            || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                pin,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
