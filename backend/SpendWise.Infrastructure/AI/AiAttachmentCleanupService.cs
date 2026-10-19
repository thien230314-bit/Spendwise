using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Infrastructure.AI;

// Dọn các file upload chưa từng gắn vào hội thoại để bảng attachment không phình vô hạn.
public sealed class AiAttachmentCleanupService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch { /* Không để worker làm dừng API; lần chạy kế tiếp sẽ thử lại. */ }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISpendWiseDbContext>();
        if (await SpendWise.Application.Features.Operations.V1MaintenanceGate.IsActiveAsync(db, timeProvider.GetUtcNow().UtcDateTime, cancellationToken, "AI")) return;
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddHours(-24);
        await db.AiAttachments.Where(x => x.AiConversationId == null && x.AiMessageId == null && x.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
