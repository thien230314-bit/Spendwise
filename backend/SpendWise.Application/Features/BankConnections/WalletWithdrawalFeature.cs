using System.Security.Cryptography;
using System.Text.Json;
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
    DateTime? CompletedAt,
    string? FailureReason = null);

public sealed record CreateWalletWithdrawalCommand(
    bool IsDevelopment,
    Guid UserId,
    CreateWalletWithdrawalRequest Request) : IRequest<WalletWithdrawalDto>;

public sealed record ConfirmTestModeWalletWithdrawalCommand(
    bool IsDevelopment,
    Guid UserId,
    Guid WithdrawalId) : IRequest<WalletWithdrawalDto>;

public sealed record CancelTestModeWalletWithdrawalCommand(
    bool IsDevelopment,
    Guid UserId,
    Guid WithdrawalId,
    string? Reason = null) : IRequest<WalletWithdrawalDto>;

public sealed record GetWalletWithdrawalsQuery(
    bool IsDevelopment,
    Guid UserId) : IRequest<IReadOnlyList<WalletWithdrawalDto>>;

public sealed record GetWalletWithdrawalByIdQuery(
    bool IsDevelopment,
    Guid UserId,
    Guid Id) : IRequest<WalletWithdrawalDto>;

public enum WithdrawalReconciliationAction
{
    FinalizeSuccess,
    ReleaseFailure,
    QueryProvider
}

public sealed record ReconcileWalletWithdrawalCommand(
    Guid WithdrawalId,
    WithdrawalReconciliationAction Action,
    string? ProviderTransactionId = null,
    string? Reason = null,
    string? FailureEvidence = null) : IRequest<WalletWithdrawalDto>;

