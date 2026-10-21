using Microsoft.Extensions.Hosting;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Infrastructure.Services;

public sealed class LocalAvatarStorage(IHostEnvironment environment) : IAvatarStorage
{
    public async Task<string> SaveAvatarAsync(Guid userId, string extension, byte[] content, CancellationToken ct)
    {
        var webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");
        var avatarsDir = Path.Combine(webRoot, "avatars");
        if (!Directory.Exists(avatarsDir))
        {
            Directory.CreateDirectory(avatarsDir);
        }

        var safeFileName = $"{userId:N}_{DateTime.UtcNow.Ticks}{extension}";
        var filePath = Path.Combine(avatarsDir, safeFileName);

        await File.WriteAllBytesAsync(filePath, content, ct);

        try
        {
            var existingFiles = Directory.GetFiles(avatarsDir, $"{userId:N}_*");
            foreach (var existing in existingFiles)
            {
                if (!string.Equals(existing, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(existing);
                }
            }
        }
        catch
        {
            // Best effort cleanup of older avatars
        }

        return $"/avatars/{safeFileName}";
    }
}
