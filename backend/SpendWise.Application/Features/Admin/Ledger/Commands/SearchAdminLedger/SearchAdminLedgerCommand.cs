using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Ledger.Commands.SearchAdminLedger;

// Command tra cứu posting ledger cho đối soát mà không chỉnh sửa bút toán.

public sealed record SearchAdminLedgerCommand(
    Guid AdminId,
    SensitiveAccessRequest Request) : IRequest<IReadOnlyList<LedgerTransactionDto>>;
