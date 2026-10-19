using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Admin.Configuration.Queries.GetConfigurations;

// Handler lấy các giá trị cấu hình vận hành hiện tại.

public sealed class GetConfigurationsQueryHandler(
    ISpendWiseDbContext db)
    : IRequestHandler<GetConfigurationsQuery, IReadOnlyList<ConfigurationDto>>
{
    public async Task<IReadOnlyList<ConfigurationDto>> Handle(
        GetConfigurationsQuery query,
        CancellationToken cancellationToken)
    {
        // Đọc danh sách cấu hình hệ thống để màn hình admin hiển thị.
        return await db.SystemConfigurations
            .AsNoTracking()
            .OrderBy(item => item.Key)
            .Select(item => new ConfigurationDto(
                item.Key,
                item.Value,
                item.ValueType,
                item.Description))
            .ToListAsync(cancellationToken);
    }
}
