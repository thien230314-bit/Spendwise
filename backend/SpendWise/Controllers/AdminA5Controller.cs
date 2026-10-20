using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.API.Infrastructure;
using SpendWise.Application.Features.Administration;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize(Policy = V1Authorization.OperationsRead)]
[Route("api/v1/admin/support")]
public sealed class AdminA5Controller(AdminA5Service service) : ControllerBase
{
    // A5.1 - Ticket mới & tra cứu
    [HttpPost("tickets/search")]
    public Task<A2Page<A5TicketListItem>> Tickets(A5TicketQuery query, CancellationToken ct) =>
        service.TicketsAsync(User, query, ct);

    // A5.2 - Đang xử lý / Phân công (Assign)
    [HttpPost("{caseId:guid}/assign")]
    public async Task<IActionResult> Assign(Guid caseId, A5AssignTicketRequest request, CancellationToken ct)
    {
        await service.AssignTicketAsync(User, caseId, request, ct);
        return Ok(new { success = true, caseId });
    }

    // A5.2 - Danh sách ticket được phân công
    [HttpPost("assigned")]
    public Task<IReadOnlyList<A5AssignedCaseItem>> AssignedCases(OperatorDirectoryRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        service.AssignedCasesAsync(User, request.Reason, page, pageSize, ct);

    // A5.3 - Chờ User phản hồi (Request info)
    [HttpPost("{caseId:guid}/request-info")]
    public async Task<IActionResult> RequestInfo(Guid caseId, A5RequestInfoRequest request, CancellationToken ct)
    {
        await service.RequestInfoAsync(User, caseId, request, ct);
        return Ok(new { success = true, caseId, status = "WaitingForUser" });
    }

    // A5.4 - Hồ sơ điều tra quỹ (Dossier)
    [HttpPost("{caseId:guid}/dossier")]
    public Task<A5FundDossierResult> FundDossier(Guid caseId, OperatorDirectoryRequest request, CancellationToken ct) =>
        service.FundDossierAsync(User, caseId, request.Reason, ct);

    // Legacy route retained to return a controlled V1.0 denial.
    [HttpPost("{caseId:guid}/escalate")]
    public Task<A5EscalationStatusResult> Escalate(Guid caseId, A5EscalateRequest request, CancellationToken ct) =>
        service.EscalateAsync(User, caseId, request, ct);

    // A5.6 - Đã giải quyết (Resolve or Reject)
    [HttpPost("{caseId:guid}/resolve")]
    public Task<A5ResolveResult> Resolve(Guid caseId, A5ResolveRequest request, CancellationToken ct) =>
        service.ResolveAsync(User, caseId, request, ct);

    [HttpPost("disputes/{requestId:guid}/resolve")]
    public Task<A5ResolveResult> ResolveDispute(Guid requestId, A5ResolveRequest request, CancellationToken ct) =>
        service.ResolveDisputeRequestAsync(User, requestId, request, ct);

    // A5.7 - Mẫu phản hồi (Catalog)
    [HttpPost("templates")]
    public Task<IReadOnlyList<A5SupportTemplateItem>> GetTemplates(OperatorDirectoryRequest request, CancellationToken ct) =>
        service.GetTemplatesAsync(User, request.Reason, ct);

    // A5.7 - Áp dụng mẫu phản hồi (Apply)
    [HttpPost("{caseId:guid}/reply-template")]
    public Task<A5AppliedTemplateResult> ApplyTemplate(Guid caseId, A5ApplyTemplateRequest request, CancellationToken ct) =>
        service.ApplyTemplateAsync(User, caseId, request, ct);
}
