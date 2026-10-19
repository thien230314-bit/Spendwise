using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Configuration.Commands.UpdateConfiguration;

// Command cập nhật giá trị cấu hình vận hành sau khi kiểm tra dữ liệu.

public sealed record UpdateConfigurationCommand(
    Guid AdminId,
    string Key,
    UpdateConfigurationRequest Request) : IRequest<ConfigurationDto>;