public sealed class CreateWalletWithdrawalCommandHandler(
    ISpendWiseDbContext db,
    IBankHubGateway gateway,
    ILedgerService ledger,
    IClock clock,
    FinancialSecurityGuard security,
    IActiveBankGuard activeBankGuard,
    ISePayTestModeAccountReader? testModeReader = null) : IRequestHandler<CreateWalletWithdrawalCommand, WalletWithdrawalDto>
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

        var activeWithdrawal = await db.WalletWithdrawals.AnyAsync(
            w => w.BankConnectionId == connection.Id && (w.Status == WalletWithdrawalStatus.Pending || w.Status == WalletWithdrawalStatus.Processing),
            cancellationToken);
        if (activeWithdrawal)
        {
            throw new ConflictException("active_withdrawal_in_progress", "A withdrawal is already in progress for this bank connection.");
        }

        if (connection.AccountEnvironment == SePayAccountEnvironment.TestMode)
        {
            return await HandleTestModeWithdrawalAsync(
                command,
                request,
                normalizedKey,
                connection,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(connection.ExternalAccountId))
        {
            throw new ConflictException("invalid_bank_account_identity", "Tài khoản ngân hàng liên kết thiếu định danh hợp lệ.");
        }

        var walletCheck = await db.Wallets.AsNoTracking().SingleOrDefaultAsync(w => w.Id == request.WalletId && w.OwnerUserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");

        if (walletCheck.Status != WalletStatus.Active)
            throw new ConflictException("wallet_unavailable", "Wallet must be Active to withdraw funds.");

        var reference = "SWWD" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

        // Ensure system reservation clearing account exists
        var holdAccount = await GetOrCreateReservationHoldAccountAsync(db, cancellationToken);

        // Step 1: Atomic Database Reservation (Commit before HTTP call)
        var withdrawal = await db.ExecuteInTransactionAsync(async token =>
        {
            var wallet = await db.Wallets
                .Include(w => w.LedgerAccount)
                .SingleOrDefaultAsync(w => w.Id == request.WalletId && w.OwnerUserId == command.UserId, token)
                ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");

            if (wallet.Status != WalletStatus.Active)
                throw new ConflictException("wallet_unavailable", "Wallet must be Active to withdraw funds.");

            var walletLedger = await db.LedgerAccounts.SingleOrDefaultAsync(
                a => a.WalletId == wallet.Id && a.Type == LedgerAccountType.Wallet, token)
                ?? throw new ConflictException("wallet_unavailable", "Wallet ledger account not found.");

            if (walletLedger.Balance < request.Amount)
                throw new ConflictException("insufficient_wallet_balance", "Wallet does not have enough balance for withdrawal.");

            var newWithdrawal = new WalletWithdrawal
            {
                UserId = command.UserId,
                BankConnectionId = connection.Id,
                WalletId = wallet.Id,
                Amount = request.Amount,
                Status = WalletWithdrawalStatus.Reserved,
                Reference = reference,
                IdempotencyKey = normalizedKey,
                ReservedAt = clock.UtcNow,
                CreatedAt = clock.UtcNow
            };

            // Hold funds in reservation clearing account
            var holdPosting = await ledger.PostAsync(
                walletLedger.Id,
                holdAccount.Id,
                request.Amount,
                "VND",
                "WithdrawalReservation",
                newWithdrawal.Id,
                $"wd-hold:{newWithdrawal.Id:N}",
                normalizedKey + ":hold",
                $"Tạm giữ số dư rút tiền {reference}",
                token);

            newWithdrawal.ReservationLedgerTransactionId = holdPosting.LedgerTransactionId;
            db.WalletWithdrawals.Add(newWithdrawal);
            await db.SaveChangesAsync(token);
            return newWithdrawal;
        }, cancellationToken);

        // Step 2: Transition to Processing & Call Provider outside DB transaction
        withdrawal.Status = WalletWithdrawalStatus.Processing;
        await db.SaveChangesAsync(cancellationToken);

        SandboxTransactionResult result;
        try
        {
            result = await gateway.CreateSandboxTransactionAsync(
                connection.ExternalAccountId,
                BankTransactionDirection.Credit,
                request.Amount,
                $"SpendWise Withdrawal {reference}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            result = new SandboxTransactionResult(
                Success: false,
                ProviderTransactionId: null,
                ReferenceNumber: null,
                TransactionDate: null,
                Amount: request.Amount,
                Direction: "Credit",
                ExternalBankAccountId: connection.ExternalAccountId,
                ProviderStatus: "Exception",
                ErrorCode: "provider_exception",
                ErrorMessage: ex.Message,
                IsTimeoutOrUnknown: true);
        }

        // Step 3: Handle Outcome atomically
        var holdAccountFinal = await GetOrCreateReservationHoldAccountAsync(db, cancellationToken);
        var walletLedgerFinal = await db.LedgerAccounts.SingleAsync(
            a => a.WalletId == withdrawal.WalletId && a.Type == LedgerAccountType.Wallet, cancellationToken);

        if (result.Success)
        {
            try
            {
                return await db.ExecuteInTransactionAsync(async token =>
                {
                    if (!string.IsNullOrWhiteSpace(result.ProviderTransactionId))
                    {
                        var isDuplicateTxId = await db.WalletWithdrawals.AnyAsync(
                            w => w.ProviderTransactionId == result.ProviderTransactionId && w.Id != withdrawal.Id, token);
                        if (isDuplicateTxId)
                        {
                            withdrawal.Status = WalletWithdrawalStatus.PendingReconciliation;
                            withdrawal.FailureReason = "Phát hiện ProviderTransactionId bị trùng lặp giữa các giao dịch rút tiền.";
                            withdrawal.ProviderResponseData = SerializeProviderResponse(result);
                            await db.SaveChangesAsync(token);
                            throw new ConflictException("duplicate_provider_transaction", "Phát hiện ProviderTransactionId bị trùng lặp.");
                        }
                    }

                    var clearingAccount = await GetOrCreateExternalSettlementAccountAsync(db, token);
                    var posting = await ledger.PostAsync(
                        holdAccountFinal.Id,
                        clearingAccount.Id,
                        request.Amount,
                        "VND",
                        "WalletWithdrawal",
                        withdrawal.Id,
                        $"withdrawal:{withdrawal.Id:N}",
                        normalizedKey,
                        $"Rút tiền về {connection.BankBrandName} {connection.MaskedAccountNumber}",
                        token);

                    withdrawal.Status = WalletWithdrawalStatus.Completed;
                    withdrawal.LedgerTransactionId = posting.LedgerTransactionId;
                    withdrawal.ProviderTransactionId = result.ProviderTransactionId;
                    withdrawal.CompletedAt = clock.UtcNow;
                    withdrawal.ProviderResponseData = SerializeProviderResponse(result);

                    db.FinancialTransactions.Add(new FinancialTransaction
                    {
                        OwnerUserId = command.UserId,
                        WalletId = withdrawal.WalletId,
                        LedgerTransactionId = posting.LedgerTransactionId,
                        RelatedBusinessId = withdrawal.Id,
                        Type = FinancialTransactionType.TransferOut,
                        Amount = request.Amount,
                        Description = $"Rút tiền về {connection.BankBrandName} {connection.MaskedAccountNumber}",
                        OccurredAt = clock.UtcNow
                    });

                    await db.SaveChangesAsync(token);
                    return ToDto(withdrawal, connection.MaskedAccountNumber);
                }, cancellationToken);
            }
            catch (Exception ex) when (ex is not ConflictException)
            {
                await db.ExecuteInTransactionAsync(async token =>
                {
                    withdrawal.Status = WalletWithdrawalStatus.PendingReconciliation;
                    withdrawal.ProviderTransactionId = result.ProviderTransactionId;
                    withdrawal.FailureReason = $"Provider thành công nhưng lỗi hạch toán sổ cái: {ex.Message}";
                    withdrawal.ProviderResponseData = SerializeProviderResponse(result);
                    await db.SaveChangesAsync(token);
                    return true;
                }, CancellationToken.None);

                throw new AppException("withdrawal_pending_reconciliation", "Giao dịch đã được provider tiếp nhận nhưng đang chờ đối soát sổ cái.", 504);
            }
        }

        if (result.IsTimeoutOrUnknown)
        {
            await db.ExecuteInTransactionAsync(async token =>
            {
                withdrawal.Status = WalletWithdrawalStatus.PendingReconciliation;
                withdrawal.FailureReason = result.ErrorMessage ?? "Giao dịch quá thời gian chờ, đang chờ đối soát kết quả từ ngân hàng.";
                withdrawal.ProviderResponseData = SerializeProviderResponse(result);
                await db.SaveChangesAsync(token);
                return true;
            }, cancellationToken);

            throw new AppException("withdrawal_pending_reconciliation", "Giao dịch đang chờ đối soát do chưa nhận được kết quả xác nhận cuối cùng từ ngân hàng.", 504);
        }

        // Definitive failure: release reservation hold back to wallet
        await db.ExecuteInTransactionAsync(async token =>
        {
            await ledger.PostAsync(
                holdAccountFinal.Id,
                walletLedgerFinal.Id,
                request.Amount,
                "VND",
                "WithdrawalReservationRelease",
                withdrawal.Id,
                $"wd-release:{withdrawal.Id:N}",
                normalizedKey + ":release",
                $"Hoàn trả số dư tạm giữ rút tiền {reference}",
                token);

            withdrawal.Status = WalletWithdrawalStatus.Failed;
            withdrawal.FailureReason = result.ErrorMessage ?? "Thực hiện rút tiền về tài khoản ngân hàng thất bại từ cổng thanh toán.";
            withdrawal.FailedAt = clock.UtcNow;
            withdrawal.ProviderResponseData = SerializeProviderResponse(result);
            await db.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

        throw new ConflictException("withdrawal_failed", result.ErrorMessage ?? "Thực hiện rút tiền về tài khoản ngân hàng thất bại từ cổng thanh toán.");
    }

    private async Task<WalletWithdrawalDto> HandleTestModeWithdrawalAsync(
        CreateWalletWithdrawalCommand command,
        CreateWalletWithdrawalRequest request,
        string normalizedKey,
        BankConnection connection,
        CancellationToken cancellationToken)
    {
        var walletCheck = await db.Wallets.AsNoTracking().SingleOrDefaultAsync(
            w => w.Id == request.WalletId && w.OwnerUserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");

        if (walletCheck.Status != WalletStatus.Active)
            throw new ConflictException("wallet_unavailable", "Wallet must be Active to withdraw funds.");

        var reference = TestModeSePaySupport.CreateReference("SWWD");
        var holdAccount = await GetOrCreateReservationHoldAccountAsync(db, cancellationToken);
        var clearingAccount = await GetOrCreateExternalSettlementAccountAsync(db, cancellationToken);

        var withdrawal = await db.ExecuteInTransactionAsync(async token =>
        {
            var wallet = await db.Wallets
                .Include(w => w.LedgerAccount)
                .SingleOrDefaultAsync(w => w.Id == request.WalletId && w.OwnerUserId == command.UserId, token)
                ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");

            if (wallet.Status != WalletStatus.Active)
                throw new ConflictException("wallet_unavailable", "Wallet must be Active to withdraw funds.");

            var walletLedger = await db.LedgerAccounts.SingleOrDefaultAsync(
                a => a.WalletId == wallet.Id && a.Type == LedgerAccountType.Wallet, token)
                ?? throw new ConflictException("wallet_unavailable", "Wallet ledger account not found.");

            if (walletLedger.Balance < request.Amount)
                throw new ConflictException("insufficient_wallet_balance", "Wallet does not have enough balance for withdrawal.");

            var newWithdrawal = new WalletWithdrawal
            {
                UserId = command.UserId,
                BankConnectionId = connection.Id,
                WalletId = wallet.Id,
                Amount = request.Amount,
                Status = WalletWithdrawalStatus.Reserved,
                Reference = reference,
                IdempotencyKey = normalizedKey,
                ReservedAt = clock.UtcNow,
                CreatedAt = clock.UtcNow
            };

            var holdPosting = await ledger.PostAsync(
                walletLedger.Id,
                holdAccount.Id,
                request.Amount,
                "VND",
                "WithdrawalReservation",
                newWithdrawal.Id,
                $"wd-hold:{newWithdrawal.Id:N}",
                normalizedKey + ":hold",
                $"Tạm giữ số dư rút tiền {reference}",
                token);

            newWithdrawal.ReservationLedgerTransactionId = holdPosting.LedgerTransactionId;
            newWithdrawal.Status = WalletWithdrawalStatus.Processing;

            db.WalletWithdrawals.Add(newWithdrawal);
            await db.SaveChangesAsync(token);
            return newWithdrawal;
        }, cancellationToken);

        return ToDto(withdrawal, connection.MaskedAccountNumber);
    }

    private static Task<LedgerAccount> GetOrCreateReservationHoldAccountAsync(
        ISpendWiseDbContext db, CancellationToken cancellationToken) =>
        WalletWithdrawalHelpers.GetOrCreateReservationHoldAccountAsync(db, cancellationToken);

    private static Task<LedgerAccount> GetOrCreateExternalSettlementAccountAsync(
        ISpendWiseDbContext db, CancellationToken cancellationToken) =>
        WalletWithdrawalHelpers.GetOrCreateExternalSettlementAccountAsync(db, cancellationToken);

    private static WalletWithdrawalDto ToDto(WalletWithdrawal withdrawal, string maskedAccount) =>
        WalletWithdrawalHelpers.ToDto(withdrawal, maskedAccount);

    private static string SerializeProviderResponse(SandboxTransactionResult result) =>
        WalletWithdrawalHelpers.SerializeProviderResponse(result);
}

public sealed class ConfirmTestModeWalletWithdrawalCommandHandler(
    ISpendWiseDbContext db,
    ISePayTestModeAccountReader provider,
    ILedgerService ledger,
    IClock clock) : IRequestHandler<ConfirmTestModeWalletWithdrawalCommand, WalletWithdrawalDto>
{
    public async Task<WalletWithdrawalDto> Handle(
        ConfirmTestModeWalletWithdrawalCommand command,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(command.IsDevelopment);
        var snapshot = await db.WalletWithdrawals.AsNoTracking()
            .Include(item => item.BankConnection)
            .SingleOrDefaultAsync(item => item.Id == command.WithdrawalId && item.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("wallet_withdrawal_not_found", "Wallet withdrawal not found.");

        if (snapshot.Status == WalletWithdrawalStatus.Completed)
            return WalletWithdrawalHelpers.ToDto(snapshot, snapshot.BankConnection?.MaskedAccountNumber ?? "");

        if (snapshot.Status != WalletWithdrawalStatus.Processing && snapshot.Status != WalletWithdrawalStatus.Reserved)
            throw new ConflictException("wallet_withdrawal_unavailable", "Wallet withdrawal is no longer pending or processing.");

        if (snapshot.BankConnection == null || snapshot.BankConnection.AccountEnvironment != SePayAccountEnvironment.TestMode)
            throw new ConflictException("bank_connection_invalid", "Withdrawal does not belong to a SePay Test Mode connection.");

        if (!Guid.TryParse(snapshot.BankConnection.ExternalAccountId, out var providerAccountId))
            throw new ConflictException("bank_connection_invalid", "The SePay account identifier is invalid.");

        var matches = (await provider.GetTransactionsAsync(snapshot.Reference, cancellationToken))
            .Where(item => item.ProviderAccountId == providerAccountId
                && item.Direction == "in"
                && item.AmountIn == snapshot.Amount
                && item.AmountOut == 0
                && TestModeSePaySupport.ContainsExactReference(item, snapshot.Reference))
            .ToList();

        if (matches.Count == 0)
            throw new ConflictException("wallet_withdrawal_pending", "No exact SePay settlement transaction was found yet.");
        if (matches.Count != 1)
            throw new ConflictException("wallet_withdrawal_ambiguous", "Multiple exact SePay settlement transactions were found.");

        var settlement = matches[0];

        return await db.ExecuteInTransactionAsync(async token =>
        {
            var isDuplicateTxId = await db.WalletWithdrawals.AnyAsync(
                w => w.ProviderTransactionId == settlement.ProviderTransactionId.ToString() && w.Id != snapshot.Id, token);
            if (isDuplicateTxId)
            {
                throw new ConflictException("duplicate_provider_transaction", "Phát hiện ProviderTransactionId bị trùng lặp.");
            }

            var withdrawal = await db.WalletWithdrawals
                .Include(w => w.BankConnection)
                .SingleAsync(w => w.Id == snapshot.Id, token);

            var holdAccount = await WalletWithdrawalHelpers.GetOrCreateReservationHoldAccountAsync(db, token);
            var clearingAccount = await WalletWithdrawalHelpers.GetOrCreateExternalSettlementAccountAsync(db, token);

            var posting = await ledger.PostAsync(
                holdAccount.Id,
                clearingAccount.Id,
                withdrawal.Amount,
                "VND",
                "WalletWithdrawal",
                withdrawal.Id,
                $"withdrawal:{withdrawal.Id:N}",
                withdrawal.IdempotencyKey,
                $"Rút tiền về {withdrawal.BankConnection.BankBrandName} {withdrawal.BankConnection.MaskedAccountNumber}",
                token);

            withdrawal.Status = WalletWithdrawalStatus.Completed;
            withdrawal.LedgerTransactionId = posting.LedgerTransactionId;
            withdrawal.ProviderTransactionId = settlement.ProviderTransactionId.ToString();
            withdrawal.CompletedAt = clock.UtcNow;

            var financialTx = new FinancialTransaction
            {
                OwnerUserId = command.UserId,
                WalletId = withdrawal.WalletId,
                LedgerTransactionId = posting.LedgerTransactionId,
                RelatedBusinessId = withdrawal.Id,
                Type = FinancialTransactionType.TransferOut,
                Amount = withdrawal.Amount,
                Description = $"Rút tiền về {withdrawal.BankConnection.BankBrandName} {withdrawal.BankConnection.MaskedAccountNumber}",
                OccurredAt = clock.UtcNow
            };
            db.FinancialTransactions.Add(financialTx);
            await db.SaveChangesAsync(token);
            withdrawal.FinancialTransactionId = financialTx.Id;
            await db.SaveChangesAsync(token);

            return WalletWithdrawalHelpers.ToDto(withdrawal, withdrawal.BankConnection.MaskedAccountNumber);
        }, cancellationToken);
    }
}

public sealed class CancelTestModeWalletWithdrawalCommandHandler(
    ISpendWiseDbContext db,
    ILedgerService ledger,
    IClock clock) : IRequestHandler<CancelTestModeWalletWithdrawalCommand, WalletWithdrawalDto>
{
    public async Task<WalletWithdrawalDto> Handle(
        CancelTestModeWalletWithdrawalCommand command,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(command.IsDevelopment);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            var withdrawal = await db.WalletWithdrawals
                .Include(w => w.BankConnection)
                .SingleOrDefaultAsync(w => w.Id == command.WithdrawalId && w.UserId == command.UserId, token)
                ?? throw new NotFoundException("wallet_withdrawal_not_found", "Wallet withdrawal not found.");

            if (withdrawal.Status != WalletWithdrawalStatus.Processing && withdrawal.Status != WalletWithdrawalStatus.Reserved)
                throw new ConflictException("wallet_withdrawal_not_cancellable", "Only reserved or processing withdrawals can be cancelled.");

            var holdAccount = await WalletWithdrawalHelpers.GetOrCreateReservationHoldAccountAsync(db, token);
            var walletLedger = await db.LedgerAccounts.SingleAsync(
                a => a.WalletId == withdrawal.WalletId && a.Type == LedgerAccountType.Wallet, token);

            await ledger.PostAsync(
                holdAccount.Id,
                walletLedger.Id,
                withdrawal.Amount,
                "VND",
                "WithdrawalReservationRelease",
                withdrawal.Id,
                $"wd-release:{withdrawal.Id:N}",
                withdrawal.IdempotencyKey + ":cancel",
                $"Hủy rút tiền {withdrawal.Reference}: {command.Reason ?? "Người dùng hủy"}",
                token);

            withdrawal.Status = WalletWithdrawalStatus.Cancelled;
            withdrawal.FailedAt = clock.UtcNow;
            withdrawal.FailureReason = command.Reason ?? "Cancelled by user";
            await db.SaveChangesAsync(token);

            return WalletWithdrawalHelpers.ToDto(withdrawal, withdrawal.BankConnection?.MaskedAccountNumber ?? "");
        }, cancellationToken);
    }
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
            w.CompletedAt,
            w.FailureReason)).ToList();
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
            item.CompletedAt,
            item.FailureReason);
    }
}

