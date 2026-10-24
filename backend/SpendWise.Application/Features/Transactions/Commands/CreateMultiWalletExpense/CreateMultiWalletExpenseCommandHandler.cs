using SpendWise.Application.Features.Banking;
using MediatR;

using Microsoft.EntityFrameworkCore;

using SpendWise.Application.Common;

using SpendWise.Application.Common.Exceptions;

using SpendWise.Application.Common.Interfaces;

using SpendWise.Application.Features.Evidence;

using SpendWise.Application.Finance;

using SpendWise.Domain.Finance;



namespace SpendWise.Application.Features.Transactions.Commands.CreateMultiWalletExpense;



// Ghi một khoản chi duy nhất từ các phần tiền do người dùng chỉ định trên tối đa ba ví.

public sealed class CreateMultiWalletExpenseCommandHandler(

    ISpendWiseDbContext db,

    ILedgerService ledger,

    IClock clock,

    FinancialSecurityGuard security,

    EvidenceSupport evidence,
    IActiveBankGuard? activeBankGuard = null) : IRequestHandler<CreateMultiWalletExpenseCommand, TransactionDto>

{

    public async Task<TransactionDto> Handle(

        CreateMultiWalletExpenseCommand command,

        CancellationToken cancellationToken)

    {

        if (activeBankGuard is not null) await activeBankGuard.EnsureActiveBankAsync(command.UserId, cancellationToken);
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);



        var multiExpenseUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (multiExpenseUser?.Status == SpendWise.Domain.Identity.UserStatus.Restricted)

            throw new AppException("account_restricted", "Tài khoản đang bị hạn chế giao dịch tài chính.", 403);



        // Chuẩn hóa key trước khi validate để khoảng trắng không tạo một scope idempotency giả.

        var request = command.Request with

        {

            Description = command.Request.Description.Trim(),

            Counterparty = command.Request.Counterparty?.Trim(),

            IdempotencyKey = command.Request.IdempotencyKey?.Trim()

        };

        Validate(request);



        return await db.ExecuteInTransactionAsync(async token =>

        {

            // Khóa User để các khoản chi đồng thời của cùng chủ ví được kiểm tra số dư tuần tự.

            if (await LockOwnerAsync(command.UserId, token) != 1)

            {

                throw new NotFoundException("user_not_found", "User not found.");

            }



            var walletIds = request.WalletAllocations.Select(item => item.WalletId).ToList();

            // Chỉ nạp ví thuộc người dùng đăng nhập; ID của người khác không được dùng làm quyền.

            var wallets = await db.Wallets

                .Include(wallet => wallet.LedgerAccount)

                .Where(wallet => wallet.OwnerUserId == command.UserId && walletIds.Contains(wallet.Id))

                .OrderBy(wallet => wallet.Id)

                .ToListAsync(token);

            if (wallets.Count != walletIds.Count)

            {

                throw new NotFoundException("wallet_not_found", "One or more wallets were not found.");

            }



            foreach (var allocation in request.WalletAllocations)

            {

                var wallet = wallets.Single(item => item.Id == allocation.WalletId);

                if (wallet.Status != WalletStatus.Active)

                {

                    throw new ConflictException("wallet_unavailable", $"Wallet is {wallet.Status}.");

                }

            }



            var categoryId = await ApplyTransactionRulesAsync(command.UserId, request, token);

            var category = await db.Categories.SingleOrDefaultAsync(

                item => item.Id == categoryId

                    && item.Kind == CategoryKind.Expense

                    && item.IsActive

                    && (item.IsSystem || item.OwnerUserId == command.UserId),

                token)

                ?? throw new AppException("invalid_category", "A valid Expense category is required.");

            var attachments = await evidence.LoadOwnedAsync(

                command.UserId,

                request.EvidenceIds,

                required: true,

                requireUnlinked: false,

                token);



            var transaction = new FinancialTransaction

            {

                OwnerUserId = command.UserId,

                WalletId = request.WalletAllocations.Count == 1

                    ? request.WalletAllocations[0].WalletId

                    : null,

                CategoryId = category.Id,

                Type = FinancialTransactionType.Expense,

                Amount = request.Amount,

                Description = request.Description,

                CounterpartyName = request.Counterparty,

                IsManual = true,

                CorrectionAllowance = 1,

                OccurredAt = request.OccurredAt?.ToUniversalTime() ?? clock.UtcNow

            };

            var clearing = await GetExpenseClearingAsync(token);

            var entries = request.WalletAllocations

                .Select(allocation => new LedgerPostingEntry(

                    wallets.Single(wallet => wallet.Id == allocation.WalletId).LedgerAccount.Id,

                    LedgerEntryDirection.Credit,

                    allocation.Amount))

                .Append(new LedgerPostingEntry(

                    clearing.Id,

                    LedgerEntryDirection.Debit,

                    request.Amount))

                .ToList();

            var scope = request.IdempotencyKey is null

                ? null

                : $"user:{command.UserId:N}:{FinancialTransactionType.Expense}";

            var posting = await ledger.PostEntriesAsync(

                entries,

                "VND",

                FinancialTransactionType.Expense.ToString(),

                transaction.Id,

                scope,

                request.IdempotencyKey,

                transaction.Description,

                token);



            if (posting.Replayed)

            {

                var existing = await db.FinancialTransactions

                    .AsNoTracking()

                    .Include(item => item.Wallet)

                    .Include(item => item.Category)

                    .Include(item => item.Evidence)

                    .Include(item => item.Corrections)

                    .Include(item => item.WalletAllocations).ThenInclude(item => item.Wallet)

                    .SingleOrDefaultAsync(item => item.OwnerUserId == command.UserId

                        && item.LedgerTransactionId == posting.LedgerTransactionId

                        && item.Type == FinancialTransactionType.Expense, token)

                    ?? throw new ConflictException(

                        "incomplete_idempotent_operation",

                        "The ledger posting exists without its matching transaction history.");

                EnsureReplayMatches(existing, request, category.Id, attachments);

                return TransactionMappings.ToDto(existing);

            }



            if (attachments.Any(item => item.FinancialTransactionId != null

                || item.TransactionCorrectionId != null))

            {

                throw new ConflictException("evidence_already_linked", "Evidence is already linked to another operation.");

            }



            transaction.LedgerTransactionId = posting.LedgerTransactionId;

            foreach (var requested in request.WalletAllocations)

            {

                transaction.WalletAllocations.Add(new TransactionWalletAllocation

                {

                    WalletId = requested.WalletId,

                    Wallet = wallets.Single(wallet => wallet.Id == requested.WalletId),

                    Amount = requested.Amount

                });

            }

            foreach (var attachment in attachments)

            {

                attachment.FinancialTransactionId = transaction.Id;

                transaction.Evidence.Add(attachment);

            }

            db.FinancialTransactions.Add(transaction);

            await db.SaveChangesAsync(token);



            transaction.Wallet = transaction.WalletId is Guid walletId

                ? wallets.Single(wallet => wallet.Id == walletId)

                : null;

            transaction.Category = category;

            return TransactionMappings.ToDto(transaction);

        }, cancellationToken);

    }



    private async Task<int> LockOwnerAsync(Guid userId, CancellationToken token)

    {

        if (db is DbContext context

            && string.Equals(context.Database.ProviderName,

                "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))

        {

            return await db.Users.AnyAsync(user => user.Id == userId, token) ? 1 : 0;

        }



        return await db.Users.Where(user => user.Id == userId)

            .ExecuteUpdateAsync(

                setters => setters.SetProperty(user => user.UpdatedAt, user => user.UpdatedAt),

                token);

    }



    private static void Validate(CreateMultiWalletExpenseRequest request)

    {

        InputBoundary.PositiveMoney(request.Amount);

        InputBoundary.OptionalText(request.Description, 500, "Description");

        InputBoundary.OptionalText(request.Counterparty, 160, "Counterparty");

        if (request.WalletAllocations is null || request.WalletAllocations.Count is < 1 or > 3)

        {

            throw new AppException("invalid_wallet_allocations", "Between one and three wallet allocations are required.");

        }

        foreach (var allocation in request.WalletAllocations)

        {

            // Mỗi phần tiền phải tự phù hợp decimal(18,2), không chỉ tổng thanh toán.

            InputBoundary.PositiveMoney(allocation.Amount, "Wallet allocation amount");

        }

        if (request.WalletAllocations.Select(item => item.WalletId).Distinct().Count()

            != request.WalletAllocations.Count)

        {

            throw new AppException("duplicate_wallet_allocation", "Each wallet can appear only once.");

        }

        if (request.WalletAllocations.Sum(item => item.Amount) != request.Amount)

        {

            throw new AppException("allocation_sum_mismatch", "Wallet allocations must equal the total amount.");

        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)

            || request.IdempotencyKey.Length > 100)

        {

            throw new AppException(

                "invalid_idempotency_key",

                "Idempotency key is required and limited to 100 characters.");

        }

    }



    private async Task<Guid> ApplyTransactionRulesAsync(

        Guid userId,

        CreateMultiWalletExpenseRequest request,

        CancellationToken token)

    {

        var normalizedDescription = TransactionTextNormalizer.Normalize(request.Description);

        var rules = await db.TransactionRules.AsNoTracking()

            .Include(rule => rule.TargetCategory)

            .Where(rule => rule.OwnerUserId == userId

                && rule.IsActive

                && rule.TargetCategory.Kind == CategoryKind.Expense)

            .OrderByDescending(rule => rule.Priority)

            .ToListAsync(token);

        return rules.FirstOrDefault(rule => normalizedDescription.Contains(

                TransactionTextNormalizer.Normalize(rule.DescriptionPattern),

                StringComparison.Ordinal))?.TargetCategoryId

            ?? request.CategoryId;

    }



    private async Task<LedgerAccount> GetExpenseClearingAsync(CancellationToken token)

    {

        const string code = "SYSTEM:ExpenseClearing:VND";

        var account = await db.LedgerAccounts.SingleOrDefaultAsync(item => item.Code == code, token);

        if (account is not null)

        {

            return account;

        }



        account = new LedgerAccount

        {

            Code = code,

            Name = LedgerAccountType.ExpenseClearing.ToString(),

            Type = LedgerAccountType.ExpenseClearing,

            IsSystem = true

        };

        db.LedgerAccounts.Add(account);

        await db.SaveChangesAsync(token);

        return account;

    }



    private static void EnsureReplayMatches(

        FinancialTransaction existing,

        CreateMultiWalletExpenseRequest request,

        Guid categoryId,

        IReadOnlyCollection<EvidenceAttachment> attachments)

    {

        var storedAllocations = existing.WalletAllocations

            .Select(item => (item.WalletId, item.Amount))

            .OrderBy(item => item.WalletId)

            .ToList();

        var requestedAllocations = request.WalletAllocations

            .Select(item => (item.WalletId, item.Amount))

            .OrderBy(item => item.WalletId)

            .ToList();

        var matches = existing.CategoryId == categoryId

            && existing.Amount == request.Amount

            && existing.Description == request.Description

            && existing.CounterpartyName == request.Counterparty

            && storedAllocations.SequenceEqual(requestedAllocations)

            && existing.Evidence.Select(item => item.Id).OrderBy(id => id)

                .SequenceEqual(attachments.Select(item => item.Id).OrderBy(id => id))

            && (request.OccurredAt is null

                || existing.OccurredAt == request.OccurredAt.Value.ToUniversalTime());

        if (!matches)

        {

            throw new ConflictException(

                "idempotency_conflict",

                "The idempotency key was already used with different transaction details.");

        }

    }

}

