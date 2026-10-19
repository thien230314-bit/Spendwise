using MediatR;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Users.Commands.UpdatePreferences;

// Command lưu tùy chọn hợp lệ cho người dùng hiện tại.

public sealed record UpdatePreferencesCommand(
    Guid UserId,
    UpdatePreferencesRequest Request) : IRequest<UserProfile>;
