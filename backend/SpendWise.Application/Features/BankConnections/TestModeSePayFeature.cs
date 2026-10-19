using System.Security.Cryptography;
using System.Text.RegularExpressions;
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

public sealed record StartTestModeBankLinkCommand(bool IsDevelopment, Guid UserId)
    : IRequest<TestModeBankLinkChallengeDto>;
public sealed record ConfirmTestModeBankLinkCommand(bool IsDevelopment, Guid UserId, Guid ChallengeId)
    : IRequest<BankConnectionDto>;
public sealed record CreateTestModeWalletTopUpCommand(
    bool IsDevelopment,
    Guid UserId,
    CreateWalletTopUpRequest Request) : IRequest<WalletTopUpDto>;
public sealed record ConfirmTestModeWalletTopUpCommand(bool IsDevelopment, Guid UserId, Guid TopUpId)
    : IRequest<WalletTopUpDto>;
public sealed record GetTestModeLinkedAccountsQuery(bool IsDevelopment, Guid UserId)
    : IRequest<IReadOnlyList<TestModeLinkedAccountDto>>;
public sealed record GetTestModeWalletTopUpsQuery(bool IsDevelopment, Guid UserId)
    : IRequest<IReadOnlyList<WalletTopUpDto>>;

internal static partial class TestModeSePaySupport
{
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TopUpLifetime = TimeSpan.FromMinutes(15);

    public static DateTime LinkExpiry(DateTime now) => now.Add(LinkLifetime);
    public static DateTime TopUpExpiry(DateTime now) => now.Add(TopUpLifetime);

    public static string CreateReference(string prefix) =>
        prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

    public static void EnsureDevelopment(bool isDevelopment)
    {
        if (!isDevelopment)
            throw new NotFoundException("sepay_test_mode_unavailable", "SePay Test Mode flow is unavailable.");
    }

    public static bool ContainsExactReference(SePayTestModeTransactionDto transaction, string reference)
    {
        if (string.Equals(transaction.ReferenceNumber, reference, StringComparison.OrdinalIgnoreCase)
            || string.Equals(transaction.Code, reference, StringComparison.OrdinalIgnoreCase))
            return true;
        return ReferenceTokenRegex(reference).IsMatch(transaction.Content);
    }

    public static WalletTopUpDto ToDto(WalletTopUp topUp, string maskedAccount) => new(
        topUp.Id,
        topUp.BankConnectionId,
        topUp.WalletId,
        topUp.Amount,
        topUp.Currency,
        maskedAccount,
        topUp.Reference,
        topUp.Status.ToString(),
        topUp.ExpiresAt,
        topUp.CompletedAt,
        topUp.FinancialTransactionId,
        topUp.Status == WalletTopUpStatus.Pending
            ? $"Tạo giao dịch tiền ra đúng {topUp.Amount:0} VND trên SePay Test Mode và nhập mã {topUp.Reference}."
            : string.Empty);

    private static Regex ReferenceTokenRegex(string reference) => new(
        $"(?<![A-Z0-9]){Regex.Escape(reference)}(?![A-Z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
}

public sealed class StartTestModeBankLinkCommandHandler(
    ISpendWiseDbContext db,
    IClock clock) : IRequestHandler<StartTestModeBankLinkCommand, TestModeBankLinkChallengeDto>
{
    public async Task<TestModeBankLinkChallengeDto> Handle(
        StartTestModeBankLinkCommand command,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(command.IsDevelopment);
        if (!await db.Users.AnyAsync(user => user.Id == command.UserId, cancellationToken))
            throw new NotFoundException("user_not_found", "User not found.");

        var reference = TestModeSePaySupport.CreateReference("SWLINK");
        var challenge = new BankLinkSession
        {
            UserId = command.UserId,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            Purpose = BankLinkPurpose.LinkBankAccount,
            ExternalLinkTokenXid = reference,
            Status = BankLinkSessionStatus.Pending,
            ExpiresAt = TestModeSePaySupport.LinkExpiry(clock.UtcNow)
        };
        db.BankLinkSessions.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);
        return new TestModeBankLinkChallengeDto(
            challenge.Id,
            reference,
            challenge.ExpiresAt,
            $"Tạo một giao dịch tiền ra trên SePay Test Mode với nội dung chứa mã {reference}, sau đó xác nhận liên kết.");
    }
}

