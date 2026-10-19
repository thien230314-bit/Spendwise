using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Infrastructure.Services;

// Cung cấp thời gian UTC thống nhất để nghiệp vụ có thể kiểm thử.

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
