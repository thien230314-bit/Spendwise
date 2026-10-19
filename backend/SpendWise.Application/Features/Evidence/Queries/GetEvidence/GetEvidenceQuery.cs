using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Evidence.Queries.GetEvidence;

public sealed record GetEvidenceQuery(Guid UserId, Guid EvidenceId) : IRequest<EvidenceContentDto>;
