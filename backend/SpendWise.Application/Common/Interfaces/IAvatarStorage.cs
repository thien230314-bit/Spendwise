namespace SpendWise.Application.Common.Interfaces;

public interface IAvatarStorage
{
    Task<string> SaveAvatarAsync(Guid userId, string extension, byte[] content, CancellationToken ct);
}
