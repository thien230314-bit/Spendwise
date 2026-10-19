using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Evidence;

public sealed class EvidenceSupport(ISpendWiseDbContext db)
{
    public const int MaximumFiles = 3;
    public const long MaximumFileSize = 2 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string[]> Allowed =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/jpg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["image/webp"] = [".webp"],
            ["application/pdf"] = [".pdf"]
        };

    public EvidenceAttachment Create(Guid ownerUserId, UploadEvidenceData data)
    {
        var fileName = ValidateFile(data);
        return new EvidenceAttachment
        {
            OwnerUserId = ownerUserId,
            OriginalFileName = fileName,
            ContentType = data.ContentType.ToLowerInvariant(),
            Length = data.Content.LongLength,
            ContentHash = Convert.ToHexString(SHA256.HashData(data.Content)),
            Content = data.Content
        };
    }

    public async Task<List<EvidenceAttachment>> LoadOwnedAsync(
        Guid userId,
        IReadOnlyList<Guid>? ids,
        bool required,
        bool requireUnlinked,
        CancellationToken cancellationToken)
    {
        var distinctIds = ids?.Distinct().ToList() ?? [];
        if ((required && distinctIds.Count == 0) || distinctIds.Count > MaximumFiles)
        {
            throw new AppException(
                "invalid_evidence_count",
                $"Evidence requires {(required ? "1-3" : "0-3")} files.");
        }

        var evidence = await db.EvidenceAttachments
            .Where(item => distinctIds.Contains(item.Id)
                && item.OwnerUserId == userId)
            .ToListAsync(cancellationToken);
        if (evidence.Count != distinctIds.Count
            || (requireUnlinked && evidence.Any(item => item.FinancialTransactionId != null
                || item.TransactionCorrectionId != null)))
        {
            throw new AppException(
                "invalid_evidence",
                "Evidence is missing, owned by another user, or already linked.");
        }

        return evidence;
    }

    private static string ValidateFile(UploadEvidenceData data)
    {
        if (data.Content.Length == 0 || data.Content.LongLength > MaximumFileSize)
        {
            throw new AppException("invalid_evidence_size", "Evidence must be between 1 byte and 2 MB.");
        }

        var submittedName = data.OriginalFileName?.Trim() ?? string.Empty;
        var safeName = Path.GetFileName(submittedName);
        if (string.IsNullOrWhiteSpace(safeName)
            || safeName.Length > 255
            || !string.Equals(safeName, submittedName, StringComparison.Ordinal)
            || safeName.Any(char.IsControl))
        {
            throw new AppException("invalid_evidence_name", "Evidence filename is invalid.");
        }

        var extension = Path.GetExtension(safeName);
        if (!Allowed.TryGetValue(data.ContentType ?? string.Empty, out var extensions)
            || !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            || !SignatureMatches(data.ContentType ?? string.Empty, data.Content))
        {
            throw new AppException("invalid_evidence_type", "Evidence file type or content is not supported.");
        }

        return safeName;
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
            "application/pdf" => content.Length >= 5
                && content.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            _ => false
        };
}
