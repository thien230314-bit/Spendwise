using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.FeatureFlags.Queries.GetFeatureFlags;

// Query lấy các cờ tính năng đang được cấu hình.

public sealed record GetFeatureFlagsQuery
    : IRequest<IReadOnlyList<FeatureFlagDto>>;
