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
    string? Reason = null) : IRequest<WalletWithdrawalDto>;

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

        if (connection.AccountEnvironment == SePayAccountEnvironment.TestMode)
        {
            throw new ConflictException(
                "SEPAY_SANDBOX_ACCOUNT_IDENTITY_MAPPING_BLOCKED",
                "Tài khoản liên kết từ SePay TestMode không thể dùng cho Bank Hub withdrawal do chưa xác minh định danh.");
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
                var walletLedger = await db.LedgerAccounts.SingleOrDefaultAsync(
                    a => a.OwnerUserId == withdrawal.UserId && a.WalletId == withdrawal.WalletId && a.Currency == "VND", token)
                    ?? throw new NotFoundException("wallet_ledger_not_found", "Wallet ledger account not found.");

                await ledger.PostAsync(
                    holdAccount.Id,
                    walletLedger.Id,
                    withdrawal.Amount,
                    "VND",
                    "WithdrawalReservationRelease",
                    withdrawal.Id,
                    $"wd-release:{withdrawal.Id:N}",
                    normalizedKey + ":reconciled-release",
                    $"Hoàn trả đối soát số dư tạm giữ rút tiền {withdrawal.Reference}: {command.Reason ?? "Definitive failure confirmation"}",
                    token);

                withdrawal.Status = WalletWithdrawalStatus.Failed;
                withdrawal.FailureReason = command.Reason ?? "Giao dịch đối soát xác nhận thất bại từ nhà cung cấp.";
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
}
