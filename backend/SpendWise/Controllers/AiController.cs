using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MediatR;
using SpendWise.Application.AI;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.AiAssistant.Commands.CreateAiConversation;
using SpendWise.Application.Features.AiAssistant.Commands.DeleteAiConversation;
using SpendWise.Application.Features.AiAssistant.Commands.SendAiMessage;
using SpendWise.Application.Features.AiAssistant.Commands.UpdateAiConversation;
using SpendWise.Application.Features.AiAssistant.Queries.GetAiConversationById;
using SpendWise.Application.Features.AiAssistant.Queries.GetAiConversations;
using SpendWise.Application.Features.AiAssistant;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize(Policy = "User")]
[Route("api/ai")]
[EnableRateLimiting("ai")]
public sealed class AiController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("conversations")]
    public async Task<IReadOnlyList<AiConversationDto>> List([FromQuery] string? search, CancellationToken ct)
    {
        // Lấy danh sách hội thoại thuộc người dùng đang đăng nhập.
        var userId = UserId();

        return await mediator.Send(new GetAiConversationsQuery(userId, search), ct);
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<AiConversationDto> Get(Guid id, CancellationToken ct)
    {
        // Query tải một hội thoại cùng lịch sử tin nhắn theo quyền sở hữu.
        return await mediator.Send(new GetAiConversationByIdQuery(UserId(), id), ct);
    }

    [HttpPost("conversations")]
    public async Task<AiConversationDto> Create(
        CreateAiConversationRequest request,
        CancellationToken ct)
    {
        // Tạo hội thoại qua Application, không đặt business logic trong Controller.
        var userId = UserId();

        return await mediator.Send(
            new CreateAiConversationCommand(userId, request),
            ct);
    }

    [HttpPut("conversations/{id:guid}")]
    public async Task<AiConversationDto> Update(
        Guid id,
        UpdateAiConversationRequest request,
        CancellationToken ct)
    {
        // Chỉ đổi tiêu đề hội thoại; tin nhắn cũ vẫn giữ nguyên.
        return await mediator.Send(new UpdateAiConversationCommand(UserId(), id, request), ct);
    }

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        // Chỉ xóa hội thoại nếu Handler xác nhận đúng chủ sở hữu.
        var userId = UserId();
        await mediator.Send(new DeleteAiConversationCommand(userId, id), ct);

        return NoContent();
    }

    [HttpPost("chat")]
    public async Task<AiChatResponse> Chat(
        AiChatRequest request,
        CancellationToken ct)
    {
        // Gửi nội dung chat cùng thông tin phiên đã xác thực sang Handler.
        var userId = UserId();

        return await mediator.Send(
            new SendAiMessageCommand(
                userId,
                currentUser.SystemRole,
                currentUser.Locale,
                request),
            ct);
    }

    [HttpPost("attachments")]
    [RequestSizeLimit(10 * 1024 * 1024 + 64 * 1024)]
    public async Task<ActionResult<AiAttachmentDto>> UploadAttachment(IFormFile file, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var result = await mediator.Send(new UploadAiAttachmentCommand(UserId(),
            new UploadAiAttachmentData(file.FileName, file.ContentType, stream.ToArray())), ct);
        return Created($"/api/ai/attachments/{result.Id}", result);
    }

    [HttpGet("attachments/{id:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAiAttachmentQuery(UserId(), id), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("actions/{id:guid}")]
    public Task<AiActionDto> GetAction(Guid id, CancellationToken ct) => mediator.Send(new GetAiActionQuery(UserId(), id), ct);

    [HttpPost("actions/{id:guid}/execute")]
    public Task<AiActionDto> ExecuteAction(Guid id, ExecuteAiActionRequest request, CancellationToken ct) =>
        mediator.Send(new ExecuteAiActionCommand(UserId(), id, request), ct);

    [HttpPost("actions/{id:guid}/cancel")]
    public Task<AiActionDto> CancelAction(Guid id, CancellationToken ct) => mediator.Send(new CancelAiActionCommand(UserId(), id), ct);

    private Guid UserId()
    {
        // UserId phải đến từ access token, không đến từ request body.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}
