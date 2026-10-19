using SpendWise.Application.Admin;
using SpendWise.Application.Features.Admin.Cases;

namespace SpendWise.API.Contracts;

public sealed class CaseMessageMultipartRequest
{
    public const long MaximumBodyLength =
        (long)CaseAttachmentValidator.MaximumFiles * CaseAttachmentValidator.MaximumFileSize
        + 1024 * 1024;

    public string Message { get; set; } = string.Empty;
    public List<IFormFile> Files { get; set; } = [];

    public async Task<CaseMessageRequest> ToApplicationRequestAsync(CancellationToken cancellationToken)
    {
        CaseAttachmentValidator.ValidateCount(Files.Count);
        var uploads = new List<CaseAttachmentUpload>(Files.Count);
        foreach (var file in Files)
        {
            CaseAttachmentValidator.ValidateSize(file.Length);
            using var content = new MemoryStream((int)file.Length);
            await file.CopyToAsync(content, cancellationToken);
            uploads.Add(new CaseAttachmentUpload(
                file.FileName,
                file.ContentType,
                content.ToArray()));
        }

        return new CaseMessageRequest(Message, uploads);
    }
}