public sealed class ConfirmTestModeBankLinkCommandHandler(
    ISpendWiseDbContext db,
    ISePayTestModeAccountReader provider,
    IClock clock) : IRequestHandler<ConfirmTestModeBankLinkCommand, BankConnectionDto>
{
    public async Task<BankConnectionDto> Handle(
        ConfirmTestModeBankLinkCommand command,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(command.IsDevelopment);
        var challenge = await db.BankLinkSessions
            .Include(item => item.BankConnection)
            .SingleOrDefaultAsync(item => item.Id == command.ChallengeId && item.UserId == command.UserId
                && item.AccountEnvironment == SePayAccountEnvironment.TestMode, cancellationToken)
            ?? throw new NotFoundException("bank_link_challenge_not_found", "Bank link challenge not found.");

        if (challenge.Status == BankLinkSessionStatus.Completed && challenge.BankConnection is not null)
            return BankingMappings.ToUserDto(challenge.BankConnection);
        if (challenge.Status != BankLinkSessionStatus.Pending)
            throw new ConflictException("bank_link_challenge_unavailable", "Bank link challenge is no longer pending.");
        if (challenge.ExpiresAt <= clock.UtcNow)
        {
            challenge.Status = BankLinkSessionStatus.Expired;
            await db.SaveChangesAsync(cancellationToken);
            throw new ConflictException("bank_link_challenge_expired", "Bank link challenge has expired.");
        }

        var matches = (await provider.GetTransactionsAsync(challenge.ExternalLinkTokenXid, cancellationToken))
            .Where(item => item.Direction == "out"
                && item.AmountOut > 0
                && item.AmountIn == 0
                && TestModeSePaySupport.ContainsExactReference(item, challenge.ExternalLinkTokenXid))
            .ToList();
        if (matches.Count == 0)
            throw new ConflictException("bank_link_not_confirmed", "No matching SePay transaction was found yet.");
        if (matches.Count != 1)
            throw new ConflictException("bank_link_ambiguous", "Multiple matching SePay transactions were found.");

        var proof = matches[0];
        var providerAccount = (await provider.GetAccountsAsync(cancellationToken))
            .SingleOrDefault(item => item.ProviderAccountId == proof.ProviderAccountId)
            ?? throw new ConflictException("sepay_account_not_found", "The SePay account no longer exists.");
        if (providerAccount.Status != "Active")
            throw new ConflictException("sepay_account_inactive", "The SePay account is not active.");

        var existing = await db.BankConnections.SingleOrDefaultAsync(item =>
            item.Provider == BankProvider.SePayBankHub
            && item.AccountEnvironment == SePayAccountEnvironment.TestMode
            && item.ExternalAccountId == proof.ProviderAccountId.ToString(), cancellationToken);
        if (existing is not null && existing.UserId != command.UserId)
            throw new ConflictException("bank_account_already_linked", "This SePay account is already linked to another user.");

        var connection = existing ?? new BankConnection
        {
            UserId = command.UserId,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = proof.ProviderAccountId.ToString()
        };
        connection.BankBrandName = providerAccount.BankName;
        connection.AccountType = providerAccount.AccountType;
        connection.MaskedAccountNumber = providerAccount.MaskedAccountNumber;
        connection.Status = BankConnectionStatus.Active;
        connection.LinkedAt ??= clock.UtcNow;
        connection.UnlinkedAt = null;
        if (existing is null)
            db.BankConnections.Add(connection);

        challenge.BankConnectionId = connection.Id;
        challenge.ExternalBankAccountId = proof.ProviderAccountId.ToString();
        challenge.Status = BankLinkSessionStatus.Completed;
        challenge.CompletedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return BankingMappings.ToUserDto(connection);
    }
}

