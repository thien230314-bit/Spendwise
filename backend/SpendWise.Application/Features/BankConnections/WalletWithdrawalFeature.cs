using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;
using SpendWise.Application.Finance;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.BankConnections;

public sealed record CreateWalletWithdrawalRequest(
    Guid BankConnectionId,
    Guid WalletId,
    decimal Amount,
    string SecurityPin,
    string IdempotencyKey);

public sealed record WalletWithdrawalDto(
    Guid Id,
    Guid BankConnectionId,
    Guid WalletId,
    decimal Amount,
    string Status,
    string MaskedAccountNumber,
    string Reference,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public sealed record CreateWalletWithdrawalCommand(
    bool IsDevelopment,
    Guid UserId,
    CreateWalletWithdrawalRequest Request) : IRequest<WalletWithdrawalDto>;

public sealed record GetWalletWithdrawalsQuery(
    bool IsDevelopment,
    Guid UserId) : IRequest<IReadOnlyList<WalletWithdrawalDto>>;

public sealed record GetWalletWithdrawalByIdQuery(
    bool IsDevelopment,
    Guid UserId,
    Guid Id) : IRequest<WalletWithdrawalDto>;

public sealed class CreateWalletWithdrawalCommandHandler(
    ISpendWiseDbContext db,
    IBankHubGateway gateway,
    ILedgerService ledger,
    IClock clock,
    FinancialSecurityGuard security,
    IActiveBankGuard activeBankGuard) : IRequestHandler<CreateWalletWithdrawalCommand, WalletWithdrawalDto>
{
    public async Task<WalletWithdrawalDto> Handle(
        CreateWalletWithdrawalCommand command,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(command.IsDevelopment);
        var request = command.Request;

        InputBoundary.PositiveMoney(request.Amount);
        if (decimal.Truncate(request.Amount) != request.Amount)
            throw new AppException("invalid_amount", "Withdrawal amount must be a whole number of VND.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100)
            throw new AppException("invalid_idempotency_key", "Idempotency key is required and limited to 100 characters.");

        var normalizedKey = request.IdempotencyKey.Trim();

        await activeBankGuard.EnsureActiveBankAsync(command.UserId, cancellationToken);
        await security.VerifyAsync(command.UserId, request.SecurityPin, cancellationToken);

        // Check idempotency replay first
        var existingReplay = await db.WalletWithdrawals
            .Include(item => item.BankConnection)
            .SingleOrDefaultAsync(item => item.UserId == command.UserId && item.IdempotencyKey == normalizedKey, cancellationToken);

        if (existingReplay is not null)
        {
            if (existingReplay.BankConnectionId != request.BankConnectionId
                || existingReplay.WalletId != request.WalletId
                || existingReplay.Amount != request.Amount)
            {
                throw new ConflictException("idempotency_conflict", "Idempotency key was used with different withdrawal details.");
            }

            return ToDto(existingReplay, existingReplay.BankConnection?.MaskedAccountNumber ?? "");
        }

        var connection = await db.BankConnections.SingleOrDefaultAsync(item =>
            item.Id == request.BankConnectionId
            && item.UserId == command.UserId
            && item.Provider == BankProvider.SePayBankHub
            && item.Status == BankConnectionStatus.Active, cancellationToken)
            ?? throw BankingErrors.ConnectionNotFound();

        var wallet = await db.Wallets
            .Include(w => w.LedgerAccount)
            .SingleOrDefaultAsync(w => w.Id == request.WalletId && w.OwnerUserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");

        if (wallet.Status != WalletStatus.Active)
            throw new ConflictException("wallet_unavailable", "Wallet must be Active to withdraw funds.");

        // Check wallet balance
        var walletLedger = await db.LedgerAccounts.SingleOrDefaultAsync(
            a => a.WalletId == wallet.Id && a.Type == LedgerAccountType.Wallet, cancellationToken)
            ?? throw new ConflictException("wallet_unavailable", "Wallet ledger account not found.");

        if (walletLedger.Balance < request.Amount)
            throw new ConflictException("insufficient_wallet_balance", "Wallet does not have enough balance for withdrawal.");

        var reference = "SWWD" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

        // Create Pending withdrawal record
        var withdrawal = new WalletWithdrawal
        {
            UserId = command.UserId,
            BankConnectionId = connection.Id,
            WalletId = wallet.Id,
            Amount = request.Amount,
            Status = WalletWithdrawalStatus.Pending,
            Reference = reference,
            IdempotencyKey = normalizedKey,
            CreatedAt = clock.UtcNow
        };
        db.WalletWithdrawals.Add(withdrawal);
        await db.SaveChangesAsync(cancellationToken);

        // Call SePay Sandbox simulator: POST /v1/transaction/create
        // simulating credit into the user's linked bank account
        bool simulationSuccess = false;
        string? failureReason = null;
        try
        {
            await gateway.CreateSandboxTransactionAsync(
                connection.ExternalAccountId,
                BankTransactionDirection.Credit,
                request.Amount,
                $"SpendWise Withdrawal {reference}",
                cancellationToken);
            simulationSuccess = true;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            simulationSuccess = false;
        }

        if (!simulationSuccess)
        {
            withdrawal.Status = WalletWithdrawalStatus.Failed;
            withdrawal.FailureReason = failureReason ?? "SePay Sandbox transaction creation failed";
            await db.SaveChangesAsync(cancellationToken);
            throw new ConflictException("withdrawal_failed", "Thực hiện rút tiền về tài khoản ngân hàng thất bại từ cổng thanh toán.");
        }

        // Post ledger entries to settle withdrawal: Debit Wallet, Credit External Clearing Account
        var clearingAccount = await GetOrCreateExternalSettlementAccountAsync(db, cancellationToken);
        var posting = await ledger.PostAsync(
            walletLedger.Id,
            clearingAccount.Id,
            request.Amount,
            "VND",
            "WalletWithdrawal",
            withdrawal.Id,
            $"withdrawal:{withdrawal.Id:N}",
            normalizedKey,
            $"Rút tiền về {connection.BankBrandName} {connection.MaskedAccountNumber}",
            cancellationToken);

        withdrawal.Status = WalletWithdrawalStatus.Completed;
        withdrawal.LedgerTransactionId = posting.LedgerTransactionId;
        withdrawal.CompletedAt = clock.UtcNow;

        db.FinancialTransactions.Add(new FinancialTransaction
        {
            OwnerUserId = command.UserId,
            WalletId = wallet.Id,
            LedgerTransactionId = posting.LedgerTransactionId,
            RelatedBusinessId = withdrawal.Id,
            Type = FinancialTransactionType.TransferOut,
            Amount = request.Amount,
            Description = $"Rút tiền về {connection.BankBrandName} {connection.MaskedAccountNumber}",
            OccurredAt = clock.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(withdrawal, connection.MaskedAccountNumber);
    }

    private static async Task<LedgerAccount> GetOrCreateExternalSettlementAccountAsync(
        ISpendWiseDbContext db, CancellationToken cancellationToken)
    {
        const string code = "SYSTEM:ExternalSettlement:VND";
        var account = await db.LedgerAccounts.SingleOrDefaultAsync(a => a.Code == code, cancellationToken);
        if (account is not null) return account;

        account = new LedgerAccount
        {
            Code = code,
            Name = "External Settlement Account",
            Type = LedgerAccountType.ExternalSource,
            Currency = "VND",
            IsSystem = true
        };
        db.LedgerAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    private static WalletWithdrawalDto ToDto(WalletWithdrawal withdrawal, string maskedAccount) => new(
        withdrawal.Id,
        withdrawal.BankConnectionId,
        withdrawal.WalletId,
        withdrawal.Amount,
        withdrawal.Status.ToString(),
        maskedAccount,
        withdrawal.Reference,
        withdrawal.CreatedAt,
        withdrawal.CompletedAt);
}

public sealed class GetWalletWithdrawalsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetWalletWithdrawalsQuery, IReadOnlyList<WalletWithdrawalDto>>
{
    public async Task<IReadOnlyList<WalletWithdrawalDto>> Handle(
        GetWalletWithdrawalsQuery request,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(request.IsDevelopment);
        var items = await db.WalletWithdrawals
            .AsNoTracking()
            .Include(w => w.BankConnection)
            .Where(w => w.UserId == request.UserId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(w => new WalletWithdrawalDto(
            w.Id,
            w.BankConnectionId,
            w.WalletId,
            w.Amount,
            w.Status.ToString(),
            w.BankConnection?.MaskedAccountNumber ?? "",
            w.Reference,
            w.CreatedAt,
            w.CompletedAt)).ToList();
    }
}

public sealed class GetWalletWithdrawalByIdQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetWalletWithdrawalByIdQuery, WalletWithdrawalDto>
{
    public async Task<WalletWithdrawalDto> Handle(
        GetWalletWithdrawalByIdQuery request,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(request.IsDevelopment);
        var item = await db.WalletWithdrawals
            .AsNoTracking()
            .Include(w => w.BankConnection)
            .SingleOrDefaultAsync(w => w.Id == request.Id && w.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("wallet_withdrawal_not_found", "Wallet withdrawal not found.");

        return new WalletWithdrawalDto(
            item.Id,
            item.BankConnectionId,
            item.WalletId,
            item.Amount,
            item.Status.ToString(),
            item.BankConnection?.MaskedAccountNumber ?? "",
            item.Reference,
            item.CreatedAt,
            item.CompletedAt);
    }
}
