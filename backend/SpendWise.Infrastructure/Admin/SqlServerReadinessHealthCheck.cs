using Microsoft.Extensions.Diagnostics.HealthChecks;
using SpendWise.Application.Admin;

namespace SpendWise.Infrastructure.Admin;

// Readiness chỉ báo Healthy khi ứng dụng thật sự kết nối được cơ sở dữ liệu.
public sealed class SqlServerReadinessHealthCheck(
    IDatabaseHealthChecker databaseHealthChecker) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Thực hiện truy vấn kết nối nhẹ, không đọc dữ liệu nghiệp vụ.
            return await databaseHealthChecker.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("SQL Server is reachable.")
                : HealthCheckResult.Unhealthy("SQL Server is not reachable.");
        }
        catch (Exception exception)
        {
            // Giữ API sống nhưng báo Not Ready để bộ điều phối không gửi lưu lượng vào.
            return HealthCheckResult.Unhealthy(
                "SQL Server readiness check failed.",
                exception);
        }
    }
}
