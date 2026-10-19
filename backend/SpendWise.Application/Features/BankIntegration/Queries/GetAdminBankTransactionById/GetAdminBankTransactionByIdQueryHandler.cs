using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankTransactionById;

public sealed class GetAdminBankTransactionByIdQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetAdminBankTransactionByIdQuery, AdminBankTransactionDto>
{
    public async Task<AdminBankTransactionDto> Handle(
        GetAdminBankTransactionByIdQuery query,
        CancellationToken cancellationToken)
    {
        // Projection join User và BankConnection trong một query, không tải dữ liệu nhạy cảm dư thừa.
        return await db.BankTransactions
            .AsNoTracking()
            .Where(item => item.Id == query.BankTransactionId)
            .Select(item => new AdminBankTransactionDto(
                item.Id,
                item.UserId,
                item.User.FullName,
                item.User.SpendId,
                item.BankConnectionId,
                item.Provider.ToString(),
                item.BankConnection.BankBrandName,
                item.BankConnection.MaskedAccountNumber,
                item.ExternalTransactionId,
                item.Direction.ToString(),
                item.Amount,
                item.Currency,
                item.TransactionDate,
                item.Content,
                item.ReferenceCode,
                item.PaymentCode,
                item.Gateway,
                item.Source.ToString(),
                item.ImportedAt))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                "bank_transaction_not_found",
                "Bank transaction not found.");
    }
}
