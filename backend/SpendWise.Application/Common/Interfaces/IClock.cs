namespace SpendWise.Application.Common.Interfaces;

// Interface này cung cấp thời gian UTC thống nhất và có thể thay thế trong test.

public interface IClock
{
    DateTime UtcNow { get; }
}
