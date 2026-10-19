namespace SpendWise.Application.Common.Interfaces;

// Interface này cung cấp cờ tính năng và ngưỡng vận hành cho các use case.

public interface IOperationPolicy
{
    Task EnsureFeatureEnabledAsync(string featureName, CancellationToken cancellationToken);
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken cancellationToken);
}
