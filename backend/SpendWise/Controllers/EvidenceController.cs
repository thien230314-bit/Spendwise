using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Evidence;
using SpendWise.Application.Features.Evidence.Commands.UploadEvidence;
using SpendWise.Application.Features.Evidence.Queries.GetEvidence;
using SpendWise.Application.Finance;
using SpendWise.API.Infrastructure;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize]
[Route("api/evidence")]
public sealed class EvidenceController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("financial")]
    [RequestSizeLimit(EvidenceSupport.MaximumFileSize + 64 * 1024)]
    public async Task<ActionResult<EvidenceDto>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length > EvidenceSupport.MaximumFileSize)
        {
            return BadRequest(new
            {
                code = "invalid_evidence_size",
                message = ApiErrorLocalizer.Localize(
                    "invalid_evidence_size",
                    "Evidence must not exceed 2 MB.")
            });
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var result = await mediator.Send(new UploadEvidenceCommand(
            UserId(),
            new UploadEvidenceData(file.FileName, file.ContentType, stream.ToArray())), cancellationToken);
        return CreatedAtAction(nameof(Download), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var evidence = await mediator.Send(new GetEvidenceQuery(UserId(), id), cancellationToken);
        return File(evidence.Content, evidence.ContentType, evidence.OriginalFileName);
    }

    private Guid UserId() => currentUser.UserId ?? throw new UnauthorizedAccessException();
}
