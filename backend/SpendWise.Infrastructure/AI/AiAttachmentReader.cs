using System.Text;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AI;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using UglyToad.PdfPig;

namespace SpendWise.Infrastructure.AI;

public sealed class AiAttachmentReader(ISpendWiseDbContext db) : IAiAttachmentReader
{
    private const int MaximumExtractedBytes = 200 * 1024;

    public async Task<IReadOnlyList<AiProviderAttachment>> ReadAsync(Guid userId, IReadOnlyCollection<Guid> attachmentIds, CancellationToken cancellationToken)
    {
        if (attachmentIds.Count > 5) throw new AppException("ai_attachment_count_exceeded", "A message can include at most 5 attachments.");
        var ids = attachmentIds.Distinct().ToList();
        if (ids.Count != attachmentIds.Count) throw new AppException("ai_attachment_duplicate", "Attachment ids must be unique.");
        var attachments = await db.AiAttachments.Where(x => ids.Contains(x.Id) && x.OwnerUserId == userId).ToListAsync(cancellationToken);
        if (attachments.Count != ids.Count) throw new NotFoundException("ai_attachment_not_found", "One or more AI attachments were not found.");

        var result = new List<AiProviderAttachment>();
        foreach (var attachment in ids.Select(id => attachments.Single(x => x.Id == id)))
        {
            if (attachment.ContentType.StartsWith("image/", StringComparison.Ordinal))
                result.Add(new AiProviderAttachment(attachment.ContentType, attachment.OriginalFileName, attachment.Content, null));
            else if (attachment.ContentType == "application/pdf")
                result.Add(new AiProviderAttachment(attachment.ContentType, attachment.OriginalFileName, null, ExtractPdf(attachment.Content)));
            else
                result.Add(new AiProviderAttachment(attachment.ContentType, attachment.OriginalFileName, null, DecodeBounded(attachment.Content)));
        }
        return result;
    }

    private static string ExtractPdf(byte[] bytes)
    {
        try
        {
            using var document = PdfDocument.Open(bytes);
            var builder = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                if (Encoding.UTF8.GetByteCount(builder.ToString()) >= MaximumExtractedBytes) break;
                builder.AppendLine(page.Text);
            }
            var text = BoundText(builder.ToString());
            if (string.IsNullOrWhiteSpace(text)) throw new AppException("ai_attachment_no_extractable_text", "PDF contains no extractable text.", 422);
            return text;
        }
        catch (AppException) { throw; }
        catch (Exception) { throw new AppException("ai_attachment_parse_failed", "PDF text could not be extracted.", 422); }
    }

    private static string DecodeBounded(byte[] bytes) => BoundText(Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, MaximumExtractedBytes))));
    private static string BoundText(string value)
    {
        while (Encoding.UTF8.GetByteCount(value) > MaximumExtractedBytes) value = value[..^Math.Max(1, value.Length / 20)];
        return value;
    }
}
