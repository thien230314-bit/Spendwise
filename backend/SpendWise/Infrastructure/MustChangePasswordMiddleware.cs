using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.API.Infrastructure;

// Chặn nghiệp vụ khi Admin/recovery yêu cầu người dùng đổi mật khẩu bắt buộc.
public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISpendWiseDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                out var userId)
            && !IsAllowedSecurityPath(context.Request))
        {
            var mustChange = await db.Users
                .Where(item => item.Id == userId)
                .Select(item => item.MustChangePassword)
                .SingleOrDefaultAsync(context.RequestAborted);
            if (mustChange)
            {
                throw new AppException(
                    "PASSWORD_CHANGE_REQUIRED",
                    "Password must be changed before using this operation.",
                    403);
            }
        }

        await next(context);
    }

    private static bool IsAllowedSecurityPath(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/security-pin/status", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return request.Method == HttpMethods.Get
            && path.Equals("/api/users/me", StringComparison.OrdinalIgnoreCase);
    }
}
