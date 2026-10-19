using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Infrastructure.Admin;

// Đọc chính sách vận hành và cung cấp giá trị mặc định an toàn.

public sealed class OperationPolicy(SpendWiseDbContext db) : IOperationPolicy
{
    public async Task EnsureFeatureEnabledAsync(string featureName, CancellationToken ct)
    {
        // Nếu feature flag bị tắt rõ ràng thì dừng use case với trạng thái tạm không sẵn sàng.
        var enabled = await db.FeatureFlags
            .Where(x => x.Name == featureName)
            .Select(x => (bool?)x.IsEnabled)
            .SingleOrDefaultAsync(ct);
        if (enabled == false)
        {
            throw new AppException(
                "feature_disabled",
                $"The {featureName} feature is currently disabled.",
                503);
        }
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct)
    {
        // Đọc cấu hình số thập phân theo key và dùng giá trị mặc định khi chưa cấu hình.
        var value = await db.SystemConfigurations
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(ct);
        return decimal.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
