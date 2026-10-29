using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Authentication;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Administration;

public sealed record OrdinaryAdminDto(Guid Id, string DisplayName, string Status, DateTime CreatedAt);

public sealed class OrdinaryAdminLifecycle(
    ISpendWiseDbContext db, PlatformPermissionService permissions, AdminSecurityGuard security,
    AuthenticationSupport authentication, AdminAuditWriter audit)
{
    public Task<IReadOnlyList<OrdinaryAdminDto>> ListAsync(ClaimsPrincipal principal, string reason, int page, int pageSize, CancellationToken ct)
    {
        throw new ForbiddenException("operator_management_disabled", "Operator directory is disabled in V1.0. System has exactly one Admin.");
    }

    public Task SetAsync(ClaimsPrincipal principal, Guid targetId, bool enabled,
        string securityPin, string reason, CancellationToken ct)
    {
        throw new ForbiddenException("operator_management_disabled", "Operator role change is disabled in V1.0. System has exactly one Admin.");
    }
}
