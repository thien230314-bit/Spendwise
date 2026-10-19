using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankIntegration.Commands.CreateSePaySandboxTransaction;

public sealed class CreateSePaySandboxTransactionCommandHandler(
    ISpendWiseDbContext db,
    IBankHubGateway gateway,
    IBankHubConfiguration configuration)
    : IRequestHandler<CreateSePaySandboxTransactionCommand, BankSandboxTransactionAcceptedDto>
{
    public async Task<BankSandboxTransactionAcceptedDto> Handle(
        CreateSePaySandboxTransactionCommand command,
        CancellationToken cancellationToken)
    {
        // Hai điều kiện độc lập ngăn simulator xuất hiện ở ASP.NET Production hoặc SePay Production.
        if (!command.IsDevelopment || !configuration.IsSandbox)
        {
            throw new ForbiddenException("sepay_sandbox_forbidden", "SePay Sandbox simulator is available only in Development Sandbox mode.");
        }
        configuration.EnsureAvailable();
        InputBoundary.PositiveMoney(command.Request.Amount);
        var content = InputBoundary.RequiredText(command.Request.Content, 500, "Content");
        if (!TryDirection(command.Request.TransferType, out var direction))
        {
            throw new AppException("bank_transaction_invalid", "Transfer type must be credit or debit.");
        }

        var connection = await db.BankConnections.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == command.Request.BankConnectionId
                && item.Provider == BankProvider.SePayBankHub
                && item.Status == BankConnectionStatus.Active,
            cancellationToken) ?? throw BankingErrors.ConnectionNotFound();

        await gateway.CreateSandboxTransactionAsync(
            connection.ExternalAccountId,
            direction,
            command.Request.Amount,
            content,
            cancellationToken);
        return new BankSandboxTransactionAcceptedDto(true, BankProvider.SePayBankHub.ToString(), "Sandbox");
    }

    private static bool TryDirection(string? value, out BankTransactionDirection direction)
    {
        direction = value?.Trim().ToLowerInvariant() switch
        {
            "credit" => BankTransactionDirection.Credit,
            "debit" => BankTransactionDirection.Debit,
            _ => default
        };
        return value?.Trim().ToLowerInvariant() is "credit" or "debit";
    }
}
