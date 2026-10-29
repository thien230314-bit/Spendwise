using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.API.Infrastructure;
using SpendWise.Application.Features.Administration;

namespace SpendWise.API.Controllers;

public sealed record ScopedProfileRequest(Guid UserId, string Reason);
public sealed record OrdinaryAdminRoleRequest(bool Enabled, string SecurityPin, string Reason);
public sealed record ScopedFundsRequest(Guid UserId, string Reason, string? Code = null, int Page = 1, int PageSize = 20);
public sealed record OperatorDirectoryRequest(string Reason, int Page = 1, int PageSize = 20);
public sealed record CaseAssignmentRequest(Guid AdminId, string SecurityPin, string Reason);

[ApiController]
[Authorize]
[Route("api/v1/admin")]
public sealed class V1AdministrationController(ScopedCaseProfileReader profiles, ScopedCaseTransactionReader transactions, ScopedCaseFundReader funds, OrdinaryAdminLifecycle operators, V1CaseAssignment cases, V1CaseWorkflow workflow) : ControllerBase
{
    [HttpPost("support/{caseId:guid}/detail")]
    [Authorize(Policy = V1Authorization.OperationsRead)]
    public Task<V1CaseDetail> Detail(Guid caseId, OperatorDirectoryRequest request, CancellationToken ct) => workflow.GetAsync(User, caseId, request.Reason, ct);
    [HttpPost("support/{caseId:guid}/accounts")]
    [Authorize(Policy = V1Authorization.OperationsRead)]
    public Task<V1CaseAccounts> Accounts(Guid caseId, OperatorDirectoryRequest request, CancellationToken ct) => workflow.AccountsAsync(User, caseId, request.Reason, ct);
    [HttpPost("support/{caseId:guid}/actions")]
    [EnableRateLimiting("financial")]
    [Authorize(Policy = V1Authorization.OperationsRead)]
    public async Task<IActionResult> Action(Guid caseId, V1CaseAction request, CancellationToken ct)
    { await workflow.ActAsync(User, caseId, request, ct); return NoContent(); }
    [HttpPost("support/search")]
    [Authorize(Policy = V1Authorization.OperationsRead)]
    public Task<IReadOnlyList<V1CaseTaskDto>> Queue(OperatorDirectoryRequest request, CancellationToken ct) => cases.QueueAsync(User, request.Reason, request.Page, request.PageSize, ct);
    [HttpPut("support/{caseId:guid}/assignment")]
    [Authorize(Policy = V1Authorization.OperationsRead)]
    public async Task<IActionResult> Assign(Guid caseId, CaseAssignmentRequest request, CancellationToken ct)
    { await cases.AssignAsync(User, caseId, request.AdminId, request.SecurityPin, request.Reason, ct); return NoContent(); }

    [HttpPost("operators/search")]
    [Authorize(Policy = V1Authorization.ManageOrdinaryAdmins)]
    public Task<IReadOnlyList<OrdinaryAdminDto>> Operators(OperatorDirectoryRequest request, CancellationToken ct) =>
        operators.ListAsync(User, request.Reason, request.Page, request.PageSize, ct);

    [HttpPost("support/{caseId:guid}/funds")]
    public Task<IReadOnlyList<ScopedFundMembership>> Funds(Guid caseId, ScopedFundsRequest request, CancellationToken ct) =>
        funds.GetAsync(User, caseId, request.UserId, request.Reason, request.Code, request.Page, request.PageSize, ct);

    [HttpPost("support/{caseId:guid}/user-profile")]
    public Task<ScopedUserProfile> Profile(Guid caseId, ScopedProfileRequest request, CancellationToken ct) =>
        profiles.GetAsync(User, caseId, request.UserId, request.Reason, ct);

    [HttpPost("support/{caseId:guid}/transaction")]
    public Task<ScopedCaseTransaction> Transaction(Guid caseId, ScopedProfileRequest request, CancellationToken ct) =>
        transactions.GetAsync(User, caseId, request.UserId, request.Reason, ct);

    [HttpPut("operators/{userId:guid}")]
    [EnableRateLimiting("financial")]
    [Authorize(Policy = V1Authorization.ManageOrdinaryAdmins)]
    public async Task<IActionResult> SetOperator(Guid userId, OrdinaryAdminRoleRequest request, CancellationToken ct)
    {
        await operators.SetAsync(User, userId, request.Enabled, request.SecurityPin, request.Reason, ct);
        return NoContent();
    }
}
