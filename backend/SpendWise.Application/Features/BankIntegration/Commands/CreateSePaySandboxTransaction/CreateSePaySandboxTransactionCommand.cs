using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Commands.CreateSePaySandboxTransaction;

// Command chỉ yêu cầu provider Sandbox tạo giao dịch; không insert BankTransaction cục bộ.
public sealed record CreateSePaySandboxTransactionCommand(
    bool IsDevelopment,
    CreateBankSandboxTransactionRequest Request) : IRequest<BankSandboxTransactionAcceptedDto>;