public sealed class ReconcileWalletWithdrawalCommandHandler(
    ISpendWiseDbContext db,
    ILedgerService ledger,
    IClock clock) : IRequestHandler<ReconcileWalletWithdrawalCommand, WalletWithdrawalDto>
{
    public async Task<WalletWithdrawalDto> Handle(
        ReconcileWalletWithdrawalCommand command,
        CancellationToken cancellationToken)
    {
        return await db.ExecuteInTransactionAsync(async token =>
        {
            // Atomic Compare-And-Swap: claim reconciliation by transitioning to Processing
            var rowsUpdated = await db.WalletWithdrawals
                .Where(w => w.Id == command.WithdrawalId &&
                    (w.Status == WalletWithdrawalStatus.PendingReconciliation || w.Status == WalletWithdrawalStatus.Reserved))
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Status, WalletWithdrawalStatus.Processing), token);

            if (rowsUpdated == 0)
            {
                var existing = await db.WalletWithdrawals
                    .AsNoTracking()
                    .Include(w => w.BankConnection)
                    .SingleOrDefaultAsync(w => w.Id == command.WithdrawalId, token)
                    ?? throw new NotFoundException("wallet_withdrawal_not_found", "Wallet withdrawal not found.");

                // Idempotency: if already Completed or Failed, return current state without re-posting
                if (existing.Status == WalletWithdrawalStatus.Completed || existing.Status == WalletWithdrawalStatus.Failed)
                {
                    return WalletWithdrawalHelpers.ToDto(existing, existing.BankConnection?.MaskedAccountNumber ?? "");
                }

                throw new ConflictException("invalid_withdrawal_status", $"Cannot reconcile withdrawal with status '{existing.Status}'.");
            }

            var withdrawal = await db.WalletWithdrawals
                .Include(w => w.BankConnection)
                .SingleAsync(w => w.Id == command.WithdrawalId, token);

            var holdAccount = await WalletWithdrawalHelpers.GetOrCreateReservationHoldAccountAsync(db, token);
            var normalizedKey = withdrawal.IdempotencyKey ?? $"wd:{withdrawal.Id:N}";

            if (command.Action == WithdrawalReconciliationAction.FinalizeSuccess)
            {
                var providerTxId = command.ProviderTransactionId ?? withdrawal.ProviderTransactionId;
                if (string.IsNullOrWhiteSpace(providerTxId))
                {
                    throw new ConflictException("missing_provider_evidence", "Cannot finalize withdrawal without valid provider transaction ID.");
                }

                if (!string.IsNullOrWhiteSpace(withdrawal.ProviderTransactionId)
                    && !string.IsNullOrWhiteSpace(command.ProviderTransactionId)
                    && !string.Equals(withdrawal.ProviderTransactionId, command.ProviderTransactionId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConflictException("mismatched_provider_transaction", "Mã giao dịch đối soát không khớp với mã giao dịch ban đầu của withdrawal.");
                }

                // Strict provider evidence provenance check:
                // FinalizeSuccess requires structured verified provider evidence:
                // Option A: Direct structured provider response matching this withdrawal
                var hasDirectProviderEvidence = WalletWithdrawalHelpers.VerifyStructuredSuccessEvidence(
                    withdrawal.ProviderResponseData,
                    providerTxId,
                    withdrawal.Amount,
                    withdrawal.BankConnection?.ExternalAccountId);

                // Option B: Verified bank integration event with matching transaction ID, account, amount, and outflow event type
                var hasVerifiedBankEvent = await WalletWithdrawalHelpers.VerifyBankIntegrationEventSuccessAsync(
                    db,
                    providerTxId,
                    withdrawal.BankConnection?.ExternalAccountId,
                    withdrawal.Amount,
                    token);

                if (!hasDirectProviderEvidence && !hasVerifiedBankEvent)
                {
                    throw new ConflictException("unverified_provider_evidence",
                        "Không thể hoàn tất rút tiền: Mã giao dịch provider chưa có bằng chứng xác minh từ cổng thanh toán hoặc webhook (WITHDRAWAL_PROVIDER_SETTLEMENT_EVIDENCE_UNAVAILABLE).");
                }

                // Check duplicate provider transaction ID
                var isDuplicateTxId = await db.WalletWithdrawals.AnyAsync(
                    w => w.ProviderTransactionId == providerTxId && w.Id != withdrawal.Id, token);
                if (isDuplicateTxId)
                {
                    throw new ConflictException("duplicate_provider_transaction", "ProviderTransactionId is already used by another withdrawal.");
                }

                var clearingAccount = await WalletWithdrawalHelpers.GetOrCreateExternalSettlementAccountAsync(db, token);
                var posting = await ledger.PostAsync(
                    holdAccount.Id,
                    clearingAccount.Id,
                    withdrawal.Amount,
                    "VND",
                    "WalletWithdrawal",
                    withdrawal.Id,
                    $"withdrawal:{withdrawal.Id:N}",
                    normalizedKey + ":reconciled",
                    $"Đối soát thành công rút tiền về {withdrawal.BankConnection?.BankBrandName} {withdrawal.BankConnection?.MaskedAccountNumber}",
                    token);

                withdrawal.Status = WalletWithdrawalStatus.Completed;
                withdrawal.LedgerTransactionId = posting.LedgerTransactionId;
                withdrawal.ProviderTransactionId = providerTxId;
                withdrawal.CompletedAt = clock.UtcNow;
                withdrawal.FailureReason = null;

                db.FinancialTransactions.Add(new FinancialTransaction
                {
                    OwnerUserId = withdrawal.UserId,
                    WalletId = withdrawal.WalletId,
                    LedgerTransactionId = posting.LedgerTransactionId,
                    RelatedBusinessId = withdrawal.Id,
                    Type = FinancialTransactionType.TransferOut,
                    Amount = withdrawal.Amount,
                    Description = $"Rút tiền (đối soát) về {withdrawal.BankConnection?.BankBrandName} {withdrawal.BankConnection?.MaskedAccountNumber}",
                    OccurredAt = clock.UtcNow
                });

                await db.SaveChangesAsync(token);
                return WalletWithdrawalHelpers.ToDto(withdrawal, withdrawal.BankConnection?.MaskedAccountNumber ?? "");
            }
            else if (command.Action == WithdrawalReconciliationAction.ReleaseFailure)
            {
                if (!string.IsNullOrWhiteSpace(withdrawal.ProviderTransactionId)
                    && !string.IsNullOrWhiteSpace(command.ProviderTransactionId)
                    && !string.Equals(withdrawal.ProviderTransactionId, command.ProviderTransactionId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConflictException("mismatched_provider_transaction", "Mã giao dịch đối soát không khớp với mã giao dịch ban đầu của withdrawal.");
                }

                // P0-1: ReleaseFailure strictly requires verified definitive failure evidence with authentic provider provenance
                var (hasDefinitiveFailure, failureReason) = await WalletWithdrawalHelpers.VerifyDefinitiveFailureEvidenceAsync(
                    db,
                    command.FailureEvidence,
                    withdrawal.ProviderResponseData,
                    command.ProviderTransactionId ?? withdrawal.ProviderTransactionId,
                    withdrawal.BankConnection?.ExternalAccountId,
                    withdrawal.Amount,
                    token);

                if (!hasDefinitiveFailure)
                {
                    throw new ConflictException("unverified_provider_failure_evidence",
                        "Không thể giải tỏa tiền tạm giữ: Thiếu bằng chứng thất bại dứt khoát từ phía ngân hàng/nhà cung cấp có đối soát danh tính hợp lệ.");
                }

                var walletLedger = await db.LedgerAccounts.SingleOrDefaultAsync(
                    a => a.OwnerUserId == withdrawal.UserId && a.WalletId == withdrawal.WalletId && a.Currency == "VND", token)
                    ?? throw new NotFoundException("wallet_ledger_not_found", "Wallet ledger account not found.");

                var noteParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(failureReason))
                {
                    noteParts.Add(failureReason);
                }
                if (!string.IsNullOrWhiteSpace(command.Reason))
                {
                    noteParts.Add($"Note: {command.Reason.Trim()}");
                }
                if (!string.IsNullOrWhiteSpace(command.FailureEvidence))
                {
                    noteParts.Add($"EvidenceRef: {command.FailureEvidence.Trim()}");
                }
                var definitiveReason = noteParts.Count > 0 ? string.Join(" | ", noteParts) : "Giao dịch đối soát xác nhận thất bại từ nhà cung cấp.";

                await ledger.PostAsync(
                    holdAccount.Id,
                    walletLedger.Id,
                    withdrawal.Amount,
                    "VND",
                    "WithdrawalReservationRelease",
                    withdrawal.Id,
                    $"wd-release:{withdrawal.Id:N}",
                    normalizedKey + ":reconciled-release",
                    $"Hoàn trả đối soát số dư tạm giữ rút tiền {withdrawal.Reference}: {definitiveReason}",
                    token);

                withdrawal.Status = WalletWithdrawalStatus.Failed;
                withdrawal.FailureReason = definitiveReason;
                withdrawal.FailedAt = clock.UtcNow;

                await db.SaveChangesAsync(token);
                return WalletWithdrawalHelpers.ToDto(withdrawal, withdrawal.BankConnection?.MaskedAccountNumber ?? "");
            }
            else if (command.Action == WithdrawalReconciliationAction.QueryProvider)
            {
                // SePay Sandbox limitation: automated mock transaction query is not available
                throw new ConflictException("WITHDRAWAL_RECONCILIATION_PROVIDER_LIMITATION",
                    "SePay Bank Hub sandbox does not support automated mock transaction query. Manual evidence or provider callback is required.");
            }
            else
            {
                throw new ConflictException("invalid_reconciliation_action", "Hành động đối soát không hợp lệ.");
            }
        }, cancellationToken);
    }
}