public sealed class CreateTestModeWalletTopUpCommandHandler(
    ISpendWiseDbContext db,
    ISePayTestModeAccountReader provider,
    IClock clock) : IRequestHandler<CreateTestModeWalletTopUpCommand, WalletTopUpDto>
{
    public async Task<WalletTopUpDto> Handle(
        CreateTestModeWalletTopUpCommand command,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(command.IsDevelopment);
        InputBoundary.PositiveMoney(command.Request.Amount);
        if (decimal.Truncate(command.Request.Amount) != command.Request.Amount)
            throw new AppException("invalid_amount", "Top-up amount must be a whole number of VND.");

        var connection = await db.BankConnections.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == command.Request.BankConnectionId
            && item.UserId == command.UserId
            && item.Provider == BankProvider.SePayBankHub
            && item.AccountEnvironment == SePayAccountEnvironment.TestMode
            && item.Status == BankConnectionStatus.Active, cancellationToken)
            ?? throw BankingErrors.ConnectionNotFound();
        var wallet = await db.Wallets.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == command.Request.WalletId && item.OwnerUserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");
        if (wallet.Status != WalletStatus.Active)
            throw new ConflictException("wallet_unavailable", "Only an active personal wallet can receive a top-up.");

        // Yêu cầu Pending đã hết hạn không được giữ chỗ và chặn lần nạp mới.
        await db.WalletTopUps
            .Where(item => item.BankConnectionId == connection.Id
                && item.Status == WalletTopUpStatus.Pending
                && item.ExpiresAt <= clock.UtcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                item => item.Status, WalletTopUpStatus.Expired), cancellationToken);
        if (await db.WalletTopUps.AnyAsync(item => item.BankConnectionId == connection.Id
            && (item.Status == WalletTopUpStatus.Pending
                || item.Status == WalletTopUpStatus.Processing), cancellationToken))
            throw new ConflictException("wallet_top_up_active_exists", "This SePay account already has an active top-up.");

        if (!Guid.TryParse(connection.ExternalAccountId, out var providerAccountId))
            throw new ConflictException("bank_connection_invalid", "The SePay account identifier is invalid.");
        var providerAccount = (await provider.GetAccountsAsync(cancellationToken))
            .SingleOrDefault(item => item.ProviderAccountId == providerAccountId)
            ?? throw new ConflictException("sepay_account_not_found", "The SePay account no longer exists.");
        if (providerAccount.Status != "Active")
            throw new ConflictException("sepay_account_inactive", "The SePay account is not active.");
        if (providerAccount.AccumulatedBalance < command.Request.Amount)
            throw new ConflictException("sepay_insufficient_balance", "The current SePay Test Mode balance is insufficient.");

        var topUp = new WalletTopUp
        {
            UserId = command.UserId,
            BankConnectionId = connection.Id,
            WalletId = wallet.Id,
            Amount = command.Request.Amount,
            Reference = TestModeSePaySupport.CreateReference("SWTP"),
            ExpiresAt = TestModeSePaySupport.TopUpExpiry(clock.UtcNow)
        };
        db.WalletTopUps.Add(topUp);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique filtered index xử lý cuộc đua khi hai request cùng vượt qua bước đọc ban đầu.
            db.WalletTopUps.Remove(topUp);
            if (await db.WalletTopUps.AsNoTracking().AnyAsync(item => item.BankConnectionId == connection.Id
                && (item.Status == WalletTopUpStatus.Pending
                    || item.Status == WalletTopUpStatus.Processing), cancellationToken))
                throw new ConflictException("wallet_top_up_active_exists", "This SePay account already has an active top-up.");
            throw;
        }
        return TestModeSePaySupport.ToDto(topUp, connection.MaskedAccountNumber);
    }
}

public sealed class GetTestModeLinkedAccountsQueryHandler(
    ISpendWiseDbContext db,
    ISePayTestModeAccountReader provider)
    : IRequestHandler<GetTestModeLinkedAccountsQuery, IReadOnlyList<TestModeLinkedAccountDto>>
{
    public async Task<IReadOnlyList<TestModeLinkedAccountDto>> Handle(
        GetTestModeLinkedAccountsQuery query,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(query.IsDevelopment);
        var connections = await db.BankConnections.AsNoTracking()
            .Where(item => item.UserId == query.UserId
                && item.Provider == BankProvider.SePayBankHub
                && item.AccountEnvironment == SePayAccountEnvironment.TestMode
                && item.Status == BankConnectionStatus.Active)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        if (connections.Count == 0)
            return [];

        // Số dư luôn lấy mới từ Test Mode; API không trả ID tài khoản provider cho User.
        var accounts = (await provider.GetAccountsAsync(cancellationToken))
            .ToDictionary(item => item.ProviderAccountId);
        var result = new List<TestModeLinkedAccountDto>(connections.Count);
        foreach (var connection in connections)
        {
            if (!Guid.TryParse(connection.ExternalAccountId, out var providerAccountId))
                throw new ConflictException("bank_connection_invalid", "The SePay account identifier is invalid.");
            if (!accounts.TryGetValue(providerAccountId, out var account))
                throw new ConflictException("sepay_account_not_found", "The SePay account no longer exists.");

            result.Add(new TestModeLinkedAccountDto(
                connection.Id,
                account.BankName,
                account.MaskedAccountNumber,
                account.AccountType,
                account.Status,
                account.AccumulatedBalance));
        }
        return result;
    }
}

