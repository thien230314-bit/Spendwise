using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Admin.Cases.Queries.GetAdminCases;

// Handler đọc RiskCase, Dispute hoặc SupportCase sau khi xác thực loại case.

public sealed class GetAdminCasesQueryHandler(
    ISpendWiseDbContext db)
    : IRequestHandler<GetAdminCasesQuery, IReadOnlyList<CaseDto>>
{
    public async Task<IReadOnlyList<CaseDto>> Handle(
        GetAdminCasesQuery query,
        CancellationToken cancellationToken)
    {
        // Query chỉ chọn collection theo loại; API hiện không có tham số lọc trạng thái.
        var normalizedType = query.Type.Trim().ToLowerInvariant();
        if (normalizedType == "risk")
        {
            var riskCases = await db.RiskCases
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            return riskCases.Select(AdminMappings.ToRiskCaseDto).ToList();
        }

        if (normalizedType == "disputes")
        {
            var disputes = await db.Disputes
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            return disputes.Select(AdminMappings.ToDisputeDto).ToList();
        }

        if (normalizedType == "support")
        {
            var supportCases = await db.SupportCases
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            return supportCases.Select(AdminMappings.ToSupportCaseDto).ToList();
        }

        throw new AppException(
            "invalid_case_type",
            "Case type must be risk, disputes, or support.");
    }
}
