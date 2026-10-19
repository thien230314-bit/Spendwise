using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Configuration.Queries.GetConfigurations;

// Query lấy các giá trị cấu hình vận hành hiện tại.

public sealed record GetConfigurationsQuery
    : IRequest<IReadOnlyList<ConfigurationDto>>;