public sealed class GetTestModeWalletTopUpsQueryHandler(
    ISpendWiseDbContext db,
    IClock clock) : IRequestHandler<GetTestModeWalletTopUpsQuery, IReadOnlyList<WalletTopUpDto>>
{
    public async Task<IReadOnlyList<WalletTopUpDto>> Handle(
        GetTestModeWalletTopUpsQuery query,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(query.IsDevelopment);
        // Đồng bộ trạng thái hiển thị để Pending hết hạn không còn được coi là đang hoạt động.
        await db.WalletTopUps.Where(item => item.UserId == query.UserId
                && item.Status == WalletTopUpStatus.Pending
                && item.ExpiresAt <= clock.UtcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                item => item.Status, WalletTopUpStatus.Expired), cancellationToken);

        var rows = await db.WalletTopUps.AsNoTracking()
            .Include(item => item.BankConnection)
            .Where(item => item.UserId == query.UserId
                && item.BankConnection.AccountEnvironment == SePayAccountEnvironment.TestMode)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(item => TestModeSePaySupport.ToDto(
            item, item.BankConnection.MaskedAccountNumber)).ToList();
    }
}

public sealed class ConfirmTestModeWalletTopUpCommandHandler(
    ISpendWiseDbContext db,
    ISePayTestModeAccountReader provider,
    ILedgerService ledger,
    IClock clock) : IRequestHandler<ConfirmTestModeWalletTopUpCommand, WalletTopUpDto>
{
    public async Task<WalletTopUpDto> Handle(
        ConfirmTestModeWalletTopUpCommand command,
        CancellationToken cancellationToken)
    {
        TestModeSePaySupport.EnsureDevelopment(command.IsDevelopment);
        var snapshot = await db.WalletTopUps.AsNoTracking()
            .Include(item => item.BankConnection)
            .SingleOrDefaultAsync(item => item.Id == command.TopUpId && item.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("wallet_top_up_not_found", "Wallet top-up not found.");
        if (snapshot.Status == WalletTopUpStatus.Completed)
            return TestModeSePaySupport.ToDto(snapshot, snapshot.BankConnection.MaskedAccountNumber);
        if (snapshot.Status != WalletTopUpStatus.Pending)
            throw new ConflictException("wallet_top_up_unavailable", "Wallet top-up is no longer pending.");
        if (snapshot.ExpiresAt <= clock.UtcNow)
        {
            await db.WalletTopUps.Where(item => item.Id == snapshot.Id && item.Status == WalletTopUpStatus.Pending)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, WalletTopUpStatus.Expired), cancellationToken);
            throw new ConflictException("wallet_top_up_expired", "Wallet top-up has expired.");
        }

        if (!Guid.TryParse(snapshot.BankConnection.ExternalAccountId, out var providerAccountId))
            throw new ConflictException("bank_connection_invalid", "The SePay account identifier is invalid.");
        var matches = (await provider.GetTransactionsAsync(snapshot.Reference, cancellationToken))
            .Where(item => item.ProviderAccountId == providerAccountId
                && item.Direction == "out"
                && item.AmountOut == snapshot.Amount
                && item.AmountIn == 0
                && TestModeSePaySupport.ContainsExactReference(item, snapshot.Reference))
            .ToList();
        if (matches.Count == 0)
            throw new ConflictException("wallet_top_up_pending", "No exact SePay settlement transaction was found yet.");
        if (matches.Count != 1)
            throw new ConflictException("wallet_top_up_ambiguous", "Multiple exact SePay settlement transactions were found.");
        var settlement = matches[0];

        return await db.ExecuteInTransactionAsync(async token =>
        {
            var claimed = await db.WalletTopUps
                .Where(item => item.Id == snapshot.Id && item.UserId == command.UserId
                    && item.Status == WalletTopUpStatus.Pending && item.ExpiresAt > clock.UtcNow)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, WalletTopUpStatus.Processing), token);
            if (claimed == 0)
            {
                var current = await db.WalletTopUps.AsNoTracking().Include(item => item.BankConnection)
                    .SingleAsync(item => item.Id == snapshot.Id, token);
                if (current.Status == WalletTopUpStatus.Completed)
                    return TestModeSePaySupport.ToDto(current, current.BankConnection.MaskedAccountNumber);
                throw new ConflictException("wallet_top_up_in_progress", "Wallet top-up is being processed or is no longer available.");
            }

            if (await db.WalletTopUps.AnyAsync(item => item.Id != snapshot.Id
                && item.ProviderTransactionId == settlement.ProviderTransactionId.ToString(), token))
                throw new ConflictException("provider_transaction_already_used", "The SePay transaction was already used.");

            var topUp = await db.WalletTopUps.Include(item => item.BankConnection)
                .SingleAsync(item => item.Id == snapshot.Id, token);
            var wallet = await db.Wallets.Include(item => item.LedgerAccount)
                .SingleOrDefaultAsync(item => item.Id == topUp.WalletId && item.OwnerUserId == command.UserId, token)
                ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");
            if (wallet.Status != WalletStatus.Active)
                throw new ConflictException("wallet_unavailable", "Only an active personal wallet can receive a top-up.");

            var externalAccount = await GetExternalSourceAccountAsync(token);
            var providerTransactionId = settlement.ProviderTransactionId.ToString();
            var posting = await ledger.PostAsync(
                externalAccount.Id,
                wallet.LedgerAccount.Id,
                topUp.Amount,
                topUp.Currency,
                FinancialTransactionType.BankTopUp.ToString(),
                topUp.Id,
                $"sepay-topup:{command.UserId:N}",
                providerTransactionId,
                "SePay wallet top-up",
                token);

            FinancialTransaction history;
            if (posting.Replayed)
            {
                history = await db.FinancialTransactions.SingleOrDefaultAsync(item =>
                    item.LedgerTransactionId == posting.LedgerTransactionId
                    && item.RelatedBusinessId == topUp.Id
                    && item.OwnerUserId == command.UserId, token)
                    ?? throw new ConflictException("incomplete_idempotent_operation", "Top-up posting has no matching history.");
            }
            else
            {
                history = new FinancialTransaction
                {
                    OwnerUserId = command.UserId,
                    WalletId = wallet.Id,
                    LedgerTransactionId = posting.LedgerTransactionId,
                    RelatedBusinessId = topUp.Id,
                    Type = FinancialTransactionType.BankTopUp,
                    Status = FinancialTransactionStatus.Completed,
                    Amount = topUp.Amount,
                    Currency = topUp.Currency,
                    Description = "SePay wallet top-up",
                    CounterpartyName = "SePay",
                    OccurredAt = settlement.TransactionAt,
                    IsManual = false,
                    CorrectionAllowance = 0
                };
                db.FinancialTransactions.Add(history);
            }

            topUp.ProviderTransactionId = providerTransactionId;
            topUp.LedgerTransactionId = posting.LedgerTransactionId;
            topUp.FinancialTransactionId = history.Id;
            topUp.Status = WalletTopUpStatus.Completed;
            topUp.CompletedAt = clock.UtcNow;
            await db.SaveChangesAsync(token);
            return TestModeSePaySupport.ToDto(topUp, topUp.BankConnection.MaskedAccountNumber);
        }, cancellationToken);
    }

    private async Task<LedgerAccount> GetExternalSourceAccountAsync(CancellationToken cancellationToken)
    {
        const string code = "SYSTEM:ExternalSource:VND";
        var account = await db.LedgerAccounts.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (account is not null)
            return account;
        account = new LedgerAccount
        {
            Code = code,
            Name = "ExternalSource",
            Type = LedgerAccountType.ExternalSource,
            Currency = "VND",
            IsSystem = true
        };
        db.LedgerAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }
}
