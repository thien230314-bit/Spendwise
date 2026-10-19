using MediatR;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Users.Commands.UpdatePreferences;

// Handler lưu tùy chọn hợp lệ cho người dùng hiện tại.

public sealed class UpdatePreferencesCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<UpdatePreferencesCommand, UserProfile>
{
    public async Task<UserProfile> Handle(
        UpdatePreferencesCommand command,
        CancellationToken cancellationToken)
    {
        // Chỉ cập nhật tùy chọn giao diện/ngôn ngữ của người dùng đang đăng nhập.
        if (command.Request.PreferredLanguage is not ("vi-VN" or "en-US"))
        {
            throw new AppException(
                "invalid_language",
                "Supported languages are vi-VN and en-US.");
        }

        if (command.Request.DefaultCurrency != "VND")
        {
            throw new AppException(
                "invalid_currency",
                "Only VND is currently supported.");
        }

        // Theme chỉ nhận ba giá trị mà frontend hiện hỗ trợ, không phân biệt hoa/thường.
        var preferredTheme = (command.Request.PreferredTheme ?? string.Empty).Trim().ToLowerInvariant();
        if (preferredTheme is not ("light" or "dark" or "system"))
        {
            throw new AppException(
                "invalid_theme",
                "Supported themes are light, dark, and system.");
        }

        var user = await db.Users.FindAsync([command.UserId], cancellationToken)
            ?? throw new NotFoundException("user_not_found", "User not found.");
        user.PreferredLanguage = command.Request.PreferredLanguage;
        user.PreferredTheme = preferredTheme;
        user.DefaultCurrency = command.Request.DefaultCurrency;
        if (command.Request.EngagementEmailOptIn is bool engagementEmailOptIn)
            user.EngagementEmailOptIn = engagementEmailOptIn;
        await db.SaveChangesAsync(cancellationToken);

        return AuthenticationSupport.MapProfile(user);
    }
}
