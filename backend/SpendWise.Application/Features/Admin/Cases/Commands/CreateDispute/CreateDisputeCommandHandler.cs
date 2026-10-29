using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.Admin.Cases.Commands.CreateDispute;

// Handler mở tranh chấp cho giao dịch theo workflow kiểm soát.

public sealed class CreateDisputeCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<CreateDisputeCommand, CaseDto>
{
    public async Task<CaseDto> Handle(
        CreateDisputeCommand command,
        CancellationToken cancellationToken)
    {
        // Nghiệp vụ Tranh chấp (Dispute) độc lập đã bị bãi bỏ theo quyết định Product Owner SpendWise V1.0.
        // Hợp nhất toàn bộ yêu cầu tra soát / khiếu nại vào SupportCase (Category: complaint_*).
        throw new AppException(
            "dispute_workflow_deprecated",
            "Quy trình Tranh chấp độc lập đã ngừng tiếp nhận mới. Vui lòng gửi Khiếu nại qua POST /api/cases/support với phân loại complaint_transaction.",
            400);
    }
}
