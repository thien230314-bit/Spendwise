using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Admin.FeatureFlags.Queries.GetFeatureFlags;

// Handler lấy các cờ tính năng đang được cấu hình.

public sealed class GetFeatureFlagsQueryHandler(
    ISpendWiseDbContext db,
    IMemoryCache cache)
    : IRequestHandler<GetFeatureFlagsQuery, IReadOnlyList<FeatureFlagDto>>
{
    private const string CacheKey = "admin:feature-flags";
    private static readonly string[] CurrentRuntimeFlags = ["AIAssistant", "P2PTransfer"];

    public async Task<IReadOnlyList<FeatureFlagDto>> Handle(
        GetFeatureFlagsQuery query,
        CancellationToken cancellationToken)
    {
        // Cache 5 giây chỉ phục vụ danh sách cấu hình hiển thị, không cache dữ liệu tiền.
        if (cache.TryGetValue<IReadOnlyList<FeatureFlagDto>>(CacheKey, out var cached)
            && cached is not null)
        {
            return cached;
        }

        // Đọc trạng thái các feature flag cho màn hình cấu hình admin.
        var flags = await db.FeatureFlags
            .AsNoTracking()
            .Where(item => CurrentRuntimeFlags.Contains(item.Name))
            .OrderBy(item => item.Name)
            .Select(item => new FeatureFlagDto(
                item.Name,
                item.IsEnabled,
                item.Description))
            .ToListAsync(cancellationToken);

        cache.Set(CacheKey, flags, TimeSpan.FromSeconds(5));
        return flags;
    }
}
