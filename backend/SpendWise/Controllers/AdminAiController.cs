using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.AI;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.AdminAi;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize(Policy = "SystemAdmin")]
[Route("api/admin/ai")]
[EnableRateLimiting("ai")]
public sealed class AdminAiController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("conversations")]
    public Task<IReadOnlyList<AiConversationDto>> List([FromQuery] string? search, CancellationToken ct) =>
        mediator.Send(new GetAdminAiConversationsQuery(AdminId(), search), ct);

    [HttpPost("conversations")]
    public Task<AiConversationDto> Create(CreateAiConversationRequest request, CancellationToken ct) =>
        mediator.Send(new CreateAdminAiConversationCommand(AdminId(), request), ct);

    [HttpGet("conversations/{id:guid}")]
    public Task<AiConversationDto> Get(Guid id, CancellationToken ct) =>
        mediator.Send(new GetAdminAiConversationQuery(AdminId(), id), ct);

    [HttpPut("conversations/{id:guid}")]
    public Task<AiConversationDto> Update(Guid id, UpdateAiConversationRequest request, CancellationToken ct) =>
        mediator.Send(new UpdateAdminAiConversationCommand(AdminId(), id, request), ct);

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteAdminAiConversationCommand(AdminId(), id), ct);
        return NoContent();
    }

    [HttpPost("chat")]
    public Task<AiChatResponse> Chat(AiChatRequest request, CancellationToken ct) =>
        mediator.Send(new SendAdminAiMessageCommand(AdminId(), currentUser.Locale, request), ct);

    [HttpPost("reports/export")]
    public async Task<IActionResult> ExportReport(AdminAiReportRequest request, CancellationToken ct)
    {
        var report = await mediator.Send(new ExportAdminAiReportCommand(request), ct);
        return File(report.Content, report.ContentType, report.FileName);
    }

    private Guid AdminId() => currentUser.UserId ?? throw new UnauthorizedAccessException();
}
