using Microsoft.Extensions.Caching.Memory;
using SpendWise.Application.Features.Admin.FeatureFlags.Queries.GetFeatureFlags;
using SpendWise.Domain.Admin;
using Xunit;

namespace SpendWise.Tests;

// Kiểm tra các tối ưu đọc và nén không làm thay đổi contract của API.
public sealed class PerformanceOptimizationTests
{
    [Fact]
    public async Task Api_compresses_json_response_when_client_accepts_gzip()
    {
        // Arrange: client HTTPS yêu cầu response JSON được nén bằng Gzip.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");

        // Act: OpenAPI đủ lớn để xác nhận middleware nén toàn bộ pipeline JSON.
        using var response = await client.GetAsync("/openapi/v1.json");

        // Assert: response thành công và công bố đúng content encoding.
        response.EnsureSuccessStatusCode();
        Assert.Contains("gzip", response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task Feature_flag_query_uses_short_lived_non_financial_cache()
    {
        // Arrange: lưu một cờ tĩnh và dùng cache riêng cho test này.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var flag = new FeatureFlag
        {
            Name = "P2PTransfer",
            IsEnabled = false,
            Description = "Cache test"
        };
        fixture.Db.FeatureFlags.Add(flag);
        await fixture.Db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetFeatureFlagsQueryHandler(fixture.Db, cache);
        var first = await handler.Handle(new GetFeatureFlagsQuery(), default);

        // Act: database đổi ngay nhưng lần đọc kế tiếp vẫn dùng snapshot tối đa 5 giây.
        flag.IsEnabled = true;
        await fixture.Db.SaveChangesAsync();
        var cached = await handler.Handle(new GetFeatureFlagsQuery(), default);

        // Assert: cache chỉ giữ DTO cấu hình, không chứa dữ liệu ví hoặc giao dịch.
        Assert.False(Assert.Single(first).IsEnabled);
        Assert.False(Assert.Single(cached).IsEnabled);
    }

    [Fact]
    public async Task Feature_flag_query_hides_retired_historical_rows()
    {
        // Arrange: mô phỏng các hàng cũ còn tồn tại sau nhiều phiên bản database.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        fixture.Db.FeatureFlags.AddRange(
            new FeatureFlag { Name = "P2PTransfer", IsEnabled = true, Description = "Current" },
            new FeatureFlag { Name = "AIAssistant", IsEnabled = true, Description = "Current" },
            new FeatureFlag { Name = "MoneyRequest", IsEnabled = true, Description = "Retired" },
            new FeatureFlag { Name = "AIWriteActions", IsEnabled = false, Description = "AI write actions are not implemented" });
        await fixture.Db.SaveChangesAsync();

        // Act: Admin query chỉ trả các cờ có runtime gate thật.
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var flags = await new GetFeatureFlagsQueryHandler(fixture.Db, cache)
            .Handle(new GetFeatureFlagsQuery(), default);

        // Assert: trạng thái lịch sử không còn xuất hiện trong contract hiện hành.
        Assert.Equal(["AIAssistant", "P2PTransfer"], flags.Select(item => item.Name).ToArray());
        Assert.DoesNotContain(flags, item => item.Description.Contains("not implemented", StringComparison.OrdinalIgnoreCase));
    }
}
