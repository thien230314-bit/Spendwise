using System.Security.Claims;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.API.Infrastructure;

// Lớp này đọc danh tính, vai trò và SessionId từ các claim của access token.

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string SystemRole => Principal?.FindFirstValue(ClaimTypes.Role) ?? "User";
    public string Locale => accessor.HttpContext?.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>()?.RequestCulture.Culture.Name ?? "vi-VN";
    public string? SessionId => Principal?.FindFirstValue("sid");
}
