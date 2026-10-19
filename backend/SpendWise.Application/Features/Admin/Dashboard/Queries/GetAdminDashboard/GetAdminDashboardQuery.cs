using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Dashboard.Queries.GetAdminDashboard;

// Query tổng hợp số liệu giám sát cho bảng điều khiển quản trị.

public sealed record GetAdminDashboardQuery(
    DateTime? From,
    DateTime? To,
    System.Security.Claims.ClaimsPrincipal? Principal = null) : IRequest<AdminDashboardDto>;
