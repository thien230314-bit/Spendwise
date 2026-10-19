using System.Net.Mail;
using System.Text.RegularExpressions;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Common;

namespace SpendWise.Application.Common;

// Các quy tắc này chặn dữ liệu client sai trước khi chạm tới ràng buộc SQL Server.
public static class InputBoundary
{
    // decimal(18,2) cho phép tối đa 16 chữ số nguyên và 2 chữ số thập phân.
    public const decimal MaximumMoney = 9_999_999_999_999_999.99m;

    public static string RequiredText(string? value, int maximumLength, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new AppException(
                "invalid_input",
                $"{field} is required and limited to {maximumLength} characters.");
        }

        return normalized;
    }

    public static string? OptionalText(string? value, int maximumLength, string field)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new AppException(
                "invalid_input",
                $"{field} is limited to {maximumLength} characters.");
        }

        return normalized;
    }

    public static void PositiveMoney(decimal amount, string field = "Amount")
    {
        // Tiền phải dương, nằm trong decimal(18,2) và không có quá hai số lẻ.
        if (amount <= 0
            || amount > MaximumMoney
            || decimal.Round(amount, Money.Scale) != amount)
        {
            throw new AppException(
                "invalid_amount",
                $"{field} must be positive and use at most two decimal places.");
        }
    }

    public static void FutureDate(DateTime? value, DateTime now, string field)
    {
        if (value is not null && value.Value.ToUniversalTime() <= now)
        {
            throw new AppException(
                "invalid_date",
                $"{field} must be in the future.");
        }
    }

    public static void Page(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new AppException(
                "invalid_pagination",
                "Page must be at least 1 and pageSize must be between 1 and 100.");
        }
    }

    public static void Registration(
        string? fullName,
        string? email,
        string? phone)
    {
        RequiredText(fullName, 160, "Full name");
        _ = NormalizeEmail(email);
        _ = NormalizePhone(phone);
    }

    public static string NormalizeEmail(string? email)
    {
        var normalized = RequiredText(email, 256, "Email");
        if (!IsValidEmail(normalized))
        {
            throw new AppException("invalid_email", "Email format is invalid.");
        }

        return normalized.ToUpperInvariant();
    }

    public static string NormalizePhone(string? phone)
    {
        var normalized = RequiredText(phone, 10, "Phone");
        if (!IsVietnamesePhone(normalized))
        {
            throw new AppException(
                "invalid_phone",
                "Phone must contain exactly 10 digits and start with 0.");
        }

        return normalized;
    }

    public static bool TryNormalizePhone(string? phone, out string normalized)
    {
        normalized = phone?.Trim() ?? string.Empty;
        return IsVietnamesePhone(normalized);
    }

    public static string SecurityPin(string? pin, string field = "PIN")
    {
        // Không trim PIN để khoảng trắng không thể vô tình trở thành PIN hợp lệ.
        if (pin is null || !Regex.IsMatch(pin, "^\\d{6}$", RegexOptions.CultureInvariant))
        {
            throw new AppException(
                "invalid_pin_format",
                $"{field} must contain exactly 6 numeric digits.");
        }

        return pin;
    }

    public static bool IsValidEmail(string? email)
    {
        var normalized = email?.Trim();
        return !string.IsNullOrWhiteSpace(normalized)
            && normalized.Length <= 256
            && MailAddress.TryCreate(normalized, out var parsedEmail)
            && string.Equals(parsedEmail.Address, normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVietnamesePhone(string phone) =>
        Regex.IsMatch(phone, "^0\\d{9}$", RegexOptions.CultureInvariant);
}
