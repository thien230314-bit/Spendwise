using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Evidence.Commands.UploadEvidence;

public sealed record UploadEvidenceCommand(Guid UserId, UploadEvidenceData Data) : IRequest<EvidenceDto>;
