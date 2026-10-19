using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.API.Infrastructure;
using SpendWise.Application.Features.Administration;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize(Policy = V1Authorization.OperationsRead)]
[Route("api/v1/admin/support/{caseId:guid}/transactions")]
public sealed class AdminA4Controller(AdminA4Service service) : ControllerBase
{
    // A4.1 - Tất cả giao dịch
    [HttpPost("search")]
    public Task<A2Page<A4TransactionItem>> Transactions(Guid caseId, A4Query query, CancellationToken ct) =>
        service.TransactionsAsync(User, caseId, query, ct);

    // A4.2 - Tra cứu mã giao dịch
    [HttpPost("lookup")]
    public Task<A4LookupResult> Lookup(Guid caseId, [FromQuery] string code, OperatorDirectoryRequest request, CancellationToken ct) =>
        service.LookupAsync(User, caseId, code, request.Reason, ct);

    // A4.3 - Ví người dùng
    [HttpPost("wallets")]
    public Task<A4UserWalletsResult> Wallets(Guid caseId, OperatorDirectoryRequest request, CancellationToken ct) =>
        service.WalletsAsync(User, caseId, request.Reason, ct);

    // A4.4 - Giao dịch chờ / lỗi
    [HttpPost("pending-failed")]
    public Task<A2Page<A4PendingFailedTransaction>> PendingAndFailed(Guid caseId, A4Query query, CancellationToken ct) =>
        service.PendingAndFailedAsync(User, caseId, query, ct);

    // A4.5 - Chi tiết sổ cái
    [HttpPost("ledger/{ledgerTransactionId:guid}")]
    public Task<A4LedgerDetailResult> LedgerDetail(Guid caseId, Guid ledgerTransactionId, OperatorDirectoryRequest request, CancellationToken ct) =>
        service.LedgerDetailAsync(User, caseId, ledgerTransactionId, request.Reason, ct);

    // A4.6 - Truy vết giao dịch
    [HttpPost("{transactionId:guid}/trace")]
    public Task<A4TraceResult> Trace(Guid caseId, Guid transactionId, OperatorDirectoryRequest request, CancellationToken ct) =>
        service.TraceAsync(User, caseId, transactionId, request.Reason, ct);
}
