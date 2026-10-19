using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.FeatureFlags.Commands.UpdateFeatureFlag;

// Command bật hoặc tắt cờ tính năng theo quyền quản trị.

public sealed record UpdateFeatureFlagCommand(
    Guid AdminId,
    string Name,
    UpdateFeatureFlagRequest Request) : IRequest<FeatureFlagDto>;
