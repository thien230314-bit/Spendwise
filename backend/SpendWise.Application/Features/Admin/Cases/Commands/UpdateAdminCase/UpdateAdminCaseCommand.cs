using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Cases.Commands.UpdateAdminCase;

// Command cập nhật RiskCase, Dispute hoặc SupportCase theo loại case đã xác thực.

public sealed record UpdateAdminCaseCommand(
    Guid AdminId,
    string Type,
    Guid CaseId,
    UpdateCaseRequest Request) : IRequest<CaseDto>;
