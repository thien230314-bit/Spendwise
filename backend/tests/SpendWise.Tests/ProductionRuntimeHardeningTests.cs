using System.Net;
using Microsoft.Extensions.Configuration;
using SpendWise.API.Infrastructure;
using SpendWise.Infrastructure.AI;
using Xunit;

namespace SpendWise.Tests;

// Kiểm tra các hàng rào vận hành quan trọng ngay trên HTTP pipeline thật.
public sealed class ProductionRuntimeHardeningTests
{
    [Fact]
    public async Task HealthEndpoints_SeparateLivenessAndDatabaseReadiness()
    {
        // Arrange: test host dùng SQLite đang mở để mô phỏng database sẵn sàng.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        // Act: gọi riêng liveness, readiness và route health tương thích cũ.
        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");
        var compatible = await client.GetAsync("/health");

        // Assert: cả ba route đều hoạt động; readiness đã chạy kiểm tra database.
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, compatible.StatusCode);
    }

    [Fact]
    public async Task NonDevelopmentHost_AddsHstsHeader()
    {
        // Arrange: SpendWiseApiFactory chạy ở môi trường Testing, không phải Development.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        client.BaseAddress = new Uri("https://spendwise.test");

        // Act: gửi request HTTPS qua pipeline thật.
        var response = await client.GetAsync("/health/live");

        // Assert: HSTS được bật cho mọi môi trường ngoài Development.
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public void JwtSigningKey_RejectsKeyShorterThan256Bits()
    {
        // Act và Assert: API phải fail fast thay vì khởi động bằng khóa HS256 yếu.
        var exception = Assert.Throws<InvalidOperationException>(
            () => JwtSigningKey.Validate("too-short"));
        Assert.Contains("256 bits", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JwtSigningKey_AcceptsAtLeast32Utf8Bytes()
    {
        // Act: kiểm tra khóa đúng ngưỡng tối thiểu.
        var bytes = JwtSigningKey.Validate(new string('k', 32));

        // Assert: dữ liệu byte này được dùng trực tiếp để cấu hình HS256.
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void AiProviderStatus_RecognizesTheSameApplicationKeyUsedByProvider()
    {
        // Arrange: cấu hình khóa theo đường dẫn chuẩn OpenAI:ApiKey.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "test-provider-key"
            })
            .Build();

        // Act: health status đọc qua cùng helper với OpenAI provider.
        var status = new OpenAiProviderStatus(configuration);

        // Assert: admin health không báo fallback khi provider thực tế đã có khóa.
        Assert.True(status.IsOnlineConfigured);
    }
}
