using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Admin.Configuration.Commands.UpdateConfiguration;

// Handler cập nhật giá trị cấu hình vận hành sau khi kiểm tra dữ liệu.

public sealed class UpdateConfigurationCommandHandler(
    ISpendWiseDbContext db,
    AdminAuditWriter audit) : IRequestHandler<UpdateConfigurationCommand, ConfigurationDto>
{
    private static readonly string[] AllowedKeys =
    [
        "TransferMinimum",
        "TransferMaximum",
        "DailyTransferLimit",
        "RiskReviewThreshold"
    ];

    public async Task<ConfigurationDto> Handle(
        UpdateConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        // Kiểm tra khóa cấu hình, kiểu giá trị và lý do quản trị trước khi lưu.
        AdminAuditWriter.RequireReason(command.Request.Reason);
        var value = InputBoundary.RequiredText(command.Request.Value, 500, "Configuration value");
        if (!AllowedKeys.Contains(command.Key))
        {
            throw new ForbiddenException(
                "configuration_not_allowed",
                "Configuration key is not admin-editable.");
        }

        var configuration = await db.SystemConfigurations.SingleOrDefaultAsync(
            item => item.Key == command.Key,
            cancellationToken)
            ?? throw new NotFoundException(
                "configuration_not_found",
                "Configuration not found.");
        if (!decimal.TryParse(value, out _))
        {
            throw new AppException(
                "invalid_configuration",
                "A numeric value is required.");
        }

        configuration.Value = value;
        audit.Add(
            command.AdminId,
            "CHANGE_CONFIGURATION",
            "SystemConfiguration",
            command.Key,
            command.Request.Reason,
            new { command.Request.Value });
        await db.SaveChangesAsync(cancellationToken);

        return new ConfigurationDto(
            configuration.Key,
            configuration.Value,
            configuration.ValueType,
            configuration.Description);
    }
}
