using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Features.Operations;

namespace SpendWise.Infrastructure.Services;

public sealed class V1MaintenanceScheduler(IServiceScopeFactory scopes, ILogger<V1MaintenanceScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<V1MaintenanceService>().ActivateDueAsync(ct);
                await scope.ServiceProvider.GetRequiredService<V1MaintenanceReminders>().ProcessAsync(ct);
                await scope.ServiceProvider.GetRequiredService<SpendWise.Application.Features.V1Groups.V1FundService>().ProgressElectionsAsync(ct);
                await scope.ServiceProvider.GetRequiredService<SpendWise.Application.Features.V1Groups.V1FundService>().ProgressActivitiesAsync(ct);
                await scope.ServiceProvider.GetRequiredService<V1BroadcastService>().ProcessDueAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Maintenance activation observation failed ({ErrorType}).", ex.GetType().Name); }
            if (!await timer.WaitForNextTickAsync(ct)) break;
        }
    }
}
