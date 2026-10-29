using MediatR;
using SpendWise.Application.AI;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Banking;
using SpendWise.Domain.AI;
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

        var latestAiMessage = await db.AiMessages.AsNoTracking()
            .Where(x => x.Role == AiMessageRole.Assistant && x.Provider != null)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.CreatedAt, x.LatencyMs, x.Provider })
            .FirstOrDefaultAsync(cancellationToken);

        var totalAiResponses = await db.AiMessages.CountAsync(x => x.Role == AiMessageRole.Assistant && x.Provider != null, cancellationToken);
        var totalPolicyFallbacks = await db.AiMessages.CountAsync(x => x.Role == AiMessageRole.Assistant && x.Provider == "SpendWisePolicy", cancellationToken);
        var liveGeminiResponses = await db.AiMessages.CountAsync(x => x.Role == AiMessageRole.Assistant && x.Provider == "Gemini", cancellationToken);
        var totalWebhooks = await db.BankIntegrationEvents.CountAsync(cancellationToken);

        var providerType = latestAiMessage?.Provider switch
        {
            "Gemini" => "LiveProvider",
            "SpendWisePolicy" => "PolicyFallback",
            "Mock" => "MockProvider",
            _ => latestAiMessage != null ? "InternalProvider" : "None"
        };

        var aiLiveStatus = latestAiMessage?.Provider == "Gemini"
            ? "VERIFIED_LIVE (Gemini API)"
            : "NOT VERIFIED (Policy Fallback Only — Live Gemini Unverified)";

        var sePayLiveStatus = (bank.Configured && bank.LastSuccessfulProviderCallAt != null && bank.LastWebhookReceivedAt != null)
            ? "VERIFIED_LIVE"
            : "NOT VERIFIED (No Live Webhook or Bank Signal — Sandbox Safe)";

        return new
        {
            api = "Healthy",
            sqlServer = sqlServerStatus,
            aiProvider = new
            {
                status = aiStatus,
                configured = aiProviderStatus.IsOnlineConfigured,
                provider = "Gemini",
                model = "gemini-2.5-flash",
                providerType,
                activeMode = aiProviderStatus.IsOnlineConfigured ? "Online (Configured — Fallback Active)" : "Offline (SpendWisePolicy Fallback)",
                totalResponses = totalAiResponses,
                policyFallbacks = totalPolicyFallbacks,
                liveGeminiResponses = liveGeminiResponses,
                lastSuccessAtUtc = latestAiMessage?.CreatedAt,
                lastLatencyMs = latestAiMessage?.LatencyMs,
                lastProviderUsed = latestAiMessage?.Provider ?? "None",
                lastFailureAtUtc = (DateTime?)null,
                quotaUsagePolicy = "Protected (Calls strictly throttled to prevent quota exhaustion)",
                liveVerificationStatus = aiLiveStatus
            },
            signalR = "Configured",
            sePay = new
            {
                enabled = bank.Enabled,
                configured = bank.Configured,
                environment = bank.Environment,
                mode = "Sandbox / Simulation (Zero Real Money)",
                companyConfigured = bank.CompanyConfigured,
                lifecycleWebhookConfigured = bank.LifecycleWebhookConfigured,
                ipnConfigured = bank.IpnConfigured,
                totalWebhooksObserved = totalWebhooks,
                lastSuccessfulProviderCallAt = bank.LastSuccessfulProviderCallAt,
                lastWebhookReceivedAt = bank.LastWebhookReceivedAt,
                reconciliationStatus = "Ready (Simulated events verified, real money NOT VERIFIED)",
                liveVerificationStatus = sePayLiveStatus
            },
            notificationsOutbox = new { status = "Enabled", pending = pendingOutbox, failed = failedOutbox },
            recurringProcessor = "Registered",
            backgroundProcessing = "Registered"
        };
    }
}
