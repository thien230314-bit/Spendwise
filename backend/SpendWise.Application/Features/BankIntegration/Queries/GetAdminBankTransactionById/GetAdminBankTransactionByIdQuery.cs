using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankTransactionById;

// SystemAdmin tra cứu trực tiếp theo BankTransaction Id, không áp dụng ownership của User endpoint.
public sealed record GetAdminBankTransactionByIdQuery(
    Guid BankTransactionId) : IRequest<AdminBankTransactionDto>;
