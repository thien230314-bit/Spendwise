using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Users.Commands.UploadAvatar;

public sealed record UploadAvatarCommand(Guid UserId, UploadAvatarData Data) : IRequest<UserProfile>;

public sealed class UploadAvatarCommandHandler(
    ISpendWiseDbContext db,
    AvatarSupport avatarSupport) : IRequestHandler<UploadAvatarCommand, UserProfile>
{
    public async Task<UserProfile> Handle(UploadAvatarCommand command, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new NotFoundException("user_not_found", "User not found.");

        var avatarUrl = await avatarSupport.SaveAvatarAsync(command.UserId, command.Data, ct);

        user.AvatarUrl = avatarUrl;
        await db.SaveChangesAsync(ct);

        return AuthenticationSupport.MapProfile(user);
    }
}
