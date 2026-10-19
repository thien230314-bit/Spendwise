using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Evidence.Queries.GetEvidence;

public sealed class GetEvidenceQueryHandler(ISpendWiseDbContext db)
    : IRequestHandler<GetEvidenceQuery, EvidenceContentDto>
{
    public async Task<EvidenceContentDto> Handle(GetEvidenceQuery query, CancellationToken cancellationToken)
    {
        var item = await db.EvidenceAttachments.AsNoTracking().SingleOrDefaultAsync(
            evidence => evidence.Id == query.EvidenceId && evidence.OwnerUserId == query.UserId,
            cancellationToken)
            ?? throw new NotFoundException("evidence_not_found", "Evidence not found.");
        return new EvidenceContentDto(item.OriginalFileName, item.ContentType, item.Content);
    }
}
