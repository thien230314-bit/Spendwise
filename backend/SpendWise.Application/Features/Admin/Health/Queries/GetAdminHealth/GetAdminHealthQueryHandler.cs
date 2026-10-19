using MediatR;
using SpendWise.Application.AI;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Banking;
using Microsoft.EntityFrameworkCore;

namespace SpendWise.Application.Features.Admin.Health.Queries.GetAdminHealth;

// Handler kiểm tra trạng thái database và các thành phần hạ tầng.

public sealed class GetAdminHealthQueryHandler(
    IDatabaseHealthChecker healthChecker,
    IAiProviderStatus aiProviderStatus,
    IBankHubConfiguration bankConfiguration,
    ISpendWiseDbContext db) : IRequestHandler<GetAdminHealthQuery, object>
{
    public async Task<object> Handle(
        GetAdminHealthQuery query,
        CancellationToken cancellationToken)
    {
        // Tổng hợp khả năng kết nối database và trạng thái dịch vụ cho admin.
        var sqlServerStatus = await healthChecker.CanConnectAsync(cancellationToken)
            ? "Healthy"
            : "Unhealthy";
        var aiStatus = aiProviderStatus.Status;

        var bank = bankConfiguration.GetSafeStatus();
        var pendingOutbox = 0;
        var failedOutbox = 0;
        if (sqlServerStatus == "Healthy")
        {
            pendingOutbox = await db.OutboxMessages.CountAsync(x => x.ProcessedAt == null, cancellationToken);
            failedOutbox = await db.OutboxMessages.CountAsync(x => x.LastError != null, cancellationToken);
        }

        return new
        {
            api = "Healthy",
            sqlServer = sqlServerStatus,
            aiProvider = aiStatus,
            signalR = "Configured",
            sePay = new { enabled = bank.Enabled, configured = bank.Configured, environment = bank.Environment },
            notificationsOutbox = new { status = "Enabled", pending = pendingOutbox, failed = failedOutbox },
            recurringProcessor = "Registered",
            backgroundProcessing = "Registered"
        };
    }
}