public static class WalletWithdrawalHelpers
{
    public static async Task<LedgerAccount> GetOrCreateReservationHoldAccountAsync(
        ISpendWiseDbContext db, CancellationToken cancellationToken)
    {
        const string code = "SYSTEM:ReservationHold:VND";
        var account = await db.LedgerAccounts.SingleOrDefaultAsync(a => a.Code == code, cancellationToken);
        if (account is not null) return account;

        account = new LedgerAccount
        {
            Code = code,
            Name = "Withdrawal Reservation Hold Account",
            Type = LedgerAccountType.ExternalSource,
            Currency = "VND",
            IsSystem = true
        };
        db.LedgerAccounts.Add(account);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return account;
        }
        catch (DbUpdateException)
        {
            db.ClearTrackedEntities();
            return await db.LedgerAccounts.SingleAsync(a => a.Code == code, cancellationToken);
        }
    }

    public static async Task<LedgerAccount> GetOrCreateExternalSettlementAccountAsync(
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
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return account;
        }
        catch (DbUpdateException)
        {
            db.ClearTrackedEntities();
            return await db.LedgerAccounts.SingleAsync(a => a.Code == code, cancellationToken);
        }
    }

    public static WalletWithdrawalDto ToDto(WalletWithdrawal withdrawal, string maskedAccount) => new(
        withdrawal.Id,
        withdrawal.BankConnectionId,
        withdrawal.WalletId,
        withdrawal.Amount,
        withdrawal.Status.ToString(),
        maskedAccount,
        withdrawal.Reference,
        withdrawal.CreatedAt,
        withdrawal.CompletedAt,
        withdrawal.FailureReason);

    public static string SerializeProviderResponse(SandboxTransactionResult result)
    {
        return JsonSerializer.Serialize(new
        {
            providerTransactionId = result.ProviderTransactionId,
            referenceNumber = result.ReferenceNumber,
            transactionDate = result.TransactionDate,
            amount = result.Amount,
            direction = result.Direction,
            externalBankAccountId = result.ExternalBankAccountId,
            providerStatus = result.ProviderStatus,
            errorCode = result.ErrorCode,
            errorMessage = result.ErrorMessage,
            isTimeoutOrUnknown = result.IsTimeoutOrUnknown
        });
    }

    public static bool VerifyStructuredSuccessEvidence(
        string? providerResponseData,
        string providerTxId,
        decimal expectedAmount,
        string? expectedExternalBankAccountId)
    {
        if (string.IsNullOrWhiteSpace(providerResponseData) ||
            string.IsNullOrWhiteSpace(providerTxId) ||
            expectedAmount <= 0 ||
            string.IsNullOrWhiteSpace(expectedExternalBankAccountId))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(providerResponseData);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            // 1. Transaction ID match
            var txId = GetJsonPropString(root, "providerTransactionId", "transaction_id", "transactionId", "referenceNumber", "id");
            if (string.IsNullOrWhiteSpace(txId) || !string.Equals(txId, providerTxId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 2. Timeout or unknown check
            if (root.TryGetProperty("isTimeoutOrUnknown", out var timeoutProp) && timeoutProp.ValueKind == JsonValueKind.True)
            {
                return false;
            }

            // 3. Success boolean flag (if present, must not be false)
            if (root.TryGetProperty("success", out var successProp) && successProp.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            // 4. Terminal status check: MUST be present and MUST be in verified terminal success list
            var status = GetJsonPropString(root, "providerStatus", "status", "statusCode");
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var upperStatus = status.ToUpperInvariant();
            var validSuccessStatuses = new[] { "SUCCESS", "COMPLETED", "SETTLED", "CONFIRMED" };
            if (!validSuccessStatuses.Contains(upperStatus))
            {
                return false; // Rejects PENDING, CREATED, PROCESSING, SUBMITTED, etc.
            }

            // 5. Amount check: MUST be present, number, and exact match
            if (!root.TryGetProperty("amount", out var amtProp) || amtProp.ValueKind != JsonValueKind.Number)
            {
                return false;
            }
            if (amtProp.GetDecimal() != expectedAmount)
            {
                return false;
            }

            // 6. External bank account match: MUST be present and exact match
            var extAcc = GetJsonPropString(root, "externalBankAccountId", "accountNumber", "bankAccount");
            if (string.IsNullOrWhiteSpace(extAcc) || !string.Equals(extAcc, expectedExternalBankAccountId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 7. Direction check (if present, must be Credit/Outflow)
            var direction = GetJsonPropString(root, "direction");
            if (!string.IsNullOrWhiteSpace(direction))
            {
                var upperDir = direction.ToUpperInvariant();
                if (upperDir is not ("CREDIT" or "OUTFLOW" or "TRANSFER_OUT" or "PAYOUT"))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> VerifyBankIntegrationEventSuccessAsync(
        ISpendWiseDbContext db,
        string providerTxId,
        string? expectedExternalBankAccountId,
        decimal expectedAmount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerTxId) ||
            string.IsNullOrWhiteSpace(expectedExternalBankAccountId) ||
            expectedAmount <= 0)
        {
            return false;
        }

        var validOutflowEventTypes = new[]
        {
            "OUTFLOW_CONFIRMED",
            "OUTFLOW_SETTLED",
            "PAYOUT_SUCCESSFUL",
            "PAYOUT_COMPLETED",
            "PAYMENT_SUCCESSFUL",
            "TRANSFER_OUT_SUCCESS"
        };

        // Strict verification: BankIntegrationEvent must have matching transaction ID,
        // matching external bank account, terminal processed status, valid outflow event type,
        // and must be correlated with an authentic BankTransaction matching exact amount and Debit direction (or verified payload amount).
        var hasMatchingBankTx = await db.BankTransactions.AsNoTracking().AnyAsync(
            bt => bt.ExternalTransactionId == providerTxId
                && bt.Direction == BankTransactionDirection.Debit
                && bt.Amount == expectedAmount,
            cancellationToken);

        if (!hasMatchingBankTx)
        {
            // If BankTransaction is not recorded or amount does not match, BankIntegrationEvent alone is insufficient proof of amount settlement.
            return false;
        }

        return await db.BankIntegrationEvents.AsNoTracking().AnyAsync(
            e => e.ExternalTransactionId == providerTxId
                && e.ExternalBankAccountId == expectedExternalBankAccountId
                && e.ProcessingStatus == BankIntegrationEventStatus.Processed
                && validOutflowEventTypes.Contains(e.EventType),
            cancellationToken);
    }

    public static async Task<(bool IsDefinitiveFailure, string? Reason)> VerifyDefinitiveFailureEvidenceAsync(
        ISpendWiseDbContext db,
        string? failureEvidence,
        string? providerResponseData,
        string? providerTxId,
        string? externalBankAccountId,
        decimal expectedAmount,
        CancellationToken cancellationToken)
    {
        // 1. Caller supplied FailureEvidence:
        // P0-1 FIX: Caller cannot self-certify provider failure!
        // FailureEvidence/Reason passed by caller can ONLY be used as investigative notes,
        // NEVER to decide settlement or release of reservation funds.
        // We do NOT parse or pattern-match caller-supplied failureEvidence to grant release.

        // 2. Previously recorded ProviderResponseData shows definitive failure with trusted provenance:
        if (!string.IsNullOrWhiteSpace(providerResponseData))
        {
            try
            {
                using var doc = JsonDocument.Parse(providerResponseData);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    var isTimeout = root.TryGetProperty("isTimeoutOrUnknown", out var tProp) && tProp.ValueKind == JsonValueKind.True;
                    if (!isTimeout)
                    {
                        var status = GetJsonPropString(root, "providerStatus", "status", "statusCode")?.ToUpperInvariant();
                        var errorCode = GetJsonPropString(root, "errorCode")?.ToUpperInvariant();
                        var success = root.TryGetProperty("success", out var sProp) && sProp.ValueKind == JsonValueKind.True;

                        var isTerminalFailure = status is "FAILED" or "REJECTED" or "DECLINED" or "CANCELLED" or "ERROR" or "FAILURE"
                            || errorCode is "FAILED" or "REJECTED" or "DECLINED" or "ACCOUNT_INVALID" or "INVALID_ACCOUNT" or "INSUFFICIENT_FUNDS" or "BENEFICIARY_BANK_UNAVAILABLE" or "ACCOUNT_NOT_FOUND";

                        if (!success && isTerminalFailure)
                        {
                            var txId = GetJsonPropString(root, "providerTransactionId", "transaction_id", "transactionId", "referenceNumber", "id");
                            var extAcc = GetJsonPropString(root, "externalBankAccountId", "accountNumber", "bankAccount");
                            var hasAmount = root.TryGetProperty("amount", out var amtProp) && amtProp.ValueKind == JsonValueKind.Number;

                            // FAIL-CLOSED VALIDATION:
                            // Case A: Pre-execution rejection by provider before a transaction is created:
                            // Provenance is guaranteed when the response belongs to this exact withdrawal reference/request
                            // and the provider terminal error code specifically confirms rejection before execution (e.g. ACCOUNT_INVALID, INSUFFICIENT_FUNDS).
                            // In this case, amount and account must match if present, and transaction ID may be absent.
                            // But if amount or account is missing, we require an explicit trusted request correlation (e.g. matching ReferenceNumber or non-empty Account).
                            
                            // Case B: Post-creation failure: transaction ID is present.
                            // In both cases: NEVER assume missing fields match!
                            // If amount is present: it MUST match expectedAmount exactly.
                            // If externalBankAccountId is present: it MUST match externalBankAccountId exactly.
                            // If txId is present: it MUST match providerTxId exactly.
                            
                            bool amtMatches = hasAmount && (amtProp.GetDecimal() == expectedAmount);
                            bool accMatches = !string.IsNullOrWhiteSpace(extAcc) && !string.IsNullOrWhiteSpace(externalBankAccountId) && string.Equals(extAcc, externalBankAccountId, StringComparison.OrdinalIgnoreCase);
                            bool txMatches = !string.IsNullOrWhiteSpace(txId) && !string.IsNullOrWhiteSpace(providerTxId) && string.Equals(txId, providerTxId, StringComparison.OrdinalIgnoreCase);

                            // Strict Acceptance Criteria:
                            // Category B (Post-creation terminal failure): When transaction ID exists (either in payload or on withdrawal),
                            // it MUST match providerTxId, and account + amount must match!
                            var effectiveTxId = !string.IsNullOrWhiteSpace(providerTxId) ? providerTxId : txId;
                            var txIdMatches = !string.IsNullOrWhiteSpace(txId) && !string.IsNullOrWhiteSpace(effectiveTxId) && string.Equals(txId, effectiveTxId, StringComparison.OrdinalIgnoreCase);

                            if (txIdMatches && accMatches && amtMatches)
                            {
                                return (true, $"Provider gateway rejection: {errorCode ?? status}");
                            }

                            // Category A (Pre-creation rejection): Transaction ID does not exist yet (txId is null/empty AND providerTxId is null/empty),
                            // but response carries trusted provenance matching both Account AND Amount for this withdrawal:
                            if (string.IsNullOrWhiteSpace(txId) && string.IsNullOrWhiteSpace(providerTxId) && accMatches && amtMatches)
                            {
                                return (true, $"Provider pre-execution gateway rejection: {errorCode ?? status}");
                            }

                            // Any missing field (e.g. missing amount, missing account, or mismatched transaction ID)
                            // MUST FAIL-CLOSED. We do NOT allow "missing = match".
                        }
                    }
                }
            }
            catch { }
        }

        // 3. Processed Webhook BankIntegrationEvent shows definitive failure:
        // P0-3 FIX: Strictly requires BOTH providerTxId and externalBankAccountId to match this exact withdrawal!
        // Never use OR condition; never match on bank account alone.
        if (!string.IsNullOrWhiteSpace(providerTxId) && !string.IsNullOrWhiteSpace(externalBankAccountId))
        {
            var failEventTypes = new[] { "OUTFLOW_FAILED", "OUTFLOW_REJECTED", "PAYOUT_FAILED", "PAYOUT_REJECTED", "PAYMENT_REJECTED" };
            var matchingEvent = await db.BankIntegrationEvents.AsNoTracking().FirstOrDefaultAsync(
                e => e.ExternalTransactionId == providerTxId
                    && e.ExternalBankAccountId == externalBankAccountId
                    && e.ProcessingStatus == BankIntegrationEventStatus.Processed
                    && failEventTypes.Contains(e.EventType),
                cancellationToken);

            if (matchingEvent != null)
            {
                return (true, $"Bank webhook recorded definitive outflow rejection: {matchingEvent.EventType}");
            }
        }

        // If neither trusted provider response nor processed webhook event proves failure, hold is preserved
        return (false, null);
    }

    private static string? GetJsonPropString(JsonElement element, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            if (element.TryGetProperty(prop, out var val))
            {
                if (val.ValueKind == JsonValueKind.String)
                    return val.GetString();
                if (val.ValueKind == JsonValueKind.Number)
                    return val.GetRawText();
            }
        }
        return null;
    }
}
