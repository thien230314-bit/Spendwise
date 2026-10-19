using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Operations;

namespace SpendWise.API.Infrastructure;

// Hai middleware này giữ ranh giới vai trò và chế độ bảo trì ở phía server.
public sealed class BusinessAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISpendWiseDbContext db)
    {
        var requestedPath = context.Request.Path.Value ?? string.Empty;
        if (requestedPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) && !IsSharedOrAdminPath(requestedPath)
            || requestedPath.StartsWith("/api/integrations/sepay", StringComparison.OrdinalIgnoreCase))
            await V1MaintenanceGate.RequireApiAvailableAsync(db, context.RequestServices?.GetService<IClock>()?.UtcNow ?? DateTime.UtcNow, requestedPath, context.RequestAborted);
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if ((context.User.IsInRole("SystemAdmin") || context.User.IsInRole("Admin")
                    || context.User.IsInRole("SuperAdmin")) && IsOrdinaryUserApi(path))
            {
                throw new ForbiddenException("user_role_required", "This endpoint is available to normal users only.");
            }

            if (context.User.IsInRole("User") && IsMaintenanceBlockedPath(path))
            {
                var enabled = await db.SystemMaintenanceStates.AsNoTracking()
                    .OrderByDescending(item => item.ChangedAt)
                    .Select(item => item.IsEnabled)
                    .FirstOrDefaultAsync(context.RequestAborted);
                if (enabled)
                {
                    throw new AppException("MAINTENANCE_MODE", "The service is temporarily under maintenance.", 503);
                }
            }
        }

        await next(context);
    }

    private static bool IsOrdinaryUserApi(string path) =>
        path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
        && !IsSharedOrAdminPath(path);

    private static bool IsMaintenanceBlockedPath(string path) =>
        path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
        && !IsSharedOrAdminPath(path);

    private static bool IsSharedOrAdminPath(string path) =>
        path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/v1/admin/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/v1/system/maintenance", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/v1/system/broadcasts", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/security-pin", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/account-recovery", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/integrations/sepay", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/system", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/ping", StringComparison.OrdinalIgnoreCase);
}
