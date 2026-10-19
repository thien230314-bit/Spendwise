using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Infrastructure.Finance;

public sealed class LedgerService(SpendWiseDbContext db, IClock clock) : ILedgerService
{
    private async Task RequirePostingAvailableAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        await db.AcquireMaintenanceAdmissionAsync(false, ct);
        await SpendWise.Application.Features.Operations.V1MaintenanceGate.RequireAvailableAsync(db, clock.UtcNow, ct, "Money");
        var accounts = ids.Distinct().ToArray();
        if (await db.LedgerAccounts.AnyAsync(a => accounts.Contains(a.Id) && a.Type == LedgerAccountType.SharedFund, ct))
            await SpendWise.Application.Features.Operations.V1MaintenanceGate.RequireAvailableAsync(db, clock.UtcNow, ct, "Groups");
    }
    public Task<LedgerPostingResult> PostAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string currency,
        string businessType,
        Guid businessId,
        string? idempotencyScope,
        string? idempotencyKey,
        string description,
        CancellationToken ct)
    {
        // Kiểm tra yêu cầu và chuẩn hóa khóa trước khi mở giao dịch cơ sở dữ liệu.
        ValidateRequest(sourceAccountId, destinationAccountId, amount, currency, idempotencyScope, idempotencyKey);

        // Chuẩn hóa key để request gửi lặp không tạo posting tài chính lần thứ hai.
        var normalizedKey = Normalize(idempotencyKey);
        // Scope tách cùng một key giữa người dùng hoặc loại nghiệp vụ khác nhau.
        var normalizedScope = Normalize(idempotencyScope);

        // Mọi thay đổi số dư, bút toán và lịch sử phải commit hoặc rollback cùng nhau.
        return db.ExecuteInTransactionAsync(async token =>
        {
            await RequirePostingAvailableAsync([sourceAccountId, destinationAccountId], token);
            // Phát lại cùng khóa chỉ hợp lệ khi toàn bộ nội dung tài chính trùng khớp.
            var existing = await FindReplayAsync(businessType, normalizedScope, normalizedKey, token);
            if (existing is not null)
            {
                EnsureReplayMatches(existing, sourceAccountId, destinationAccountId, amount, currency);
                return MapReplay(existing);
            }

            // Nạp hai tài khoản theo thứ tự cố định và kiểm tra số dư, tiền tệ.
            var accounts = await db.LedgerAccounts
                .Where(x => x.Id == sourceAccountId || x.Id == destinationAccountId)
                .OrderBy(x => x.Id)
                .ToListAsync(token);
            var source = accounts.SingleOrDefault(x => x.Id == sourceAccountId)
                ?? throw new NotFoundException("source_account_not_found", "Source account not found.");
            var destination = accounts.SingleOrDefault(x => x.Id == destinationAccountId)
                ?? throw new NotFoundException("destination_account_not_found", "Destination account not found.");

            ValidateAccounts(source, destination, amount, currency);

            var transaction = new LedgerTransaction
            {
                Reference = $"SW-{clock.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..32],
                BusinessType = businessType,
                BusinessId = businessId,
                IdempotencyScope = normalizedScope,
                IdempotencyKey = normalizedKey,
                Description = description,
                Status = LedgerTransactionStatus.Completed,
                CompletedAt = clock.UtcNow
            };

            // Ghi Có tài khoản nguồn để giảm số dư của nguồn.
            transaction.Entries.Add(CreateEntry(source.Id, LedgerEntryDirection.Credit, amount, currency, description));
            // Ghi Nợ tài khoản đích để tăng số dư của đích.
            transaction.Entries.Add(CreateEntry(destination.Id, LedgerEntryDirection.Debit, amount, currency, description));

            source.Balance -= amount;
            destination.Balance += amount;

            // Đồng bộ số dư ví từ sổ cái trong cùng transaction.
            await SyncWalletAsync(source, token);
            await SyncWalletAsync(destination, token);

            // Tổng bên Nợ phải bằng tổng bên Có trước khi hoàn tất posting.
            ValidateBalanced(transaction);
            db.LedgerTransactions.Add(transaction);

            try
            {
                await db.SaveChangesAsync(token);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(
                    "concurrent_financial_operation",
                    "The balance changed concurrently. Retry with the same idempotency key.");
            }
            catch (DbUpdateException) when (normalizedKey is not null)
            {
                // Hai request đồng thời cùng key: đọc posting đã thắng thay vì ghi tiền lần hai.
                db.ChangeTracker.Clear();
                var replay = await FindReplayAsync(businessType, normalizedScope, normalizedKey, token);
                if (replay is null)
                {
                    throw;
                }

                EnsureReplayMatches(replay, sourceAccountId, destinationAccountId, amount, currency);
                return MapReplay(replay);
            }

            return new LedgerPostingResult(transaction.Id, transaction.Reference, amount, currency, false);
        }, ct);
    }

    public Task<LedgerPostingResult> PostEntriesAsync(
        IReadOnlyList<LedgerPostingEntry> entries,
        string currency,
        string businessType,
        Guid businessId,
        string? idempotencyScope,
        string? idempotencyKey,
        string description,
        CancellationToken ct)
    {
        ValidateEntryRequest(entries, currency, idempotencyScope, idempotencyKey);
        var normalizedKey = Normalize(idempotencyKey);
        var normalizedScope = Normalize(idempotencyScope);

        return db.ExecuteInTransactionAsync(async token =>
        {
            await RequirePostingAvailableAsync(entries.Select(e => e.AccountId), token);
            var existing = await FindReplayAsync(businessType, normalizedScope, normalizedKey, token);
            if (existing is not null)
            {
                EnsureReplayMatches(existing, entries, currency);
                return MapReplay(existing);
            }

            // Nạp tài khoản theo thứ tự cố định để giảm nguy cơ deadlock giữa các request nhiều ví.
            var accountIds = entries.Select(entry => entry.AccountId).Distinct().OrderBy(id => id).ToList();
            var accounts = await db.LedgerAccounts
                .Where(account => accountIds.Contains(account.Id))
                .OrderBy(account => account.Id)
                .ToListAsync(token);
            if (accounts.Count != accountIds.Count)
            {
                throw new NotFoundException("ledger_account_not_found", "Ledger account not found.");
            }
            if (accounts.Any(account => account.Currency != currency))
            {
                throw new AppException("currency_mismatch", "Ledger account currency mismatch.");
            }

            var deltas = entries
                .GroupBy(entry => entry.AccountId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(entry => entry.Direction == LedgerEntryDirection.Debit
                        ? entry.Amount
                        : -entry.Amount));
            foreach (var account in accounts)
            {
                var newBalance = account.Balance + deltas[account.Id];
                if (!account.IsSystem && newBalance < 0)
                {
                    throw new ConflictException("insufficient_balance", "Insufficient balance.");
                }
            }

            var transaction = new LedgerTransaction
            {
                Reference = $"SW-{clock.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..32],
                BusinessType = businessType,
                BusinessId = businessId,
                IdempotencyScope = normalizedScope,
                IdempotencyKey = normalizedKey,
                Description = description,
                Status = LedgerTransactionStatus.Completed,
                CompletedAt = clock.UtcNow
            };
            foreach (var entry in entries)
            {
                transaction.Entries.Add(CreateEntry(
                    entry.AccountId,
                    entry.Direction,
                    entry.Amount,
                    currency,
                    description));
            }

            foreach (var account in accounts)
            {
                account.Balance += deltas[account.Id];
                await SyncWalletAsync(account, token);
            }

            ValidateBalanced(transaction);
            db.LedgerTransactions.Add(transaction);
            try
            {
                await db.SaveChangesAsync(token);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(
                    "concurrent_financial_operation",
                    "The balance changed concurrently. Retry with the same idempotency key.");
            }
            catch (DbUpdateException) when (normalizedKey is not null)
            {
                db.ChangeTracker.Clear();
                var replay = await FindReplayAsync(businessType, normalizedScope, normalizedKey, token);
                if (replay is null)
                {
                    throw;
                }

                EnsureReplayMatches(replay, entries, currency);
                return MapReplay(replay);
            }

            return new LedgerPostingResult(
                transaction.Id,
                transaction.Reference,
                entries.Where(entry => entry.Direction == LedgerEntryDirection.Debit).Sum(entry => entry.Amount),
                currency,
                false);
        }, ct);
    }

    public Task<LedgerPostingResult> ReverseAsync(
        Guid originalId,
        Guid businessId,
        string idempotencyKey,
        string reason,
        CancellationToken ct)
    {
        // Tạo posting đảo chiều liên kết posting gốc thay vì xóa lịch sử ledger.
        var scope = $"ledger:{originalId:N}";

        return db.ExecuteInTransactionAsync(async token =>
        {
            await SpendWise.Application.Features.Operations.V1MaintenanceGate.RequireAvailableAsync(db, clock.UtcNow, token, "Money");
            // Đảo bút toán chỉ áp dụng một lần cho giao dịch đã hoàn tất.
            var original = await db.LedgerTransactions
                .Include(x => x.Entries)
                .SingleOrDefaultAsync(x => x.Id == originalId, token)
                ?? throw new NotFoundException("ledger_transaction_not_found", "Ledger transaction not found.");
            await RequirePostingAvailableAsync(original.Entries.Select(e => e.LedgerAccountId), token);

            if (original.Status == LedgerTransactionStatus.Reversed)
            {
                var completedReplay = await FindReplayAsync("Reversal", scope, idempotencyKey.Trim(), token);
                if (completedReplay?.ReversesLedgerTransactionId == originalId)
                {
                    return MapReplay(completedReplay);
                }

                throw new ConflictException("already_reversed", "Transaction has already been reversed.");
            }

            if (original.Status != LedgerTransactionStatus.Completed)
            {
                throw new NotFoundException("ledger_transaction_not_found", "Completed ledger transaction not found.");
            }

            if (await db.LedgerTransactions.AnyAsync(x => x.ReversesLedgerTransactionId == originalId, token))
            {
                throw new ConflictException("already_reversed", "Transaction has already been reversed.");
            }

            // Đảo chiều từng entry để cả posting một ví lẫn nhiều ví đều được hoàn nguyên đầy đủ.
            var reversedEntries = original.Entries
                .Select(entry => new LedgerPostingEntry(
                    entry.LedgerAccountId,
                    entry.Direction == LedgerEntryDirection.Debit
                        ? LedgerEntryDirection.Credit
                        : LedgerEntryDirection.Debit,
                    entry.Amount))
                .ToList();
            var result = await PostEntriesAsync(
                reversedEntries,
                original.Entries[0].Currency,
                "Reversal",
                businessId,
                scope,
                idempotencyKey,
                reason,
                token);

            var reversal = await db.LedgerTransactions.FindAsync([result.LedgerTransactionId], token);
            reversal!.ReversesLedgerTransactionId = originalId;

            // Đánh dấu posting gốc đã được đảo nhưng giữ nguyên toàn bộ LedgerEntry lịch sử.
            original.Status = LedgerTransactionStatus.Reversed;
            await db.SaveChangesAsync(token);

            return result;
        }, ct);
    }

    private Task<LedgerTransaction?> FindReplayAsync(
        string businessType,
        string? idempotencyScope,
        string? idempotencyKey,
        CancellationToken ct)
    {
        // Tìm posting cũ theo scope và idempotency key để tránh ghi tiền lần hai.
        if (idempotencyKey is null)
        {
            return Task.FromResult<LedgerTransaction?>(null);
        }

        return db.LedgerTransactions
            .AsNoTracking()
            .Include(x => x.Entries)
            .SingleOrDefaultAsync(
                x => x.BusinessType == businessType
                    && x.IdempotencyScope == idempotencyScope
                    && x.IdempotencyKey == idempotencyKey,
                ct);
    }

    private async Task SyncWalletAsync(LedgerAccount account, CancellationToken ct)
    {
        // Đồng bộ AvailableBalance của ví từ số dư chuẩn trên LedgerAccount.
        if (account.WalletId is null)
        {
            return;
        }

        // Đồng bộ số dư hiển thị của ví từ số dư ledger sau posting.
        var wallet = await db.Wallets.SingleAsync(x => x.Id == account.WalletId, ct);
        if (wallet.Status != WalletStatus.Active)
        {
            throw new ConflictException("wallet_unavailable", $"Wallet is {wallet.Status}.");
        }

        wallet.AvailableBalance = account.Balance;
    }

    private static void ValidateRequest(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string currency,
        string? idempotencyScope,
        string? idempotencyKey)
    {
        // Posting chỉ hợp lệ khi số tiền dương, hai tài khoản khác nhau và dùng VND.
        if (amount <= 0)
        {
            throw new AppException("invalid_amount", "Amount must be greater than zero.");
        }

        if (sourceAccountId == destinationAccountId)
        {
            throw new AppException("same_account", "Source and destination accounts must differ.");
        }

        if (currency != "VND")
        {
            throw new AppException("unsupported_currency", "Only VND is currently supported.");
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && string.IsNullOrWhiteSpace(idempotencyScope))
        {
            throw new AppException("invalid_idempotency_scope", "An idempotency scope is required when a key is supplied.");
        }
    }

    private static void ValidateAccounts(
        LedgerAccount source,
        LedgerAccount destination,
        decimal amount,
        string currency)
    {
        // Hai tài khoản phải cùng đơn vị tiền; tài khoản nguồn thông thường phải đủ số dư.
        if (source.Currency != currency || destination.Currency != currency)
        {
            throw new AppException("currency_mismatch", "Ledger account currency mismatch.");
        }

        if (!source.IsSystem && source.Balance < amount)
        {
            // Tài khoản nguồn thông thường không được chi vượt số dư ledger.
            throw new ConflictException("insufficient_balance", "Insufficient balance.");
        }
    }

    private static void EnsureReplayMatches(
        LedgerTransaction existing,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string currency)
    {
        // Replay chỉ hợp lệ khi tài khoản, số tiền và đơn vị tiền khớp posting cũ.
        var credits = existing.Entries.Where(x => x.Direction == LedgerEntryDirection.Credit).ToList();
        var debits = existing.Entries.Where(x => x.Direction == LedgerEntryDirection.Debit).ToList();
        // Posting nhiều ví không khớp API một ví, nhưng phải trả Conflict thay vì ném do Single().
        var matches = credits.Count == 1
            && debits.Count == 1
            && credits[0].LedgerAccountId == sourceAccountId
            && debits[0].LedgerAccountId == destinationAccountId
            && credits[0].Amount == amount
            && debits[0].Amount == amount
            && credits[0].Currency == currency
            && debits[0].Currency == currency;

        if (!matches)
        {
            // Cùng key nhưng payload khác phải Conflict để không phát sinh giao dịch khác.
            throw new ConflictException(
                "idempotency_conflict",
                "The idempotency key was already used with different transaction details.");
        }
    }

    private static void EnsureReplayMatches(
        LedgerTransaction existing,
        IReadOnlyList<LedgerPostingEntry> requestedEntries,
        string currency)
    {
        var stored = existing.Entries
            .Select(entry => (entry.LedgerAccountId, entry.Direction, entry.Amount, entry.Currency))
            .OrderBy(entry => entry.LedgerAccountId)
            .ThenBy(entry => entry.Direction)
            .ThenBy(entry => entry.Amount)
            .ToList();
        var requested = requestedEntries
            .Select(entry => (entry.AccountId, entry.Direction, entry.Amount, Currency: currency))
            .OrderBy(entry => entry.AccountId)
            .ThenBy(entry => entry.Direction)
            .ThenBy(entry => entry.Amount)
            .ToList();
        if (!stored.SequenceEqual(requested))
        {
            throw new ConflictException(
                "idempotency_conflict",
                "The idempotency key was already used with different transaction details.");
        }
    }

    private static void ValidateEntryRequest(
        IReadOnlyList<LedgerPostingEntry> entries,
        string currency,
        string? idempotencyScope,
        string? idempotencyKey)
    {
        if (entries.Count < 2)
        {
            throw new AppException("invalid_ledger_entries", "Ledger entries must contain positive amounts.");
        }
        foreach (var entry in entries)
        {
            InputBoundary.PositiveMoney(entry.Amount, "Ledger entry amount");
        }
        if (currency != "VND")
        {
            throw new AppException("unsupported_currency", "Only VND is currently supported.");
        }
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && string.IsNullOrWhiteSpace(idempotencyScope))
        {
            throw new AppException("invalid_idempotency_scope", "An idempotency scope is required when a key is supplied.");
        }

        var debit = entries.Where(entry => entry.Direction == LedgerEntryDirection.Debit).Sum(entry => entry.Amount);
        var credit = entries.Where(entry => entry.Direction == LedgerEntryDirection.Credit).Sum(entry => entry.Amount);
        if (debit <= 0 || debit != credit)
        {
            throw new AppException("unbalanced_ledger_entries", "Ledger entries must be balanced.");
        }
    }

    private static LedgerEntry CreateEntry(
        Guid accountId,
        LedgerEntryDirection direction,
        decimal amount,
        string currency,
        string description) => new()
        {
        // Tạo một bút toán Nợ hoặc Có cho tài khoản ledger được chỉ định.
            LedgerAccountId = accountId,
            Direction = direction,
            Amount = amount,
            Currency = currency,
            Memo = description
        };

    private static LedgerPostingResult MapReplay(LedgerTransaction transaction)
    {
        // Đọc các LedgerEntry của posting cũ để trả lại đúng kết quả replay trước đó.
        var entries = transaction.Entries.Where(x => x.Direction == LedgerEntryDirection.Debit).ToList();
        return new LedgerPostingResult(
            transaction.Id,
            transaction.Reference,
            entries.Sum(entry => entry.Amount),
            entries[0].Currency,
            true);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static void ValidateBalanced(LedgerTransaction transaction)
    {
        // Tính tổng tiền của tất cả bút toán bên Nợ.
        var debits = transaction.Entries
            .Where(x => x.Direction == LedgerEntryDirection.Debit)
            .Sum(x => x.Amount);
        // Tính tổng tiền của tất cả bút toán bên Có.
        var credits = transaction.Entries
            .Where(x => x.Direction == LedgerEntryDirection.Credit)
            .Sum(x => x.Amount);

        if (debits != credits || debits <= 0)
        {
            throw new InvalidOperationException("Ledger transaction is unbalanced.");
        }
    }
}
