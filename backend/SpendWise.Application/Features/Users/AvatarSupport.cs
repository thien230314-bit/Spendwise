using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Users;

public sealed record UploadAvatarData(string? OriginalFileName, string? ContentType, byte[] Content);

public sealed class AvatarSupport(IAvatarStorage avatarStorage)
{
    public const long MaximumFileSize = 5 * 1024 * 1024; // 5 MB

    private static readonly IReadOnlyDictionary<string, string[]> Allowed =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/jpg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["image/webp"] = [".webp"]
        };

    public async Task<string> SaveAvatarAsync(Guid userId, UploadAvatarData data, CancellationToken ct)
    {
        var validatedExt = ValidateFile(data);

        return await avatarStorage.SaveAvatarAsync(userId, validatedExt, data.Content, ct);
    }

    private static string ValidateFile(UploadAvatarData data)
    {
        if (data.Content.Length == 0 || data.Content.LongLength > MaximumFileSize)
        {
            throw new AppException("invalid_avatar_size", "Avatar must be between 1 byte and 5 MB.");
        }

        var submittedName = data.OriginalFileName?.Trim() ?? string.Empty;
        var safeName = Path.GetFileName(submittedName);
        if (string.IsNullOrWhiteSpace(safeName)
            || safeName.Length > 255
            || !string.Equals(safeName, submittedName, StringComparison.Ordinal)
            || safeName.Any(char.IsControl))
        {
            throw new AppException("invalid_avatar_name", "Avatar filename is invalid.");
        }

        var extension = Path.GetExtension(safeName);
        if (!Allowed.TryGetValue(data.ContentType ?? string.Empty, out var extensions)
            || !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            || !SignatureMatches(data.ContentType ?? string.Empty, data.Content))
        {
            throw new AppException("invalid_avatar_type", "Avatar file type or content is not supported. Use JPG, PNG, or WebP.");
        }

        return extension.ToLowerInvariant();
    }

    private static bool SignatureMatches(string contentType, byte[] content) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => content.Length >= 3
                && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            "image/png" => content.Length >= 8
                && content.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => content.Length >= 12
                && content.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
}
