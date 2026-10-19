using MediatR;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Evidence.Commands.UploadEvidence;

public sealed class UploadEvidenceCommandHandler(
    ISpendWiseDbContext db,
    EvidenceSupport evidence) : IRequestHandler<UploadEvidenceCommand, EvidenceDto>
{
    public async Task<EvidenceDto> Handle(UploadEvidenceCommand command, CancellationToken cancellationToken)
    {
        var attachment = evidence.Create(command.UserId, command.Data);
        db.EvidenceAttachments.Add(attachment);
        await db.SaveChangesAsync(cancellationToken);
        return new EvidenceDto(attachment.Id, attachment.OriginalFileName, attachment.ContentType,
            attachment.Length, attachment.ContentHash, attachment.CreatedAt);
    }
}
