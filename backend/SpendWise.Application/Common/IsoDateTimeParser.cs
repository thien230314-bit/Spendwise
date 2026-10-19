using System.Globalization;
using System.Text.RegularExpressions;

namespace SpendWise.Application.Common;

// Parse timestamp ISO-8601 có múi giờ rõ ràng để kết quả không phụ thuộc culture của máy chủ.
public static partial class IsoDateTimeParser
{
    public static bool TryParseUtc(string? value, out DateTime utcTimestamp)
    {
        utcTimestamp = default;
        if (string.IsNullOrWhiteSpace(value) || !IsoInstantPattern().IsMatch(value.Trim()))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(
                value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return false;
        }

        utcTimestamp = parsed.UtcDateTime;
        return true;
    }

    [GeneratedRegex("^\\d{4}-\\d{2}-\\d{2}T.+(?:Z|[+-]\\d{2}:\\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoInstantPattern();
}
