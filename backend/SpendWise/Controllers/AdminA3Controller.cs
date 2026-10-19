using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.API.Infrastructure;
using SpendWise.Application.Features.Administration;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize(Policy = V1Authorization.OperationsRead)]
[Route("api/v1/admin/support/{caseId:guid}/funds")]
public sealed class AdminA3Controller(AdminA3Service funds) : ControllerBase
{
    [HttpPost("search")]
    public Task<A2Page<A3Fund>> Search(Guid caseId, A3Query request, CancellationToken ct) => funds.SearchAsync(User, caseId, request, ct);
    [HttpPost("{fundId:guid}/detail")]
    public Task<A3Detail> Detail(Guid caseId, Guid fundId, OperatorDirectoryRequest request, CancellationToken ct) => funds.DetailAsync(User, caseId, fundId, request.Reason, ct);
    [HttpPost("{fundId:guid}/members")]
    public Task<A2Page<A3Member>> Members(Guid caseId, Guid fundId, A3FeedQuery request, CancellationToken ct) => funds.MembersAsync(User, caseId, fundId, request, ct);
    [HttpPost("{fundId:guid}/activity")]
    public Task<A2Page<A3Event>> Activity(Guid caseId, Guid fundId, A3FeedQuery request, CancellationToken ct) => funds.ActivityAsync(User, caseId, fundId, request, ct);
    [HttpPost("{fundId:guid}/finance/{source}")]
    public Task<A2Page<A3Money>> Finance(Guid caseId, Guid fundId, string source, A3FeedQuery request, CancellationToken ct) => funds.FinanceAsync(User, caseId, fundId, source, request, ct);
    [HttpPost("{fundId:guid}/treasury")]
    public Task<A3Treasury> Treasury(Guid caseId, Guid fundId, OperatorDirectoryRequest request, CancellationToken ct) => funds.TreasuryAsync(User, caseId, fundId, request.Reason, ct);
    [HttpPost("{fundId:guid}/postings/{postingId:guid}")]
    public Task<A3Posting> Posting(Guid caseId, Guid fundId, Guid postingId, OperatorDirectoryRequest request, CancellationToken ct) => funds.PostingAsync(User, caseId, fundId, postingId, request.Reason, ct);
    [HttpPost("{fundId:guid}/governance/{source}")]
    public Task<A2Page<A3Governance>> Governance(Guid caseId, Guid fundId, string source, A3FeedQuery request, CancellationToken ct) => funds.GovernanceAsync(User, caseId, fundId, source, request, ct);
    [HttpPost("{fundId:guid}/cases")]
    public Task<A2Page<A3Case>> Cases(Guid caseId, Guid fundId, A3FeedQuery request, CancellationToken ct) => funds.CasesAsync(User, caseId, fundId, request, ct);
}
