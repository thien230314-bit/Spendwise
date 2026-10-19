using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SpendWise.Application.AI;

namespace SpendWise.Infrastructure.AI;

// Lọc nội dung không phù hợp trước khi gọi dịch vụ AI.

public sealed partial class ProfanityFilter : IProfanityFilter
{
    private static readonly HashSet<string> MildWords = new(StringComparer.Ordinal) { "damn", "hell", "đệch", "vãi" };
    private static readonly HashSet<string> SevereWords = new(StringComparer.Ordinal) { "fuck", "shit", "đụ", "địt", "đm", "dm", "cặc", "lồn" };

    public ContentSafetyResult Evaluate(string input)
    {
        // Phân loại nội dung theo mức độ từ cấm trước khi gửi đến AI provider.
        var normalized = Normalize(input);
        var tokens = TokenRegex()
            .Matches(normalized)
            .Select(x => x.Value)
            .ToArray();
        var hasSevereContent = tokens.Any(SevereWords.Contains);
        var hasMildContent = tokens.Any(MildWords.Contains);

        if (hasSevereContent)
        {
            return new(
                normalized,
                ContentSeverity.Severe,
                ContentAction.Reject,
                string.Empty);
        }
        if (!hasMildContent)
        {
            return new(
                normalized,
                ContentSeverity.None,
                ContentAction.Allow,
                input.Trim());
        }

        var safe = input;
        foreach (var word in MildWords)
        {
            safe = Regex.Replace(
                safe,
                $@"(?iu)\b{Regex.Escape(word)}\b",
                new string('*', word.Length));
        }

        return new(normalized, ContentSeverity.Mild, ContentAction.Mask, safe.Trim());
    }

    private static string Normalize(string value)
    {
        // Chuẩn hóa văn bản để bộ lọc nội dung so khớp ổn định.
        var form = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        form = form
            .Replace('0', 'o')
            .Replace('1', 'i')
            .Replace('3', 'e')
            .Replace('4', 'a')
            .Replace('5', 's')
            .Replace('7', 't');

        return SpaceRegex().Replace(form, " ").Trim();
    }

    [GeneratedRegex(@"[\p{L}\p{M}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceRegex();
}
