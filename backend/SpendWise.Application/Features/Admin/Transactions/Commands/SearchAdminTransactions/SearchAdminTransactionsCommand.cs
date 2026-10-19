using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Transactions.Commands.SearchAdminTransactions;

// Command tra cứu giao dịch ở chế độ quản trị chỉ đọc.

public sealed record SearchAdminTransactionsCommand(
    Guid AdminId,
    SensitiveAccessRequest Request) : IRequest<IReadOnlyList<AdminTransactionDto>>;
