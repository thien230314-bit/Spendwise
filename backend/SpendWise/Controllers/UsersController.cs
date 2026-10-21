using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Users;
using SpendWise.Application.Features.Users.Commands.ConfirmChangeEmail;
using SpendWise.Application.Features.Users.Commands.RequestChangeEmail;
using SpendWise.Application.Features.Users.Commands.RevokeSessions;
using SpendWise.Application.Features.Users.Commands.UpdatePreferences;
using SpendWise.Application.Features.Users.Commands.UploadAvatar;
using SpendWise.Application.Features.Users.Queries.GetCurrentUser;
using SpendWise.Application.Identity;

namespace SpendWise.API.Controllers;

// Nhận yêu cầu hồ sơ và phiên đăng nhập rồi chuyển sang các use case người dùng.

[ApiController]
[Authorize]
[Route("api/users/me")]
public sealed class UsersController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<UserProfile> Get(CancellationToken ct)
    {
        // Đọc hồ sơ của người dùng từ UserId trong access token.
        var userId = UserId();
        var query = new GetCurrentUserQuery(userId);

        return await mediator.Send(query, ct);
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(AvatarSupport.MaximumFileSize + 64 * 1024)]
    public async Task<ActionResult<UserProfile>> UploadAvatar([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                code = "invalid_avatar_file",
                message = "Vui lòng chọn tệp ảnh đại diện hợp lệ."
            });
        }

        if (file.Length > AvatarSupport.MaximumFileSize)
        {
            return BadRequest(new
            {
                code = "invalid_avatar_size",
                message = "Kích thước ảnh đại diện không được vượt quá 5 MB."
            });
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var data = new UploadAvatarData(file.FileName, file.ContentType, stream.ToArray());

        var result = await mediator.Send(new UploadAvatarCommand(UserId(), data), ct);
        return Ok(result);
    }

    [HttpPost("email/request")]
    public async Task<ActionResult<EmailVerificationPendingResponse>> RequestEmailChange(
        [FromBody] RequestChangeEmailRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new RequestChangeEmailCommand(UserId(), request, Ip()), ct);
        return Ok(result);
    }

    [HttpPost("email/confirm")]
    public async Task<ActionResult<UserProfile>> ConfirmEmailChange(
        [FromBody] ConfirmChangeEmailRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ConfirmChangeEmailCommand(UserId(), request, Ip()), ct);
        return Ok(result);
    }

    [HttpPut("preferences")]
    public async Task<UserProfile> Preferences(
        UpdatePreferencesRequest request,
        CancellationToken ct)
    {
        // Gửi Command cập nhật tùy chọn của người dùng hiện tại.
        var userId = UserId();
        var command = new UpdatePreferencesCommand(userId, request);

        return await mediator.Send(command, ct);
    }

    [HttpDelete("sessions")]
    public async Task<IActionResult> RevokeSessions(CancellationToken ct)
    {
        // Gửi Command thu hồi các phiên đăng nhập của chính người dùng.
        var userId = UserId();
        var command = new RevokeSessionsCommand(userId);
        await mediator.Send(command, ct);

        return NoContent();
    }

    private Guid UserId()
    {
        // Lấy định danh từ phiên đã xác thực, không tin UserId do client gửi.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }

    private string? Ip()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
