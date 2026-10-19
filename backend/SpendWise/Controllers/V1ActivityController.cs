using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.V1Groups;

namespace SpendWise.API.Controllers;

[ApiController, Authorize(Policy = "User"), Route("api/v1/activity")]
public sealed class V1ActivityController(V1ActivityService activity, ICurrentUser user) : ControllerBase
{
    // Foreground interaction only; clients must never call this from polling, refresh or background sync.
    [HttpPost, EnableRateLimiting("financial")]
    public async Task<IActionResult> Record(CancellationToken ct)
    {
        if (user.UserId is not { } id || !Guid.TryParse(user.SessionId, out var session)) throw new ForbiddenException();
        await activity.RecordAsync(id, session, ct); return NoContent();
    }
}
