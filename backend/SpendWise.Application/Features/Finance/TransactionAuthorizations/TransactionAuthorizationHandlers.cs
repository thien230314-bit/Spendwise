using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Common.Security;
using SpendWise.Application.Features.Banking;
using SpendWise.Application.Features.Transfers;
using SpendWise.Application.Finance;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.Finance.TransactionAuthorizations;

public sealed record CreateTransactionAuthorizationOptionsCommand(
    Guid UserId,
    CreateTransactionAuthorizationOptionsRequest Request) : IRequest<TransactionAuthorizationOptionsResponse>;

public sealed record VerifyAndExecuteTransactionAuthorizationCommand(
    Guid UserId,
    VerifyTransactionAuthorizationRequest Request) : IRequest<TransactionAuthorizationExecutionResult>;

public sealed class TransactionAuthorizationHandlers(
    ISpendWiseDbContext db,
    ILedgerService ledger,
    IClock clock,
    IActiveBankGuard activeBankGuard) :
    IRequestHandler<CreateTransactionAuthorizationOptionsCommand, TransactionAuthorizationOptionsResponse>,
    IRequestHandler<VerifyAndExecuteTransactionAuthorizationCommand, TransactionAuthorizationExecutionResult>
{
    private const string DefaultRpId = "spendwise.test";

    public async Task<TransactionAuthorizationOptionsResponse> Handle(
        CreateTransactionAuthorizationOptionsCommand command,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        var req = command.Request;

        InputBoundary.PositiveMoney(req.Amount);

        if (string.IsNullOrWhiteSpace(req.TransactionType))
            throw new AppException("invalid_transaction_type", "Transaction type is required.", 400);

        if (string.IsNullOrWhiteSpace(req.IdempotencyKey))
            throw new AppException("invalid_idempotency_key", "Idempotency key is required.", 400);

        // Check active biometric passkey enrollment
        var activeCredentials = await db.UserPasskeyCredentials
            .Where(c => c.UserId == userId && !c.IsRevoked)
            .Select(c => c.CredentialId)
            .ToListAsync(cancellationToken);

        if (activeCredentials.Count == 0)
        {
            throw new AppException(
                "biometric_not_enrolled",
                "User has no active biometric passkey credentials enrolled. Please register a passkey first.",
                400);
        }

        // Check if existing pending authorization exists with this idempotency key
        var existing = await db.TransactionAuthorizations
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IdempotencyKey == req.IdempotencyKey, cancellationToken);

        if (existing != null)
        {
            if (existing.Status == TransactionAuthorizationStatus.Pending && existing.ExpiresAt > clock.UtcNow)
            {
                return new TransactionAuthorizationOptionsResponse(
                    existing.Id,
                    existing.Challenge,
                    existing.TransactionType,
                    existing.Amount,
                    existing.Currency,
                    existing.SourceId,
                    existing.DestinationId,
                    existing.ExpiresAt,
                    activeCredentials);
            }
        }

        // Validate source wallet balance where sourceId is a GUID
        if (Guid.TryParse(req.SourceId, out var sourceWalletId))
        {
            var sourceWallet = await db.Wallets
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == sourceWalletId && w.OwnerUserId == userId, cancellationToken);

            if (sourceWallet != null)
            {
                if (sourceWallet.Status != WalletStatus.Active)
                    throw new ConflictException("wallet_unavailable", $"Source wallet is {sourceWallet.Status}.");

                if (sourceWallet.AvailableBalance < req.Amount)
                    throw new AppException("insufficient_funds", "Source wallet has insufficient funds.", 400);
            }
        }

        // Validate P2P constraints
        if (req.TransactionType.Equals("P2PTransfer", StringComparison.OrdinalIgnoreCase))
        {
            if (Guid.TryParse(req.DestinationId, out var recipientId))
            {
                if (recipientId == userId)
                    throw new AppException("self_transfer", "Cannot transfer P2P to yourself.", 400);

                var recipient = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == recipientId, cancellationToken)
                    ?? throw new NotFoundException("recipient_not_found", "Recipient user was not found.");
            }
        }

        var challengeStr = WebAuthnPasskeySupport.GenerateChallenge(32);
        var authEntity = new TransactionAuthorization
        {
            UserId = userId,
            TransactionType = req.TransactionType,
            Amount = req.Amount,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "VND" : req.Currency,
            SourceId = req.SourceId,
            DestinationId = req.DestinationId,
            IdempotencyKey = req.IdempotencyKey,
            PayloadJson = req.PayloadJson ?? "{}",
            Challenge = challengeStr,
            Status = TransactionAuthorizationStatus.Pending,
            ExpiresAt = clock.UtcNow.AddMinutes(5)
        };

        db.TransactionAuthorizations.Add(authEntity);
        await db.SaveChangesAsync(cancellationToken);

        return new TransactionAuthorizationOptionsResponse(
            authEntity.Id,
            authEntity.Challenge,
            authEntity.TransactionType,
            authEntity.Amount,
            authEntity.Currency,
            authEntity.SourceId,
            authEntity.DestinationId,
            authEntity.ExpiresAt,
            activeCredentials);
    }

    public async Task<TransactionAuthorizationExecutionResult> Handle(
        VerifyAndExecuteTransactionAuthorizationCommand command,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        var req = command.Request;

        var auth = await db.TransactionAuthorizations
            .FirstOrDefaultAsync(a => a.Id == req.AuthorizationId && a.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("authorization_not_found", "Transaction authorization was not found.");

        if (auth.Status == TransactionAuthorizationStatus.Executed)
        {
            // Idempotent replay of executed authorization
            object? previousResult = null;
            if (!string.IsNullOrWhiteSpace(auth.ExecutedResultJson))
            {
                try
                {
                    previousResult = JsonSerializer.Deserialize<JsonElement>(auth.ExecutedResultJson);
                }
                catch
                {
                    previousResult = auth.ExecutedResultJson;
                }
            }

            return new TransactionAuthorizationExecutionResult(
                Success: true,
                AuthorizationId: auth.Id,
                Status: "Executed",
                TransactionType: auth.TransactionType,
                Amount: auth.Amount,
                Currency: auth.Currency,
                ExecutedAt: auth.ExecutedAt,
                Result: previousResult);
        }

        if (auth.Status != TransactionAuthorizationStatus.Pending)
        {
            throw new AppException("invalid_authorization_status", $"Authorization is in '{auth.Status}' state.", 400);
        }

        if (auth.ExpiresAt < clock.UtcNow)
        {
            auth.Status = TransactionAuthorizationStatus.Expired;
            await db.SaveChangesAsync(cancellationToken);
            throw new AppException("authorization_expired", "Transaction authorization challenge has expired.", 400);
        }

        // Retrieve credential
        var credential = await db.UserPasskeyCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == req.CredentialId && c.UserId == userId && !c.IsRevoked, cancellationToken)
            ?? throw new AppException("credential_not_found_or_revoked", "Passkey credential was not found or has been revoked.", 401);

        // Strict WebAuthn assertion verification with User Verification requirement
        bool verified = WebAuthnPasskeySupport.VerifyPasskeyAssertion(
            publicKeyString: credential.PublicKey,
            algorithm: credential.Algorithm,
            signatureString: req.Signature,
            clientDataJsonString: req.ClientDataJSON,
            authenticatorDataString: req.AuthenticatorData,
            expectedChallenge: auth.Challenge,
            expectedRpId: DefaultRpId,
            allowedOrigins: WebAuthnPasskeySupport.DefaultAllowedOrigins,
            requireWebAuthn: true,
            requireUserVerification: true,
            signCount: out var signCount,
            userVerified: out var userVerified,
            errorMessage: out var error);

        if (!verified)
        {
            throw new AppException("biometric_verification_failed", error ?? "Biometric signature verification failed.", 401);
        }

        // Check for authenticator counter anomaly
        if (signCount > 0 && credential.SignCount > 0 && signCount <= credential.SignCount)
        {
            throw new AppException("authenticator_counter_mismatch", "Potential authenticator replay or counter rollback detected.", 401);
        }

        if (signCount > credential.SignCount)
        {
            credential.SignCount = signCount;
        }
        credential.LastUsedAt = clock.UtcNow;
        credential.UserVerified = userVerified;

        auth.Status = TransactionAuthorizationStatus.Authorized;
        auth.AuthorizedAt = clock.UtcNow;
        auth.CredentialId = req.CredentialId;

        // Execute underlying financial transaction atomically inside database transaction
        object executionResult = await db.ExecuteInTransactionAsync(async token =>
        {
            return await ExecuteBusinessTransactionAsync(auth, userId, token);
        }, cancellationToken);

        auth.Status = TransactionAuthorizationStatus.Executed;
        auth.ExecutedAt = clock.UtcNow;
        auth.ExecutedResultJson = JsonSerializer.Serialize(executionResult);

        await db.SaveChangesAsync(cancellationToken);

        return new TransactionAuthorizationExecutionResult(
            Success: true,
            AuthorizationId: auth.Id,
            Status: "Executed",
            TransactionType: auth.TransactionType,
            Amount: auth.Amount,
            Currency: auth.Currency,
            ExecutedAt: auth.ExecutedAt,
            Result: executionResult);
    }

    private async Task<object> ExecuteBusinessTransactionAsync(
        TransactionAuthorization auth,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // 1. Internal Transfer between user's own wallets
        if (auth.TransactionType.Equals("InternalTransfer", StringComparison.OrdinalIgnoreCase))
        {
            await activeBankGuard.EnsureActiveBankAsync(userId, cancellationToken);

            if (!Guid.TryParse(auth.SourceId, out var srcWalletId) || !Guid.TryParse(auth.DestinationId, out var dstWalletId))
                throw new AppException("invalid_wallets", "Invalid source or destination wallet ID.", 400);

            if (srcWalletId == dstWalletId)
                throw new AppException("same_wallet", "Source and destination wallets must differ.", 400);

            var wallets = await db.Wallets
                .Include(w => w.LedgerAccount)
                .Where(w => w.OwnerUserId == userId && (w.Id == srcWalletId || w.Id == dstWalletId))
                .ToListAsync(cancellationToken);

            var sourceWallet = wallets.SingleOrDefault(w => w.Id == srcWalletId)
                ?? throw new NotFoundException("wallet_not_found", "Source wallet not found.");
            var destWallet = wallets.SingleOrDefault(w => w.Id == dstWalletId)
                ?? throw new NotFoundException("wallet_not_found", "Destination wallet not found.");

            TransferFeatureSupport.ValidateWallet(sourceWallet);
            TransferFeatureSupport.ValidateWallet(destWallet);

            var transfer = new Transfer
            {
                SenderUserId = userId,
                RecipientUserId = userId,
                SourceWalletId = sourceWallet.Id,
                DestinationWalletId = destWallet.Id,
                Amount = auth.Amount,
                Description = $"Biometric transfer from {sourceWallet.Name} to {destWallet.Name}",
                IdempotencyScope = "internal_biometric",
                IdempotencyKey = auth.IdempotencyKey,
                Status = TransferStatus.Processing
            };

            db.Transfers.Add(transfer);
            await db.SaveChangesAsync(cancellationToken);

            var posting = await ledger.PostAsync(
                sourceWallet.LedgerAccount.Id,
                destWallet.LedgerAccount.Id,
                auth.Amount,
                auth.Currency,
                "InternalTransfer",
                transfer.Id,
                $"user:{userId:N}:biometric",
                auth.IdempotencyKey,
                transfer.Description,
                cancellationToken);

            transfer.Status = TransferStatus.Completed;
            transfer.LedgerTransactionId = posting.LedgerTransactionId;
            transfer.CompletedAt = clock.UtcNow;

            db.FinancialTransactions.Add(TransferFeatureSupport.CreateHistory(
                userId,
                sourceWallet.Id,
                posting.LedgerTransactionId,
                transfer.Id,
                FinancialTransactionType.InternalTransfer,
                auth.Amount,
                transfer.Description,
                destWallet.Name,
                clock.UtcNow));

            await db.SaveChangesAsync(cancellationToken);

            var user = await db.Users.FindAsync([userId], cancellationToken)
                ?? throw new NotFoundException("user_not_found", "User not found.");
            transfer.SenderUser = user;
            transfer.RecipientUser = user;

            return TransferFeatureSupport.ToDto(transfer, false);
        }

        // 2. P2P Transfer to another SpendWise user
        if (auth.TransactionType.Equals("P2PTransfer", StringComparison.OrdinalIgnoreCase))
        {
            await activeBankGuard.EnsureActiveBankAsync(userId, cancellationToken);

            if (!Guid.TryParse(auth.SourceId, out var srcWalletId))
                throw new AppException("invalid_source_wallet", "Invalid source wallet ID.", 400);

            if (!Guid.TryParse(auth.DestinationId, out var recipientUserId))
                throw new AppException("invalid_recipient", "Invalid recipient user ID.", 400);

            if (recipientUserId == userId)
                throw new AppException("self_transfer", "Cannot transfer P2P to yourself.", 400);

            await activeBankGuard.EnsureActiveBankAsync(recipientUserId, cancellationToken);

            var sourceWallet = await db.Wallets
                .Include(w => w.LedgerAccount)
                .FirstOrDefaultAsync(w => w.Id == srcWalletId && w.OwnerUserId == userId, cancellationToken)
                ?? throw new NotFoundException("wallet_not_found", "Source wallet not found.");

            var recipientWallet = await db.Wallets
                .Include(w => w.LedgerAccount)
                .FirstOrDefaultAsync(w => w.OwnerUserId == recipientUserId && w.IsDefault && w.Status == WalletStatus.Active, cancellationToken)
                ?? await db.Wallets.Include(w => w.LedgerAccount).FirstOrDefaultAsync(w => w.OwnerUserId == recipientUserId && w.Status == WalletStatus.Active, cancellationToken)
                ?? throw new NotFoundException("recipient_wallet_not_found", "Recipient has no active wallet.");

            TransferFeatureSupport.ValidateWallet(sourceWallet);
            TransferFeatureSupport.ValidateWallet(recipientWallet);

            var transfer = new Transfer
            {
                SenderUserId = userId,
                RecipientUserId = recipientUserId,
                SourceWalletId = sourceWallet.Id,
                DestinationWalletId = recipientWallet.Id,
                Amount = auth.Amount,
                Description = $"Biometric P2P transfer to {recipientUserId}",
                IdempotencyScope = "p2p_biometric",
                IdempotencyKey = auth.IdempotencyKey,
                Status = TransferStatus.Processing
            };

            db.Transfers.Add(transfer);
            await db.SaveChangesAsync(cancellationToken);

            var posting = await ledger.PostAsync(
                sourceWallet.LedgerAccount.Id,
                recipientWallet.LedgerAccount.Id,
                auth.Amount,
                auth.Currency,
                "P2PTransfer",
                transfer.Id,
                $"user:{userId:N}:p2p_biometric",
                auth.IdempotencyKey,
                transfer.Description,
                cancellationToken);

            transfer.Status = TransferStatus.Completed;
            transfer.LedgerTransactionId = posting.LedgerTransactionId;
            transfer.CompletedAt = clock.UtcNow;

            db.FinancialTransactions.Add(TransferFeatureSupport.CreateHistory(
                userId,
                sourceWallet.Id,
                posting.LedgerTransactionId,
                transfer.Id,
                FinancialTransactionType.TransferOut,
                auth.Amount,
                transfer.Description,
                recipientWallet.Name,
                clock.UtcNow));

            await db.SaveChangesAsync(cancellationToken);

            var sender = await db.Users.FindAsync([userId], cancellationToken);
            var recipient = await db.Users.FindAsync([recipientUserId], cancellationToken);
            transfer.SenderUser = sender!;
            transfer.RecipientUser = recipient!;

            return TransferFeatureSupport.ToDto(transfer, false);
        }

        // 3. Group Fund Contribution
        if (auth.TransactionType.Equals("FundContribution", StringComparison.OrdinalIgnoreCase))
        {
            await activeBankGuard.EnsureActiveBankAsync(userId, cancellationToken);

            if (!Guid.TryParse(auth.SourceId, out var srcWalletId) || !Guid.TryParse(auth.DestinationId, out var fundId))
                throw new AppException("invalid_params", "Invalid wallet or fund ID.", 400);

            var fund = await db.V1Funds
                .FirstOrDefaultAsync(f => f.Id == fundId, cancellationToken)
                ?? throw new NotFoundException("fund_not_found", "Fund not found.");

            var membership = await db.V1FundMemberships
                .FirstOrDefaultAsync(m => m.FundId == fundId && m.UserId == userId && m.LeftAt == null, cancellationToken);
            if (membership == null)
                throw new AppException("not_fund_member", "User is not an active member of this fund.", 403);

            var sourceWallet = await db.Wallets
                .Include(w => w.LedgerAccount)
                .FirstOrDefaultAsync(w => w.Id == srcWalletId && w.OwnerUserId == userId, cancellationToken)
                ?? throw new NotFoundException("wallet_not_found", "Source wallet not found.");

            var treasury = await db.V1FundTreasuries
                .Where(t => t.FundId == fundId)
                .OrderByDescending(t => t.Revision)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("treasury_not_found", "Fund treasury not found.");

            var posting = await ledger.PostAsync(
                sourceWallet.LedgerAccount.Id,
                treasury.LedgerAccountId,
                auth.Amount,
                auth.Currency,
                "GroupFundContribution",
                auth.Id,
                $"fund:{fundId:N}",
                auth.IdempotencyKey,
                "Biometric group fund contribution",
                cancellationToken);

            var contribution = new V1FundContribution
            {
                FundId = fundId,
                UserId = userId,
                MembershipId = membership.Id,
                WalletId = sourceWallet.Id,
                AmountVnd = (long)auth.Amount,
                Purpose = "Biometric group fund contribution",
                IdempotencyKey = auth.IdempotencyKey,
                ObligationRevision = treasury.Revision,
                LedgerTransactionId = posting.LedgerTransactionId
            };

            db.V1FundContributions.Add(contribution);
            await db.SaveChangesAsync(cancellationToken);

            return new
            {
                ContributionId = contribution.Id,
                FundId = fund.Id,
                Amount = auth.Amount,
                Status = "Completed",
                LedgerTransactionId = posting.LedgerTransactionId
            };
        }

        // 4. SePay TEST Top-Up Intent Confirmation
        if (auth.TransactionType.Equals("TopUp", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(auth.SourceId, out var bankConnectionId) || !Guid.TryParse(auth.DestinationId, out var walletId))
                throw new AppException("invalid_params", "Invalid bank connection or wallet ID.", 400);

            var connection = await db.BankConnections.FirstOrDefaultAsync(b =>
                b.Id == bankConnectionId && b.UserId == userId && b.Status == BankConnectionStatus.Active, cancellationToken)
                ?? throw new NotFoundException("bank_connection_not_found", "Active bank connection not found.");

            var wallet = await db.Wallets.FirstOrDefaultAsync(w =>
                w.Id == walletId && w.OwnerUserId == userId && w.Status == WalletStatus.Active, cancellationToken)
                ?? throw new NotFoundException("wallet_not_found", "Active wallet not found.");

            // Expire any existing pending/processing top-ups on this bank connection to respect unique filtered index
            var activeTopUps = await db.WalletTopUps
                .Where(t => t.BankConnectionId == connection.Id && (t.Status == WalletTopUpStatus.Pending || t.Status == WalletTopUpStatus.Processing))
                .ToListAsync(cancellationToken);
            foreach (var active in activeTopUps)
            {
                active.Status = WalletTopUpStatus.Expired;
            }

            var topUp = new WalletTopUp
            {
                UserId = userId,
                BankConnectionId = connection.Id,
                WalletId = wallet.Id,
                Amount = auth.Amount,
                Reference = "SWTP" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)),
                ExpiresAt = clock.UtcNow.AddMinutes(15),
                Status = WalletTopUpStatus.Pending
            };

            db.WalletTopUps.Add(topUp);
            await db.SaveChangesAsync(cancellationToken);

            // Per PO instruction: SePay TEST must still confirm sandbox transaction; do NOT auto-credit on biometric pass alone.
            return new
            {
                TopUpId = topUp.Id,
                Reference = topUp.Reference,
                Amount = topUp.Amount,
                Status = topUp.Status.ToString(),
                MaskedAccountNumber = connection.MaskedAccountNumber,
                RequiresSandboxSettlement = true
            };
        }

        // 5. SePay TEST Withdrawal Intent Confirmation
        if (auth.TransactionType.Equals("Withdrawal", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(auth.SourceId, out var walletId) || !Guid.TryParse(auth.DestinationId, out var bankConnectionId))
                throw new AppException("invalid_params", "Invalid wallet or bank connection ID.", 400);

            var wallet = await db.Wallets
                .Include(w => w.LedgerAccount)
                .FirstOrDefaultAsync(w => w.Id == walletId && w.OwnerUserId == userId && w.Status == WalletStatus.Active, cancellationToken)
                ?? throw new NotFoundException("wallet_not_found", "Active wallet not found.");

            if (wallet.AvailableBalance < auth.Amount)
                throw new AppException("insufficient_funds", "Wallet has insufficient funds for withdrawal.", 400);

            var connection = await db.BankConnections.FirstOrDefaultAsync(b =>
                b.Id == bankConnectionId && b.UserId == userId && b.Status == BankConnectionStatus.Active, cancellationToken)
                ?? throw new NotFoundException("bank_connection_not_found", "Active bank connection not found.");

            // Cancel any previous active withdrawals on this bank connection to respect unique filtered index
            var activeWithdrawals = await db.WalletWithdrawals
                .Where(w => w.BankConnectionId == connection.Id &&
                    (w.Status == WalletWithdrawalStatus.Pending ||
                     w.Status == WalletWithdrawalStatus.Reserved ||
                     w.Status == WalletWithdrawalStatus.Processing ||
                     w.Status == WalletWithdrawalStatus.PendingReconciliation))
                .ToListAsync(cancellationToken);
            foreach (var activeW in activeWithdrawals)
            {
                activeW.Status = WalletWithdrawalStatus.Cancelled;
            }

            var withdrawal = new WalletWithdrawal
            {
                UserId = userId,
                BankConnectionId = connection.Id,
                WalletId = wallet.Id,
                Amount = auth.Amount,
                Currency = auth.Currency,
                Reference = "SWWD" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)),
                IdempotencyKey = auth.IdempotencyKey,
                Status = WalletWithdrawalStatus.Processing,
                ReservedAt = clock.UtcNow
            };

            // Deduct available balance in SpendWise
            wallet.AvailableBalance -= auth.Amount;
            db.WalletWithdrawals.Add(withdrawal);
            await db.SaveChangesAsync(cancellationToken);

            // Per PO instruction: Settlement requires valid SePay transaction reconciliation before Completed.
            return new
            {
                WithdrawalId = withdrawal.Id,
                Reference = withdrawal.Reference,
                Amount = withdrawal.Amount,
                Status = withdrawal.Status.ToString(),
                MaskedAccountNumber = connection.MaskedAccountNumber,
                RequiresSandboxSettlement = true
            };
        }

        // Generic execution
        return new
        {
            AuthorizationId = auth.Id,
            auth.TransactionType,
            auth.Amount,
            auth.Currency,
            Status = "Completed",
            Timestamp = clock.UtcNow
        };
    }
}
