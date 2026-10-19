using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Cases.Queries.GetAdminCases;

// Query lấy danh sách hồ sơ quản trị theo loại case được yêu cầu.

public sealed record GetAdminCasesQuery(
    string Type) : IRequest<IReadOnlyList<CaseDto>>;
