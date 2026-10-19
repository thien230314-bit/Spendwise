using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Admin.FeatureFlags.Commands.UpdateFeatureFlag;

// Handler bật hoặc tắt cờ tính năng theo quyền quản trị.

public sealed class UpdateFeatureFlagCommandHandler(
    ISpendWiseDbContext db,
    AdminAuditWriter audit) : IRequestHandler<UpdateFeatureFlagCommand, FeatureFlagDto>
{
    private static readonly string[] CurrentRuntimeFlags = ["AIAssistant", "P2PTransfer"];

    public async Task<FeatureFlagDto> Handle(
        UpdateFeatureFlagCommand command,
        CancellationToken cancellationToken)
    {
        // Tìm feature flag theo tên và ghi lại thay đổi có lý do vào audit log.
        AdminAuditWriter.RequireReason(command.Request.Reason);
        if (!CurrentRuntimeFlags.Contains(command.Name, StringComparer.Ordinal))
        {
            throw new NotFoundException("feature_flag_not_found", "Feature flag not found.");
        }
        var featureFlag = await db.FeatureFlags.SingleOrDefaultAsync(
            item => item.Name == command.Name,
            cancellationToken)
            ?? throw new NotFoundException(
                "feature_flag_not_found",
                "Feature flag not found.");

        featureFlag.IsEnabled = command.Request.IsEnabled;
        audit.Add(
            command.AdminId,
            "CHANGE_FEATURE_FLAG",
            "FeatureFlag",
            command.Name,
            command.Request.Reason,
            new { command.Request.IsEnabled });
        await db.SaveChangesAsync(cancellationToken);

        return new FeatureFlagDto(
            featureFlag.Name,
            featureFlag.IsEnabled,
            featureFlag.Description);
    }
}
