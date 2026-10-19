using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Users.Commands.RevokeSessions;
using SpendWise.Application.Features.Users.Commands.UpdatePreferences;
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
}
