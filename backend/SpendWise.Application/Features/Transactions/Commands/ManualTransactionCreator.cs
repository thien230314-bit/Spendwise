using Microsoft.EntityFrameworkCore;

using SpendWise.Application.Common;

using SpendWise.Application.Common.Exceptions;

using SpendWise.Application.Common.Interfaces;

using SpendWise.Application.Finance;

using SpendWise.Application.Features;

using SpendWise.Application.Features.Evidence;

using SpendWise.Domain.Finance;



namespace SpendWise.Application.Features.Transactions.Commands;



public sealed class ManualTransactionCreator(

    ISpendWiseDbContext db,

    ILedgerService ledger,

    IClock clock,

    FinancialSecurityGuard security,

    EvidenceSupport evidence)

{

    public async Task<TransactionDto> CreateAsync(

        Guid userId,

        CreateManualTransactionRequest request,

        CategoryKind categoryKind,

        CancellationToken cancellationToken,

        Guid? relatedBusinessId = null)

    {

        // PIN được kiểm tra ngay trong mutation trước khi chạm tới ledger.

        await security.VerifyAsync(userId, request.SecurityPin, cancellationToken);



        if (categoryKind == CategoryKind.Expense)

        {

            var creatorUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (creatorUser?.Status == SpendWise.Domain.Identity.UserStatus.Restricted)

                throw new AppException("account_restricted", "Tài khoản đang bị hạn chế giao dịch tài chính.", 403);

        }

        // Kiểm tra dữ liệu đầu vào.

        InputBoundary.PositiveMoney(request.Amount);

        InputBoundary.OptionalText(request.Description, 500, "Description");

        InputBoundary.OptionalText(request.Counterparty, 160, "Counterparty");



        if (request.IdempotencyKey?.Length > 100)

        {

            throw new AppException(

                "invalid_idempotency_key",

                "Idempotency key is limited to 100 characters.");

        }



        // Chuẩn hóa idempotency key do client gửi; key trống được xem là không sử dụng.

        var normalizedRequest = request with

        {

            Description = request.Description?.Trim() ?? string.Empty,

            Counterparty = request.Counterparty?.Trim(),

            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)

                ? null

                : request.IdempotencyKey.Trim()

        };



        // Ví, ledger và lịch sử luôn được ghi trong cùng database transaction.

        return await db.ExecuteInTransactionAsync(async token =>

        {

            // Tìm ví và đồng thời kiểm tra ví thuộc người dùng hiện tại.

            var wallet = await db.Wallets

                .Include(candidate => candidate.LedgerAccount)

                .SingleOrDefaultAsync(

                    candidate => candidate.Id == normalizedRequest.WalletId

                        && candidate.OwnerUserId == userId,

                    token)

                ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");

            if (wallet.Status != WalletStatus.Active)

            {

                throw new ConflictException(

                    "wallet_unavailable",

                    $"Wallet is {wallet.Status}.");

            }



            // Áp dụng quy tắc phân loại trước khi xác nhận danh mục cuối cùng.

            var selectedCategoryId = await ApplyTransactionRulesAsync(

                userId,

                normalizedRequest,

                categoryKind,

                token);

            var category = await db.Categories.SingleOrDefaultAsync(

                candidate => candidate.Id == selectedCategoryId

                    && candidate.Kind == categoryKind

                    && candidate.IsActive

                    && (candidate.IsSystem || candidate.OwnerUserId == userId),

                token)

                ?? throw new AppException(

                    "invalid_category",

                    $"A valid {categoryKind} category is required.");



            // Evidence phải thuộc người dùng; replay được phép dùng lại đúng evidence đã liên kết.

            var attachments = await evidence.LoadOwnedAsync(

                userId,

                normalizedRequest.EvidenceIds,

                required: true,

                requireUnlinked: false,

                token);



            var transaction = CreateTransaction(

                userId,

                wallet.Id,

                category.Id,

                normalizedRequest,

                categoryKind,

                relatedBusinessId);

            // Thu nhập dùng tài khoản nguồn ngoài; chi tiêu dùng tài khoản clearing đích.

            var clearingAccountType = categoryKind == CategoryKind.Income

                ? LedgerAccountType.ExternalSource

                : LedgerAccountType.ExpenseClearing;

            var clearingAccount = await GetSystemAccountAsync(clearingAccountType, token);

            // Scope tách key theo người dùng và loại Thu/Chi.

            var idempotencyScope = normalizedRequest.IdempotencyKey is null

                ? null

                : $"user:{userId:N}:{transaction.Type}";



            // Thu: posting từ nguồn ngoài vào ví. Chi: posting từ ví sang tài khoản clearing.

            var posting = categoryKind == CategoryKind.Income

                ? await ledger.PostAsync(

                    clearingAccount.Id,

                    wallet.LedgerAccount.Id,

                    normalizedRequest.Amount,

                    "VND",

                    transaction.Type.ToString(),

                    transaction.Id,

                    idempotencyScope,

                    normalizedRequest.IdempotencyKey,

                    transaction.Description,

                    token)

                : await ledger.PostAsync(

                    wallet.LedgerAccount.Id,

                    clearingAccount.Id,

                    normalizedRequest.Amount,

                    "VND",

                    transaction.Type.ToString(),

                    transaction.Id,

                    idempotencyScope,

                    normalizedRequest.IdempotencyKey,

                    transaction.Description,

                    token);



            // Retry phải trả đúng lịch sử cũ và khớp toàn bộ payload.

            if (posting.Replayed)

            {

                var existing = await db.FinancialTransactions

                    .Include(item => item.Wallet)

                    .Include(item => item.Category)

                    .Include(item => item.Evidence)

                    .Include(item => item.Corrections)

                    .AsNoTracking()

                    .SingleOrDefaultAsync(

                        item => item.LedgerTransactionId == posting.LedgerTransactionId

                            && item.OwnerUserId == userId

                            && item.Type == transaction.Type,

                        token);

                if (existing is null)

                {

                    throw new ConflictException(

                        "incomplete_idempotent_operation",

                        "The ledger posting exists without its matching transaction history.");

                }



                EnsureReplayMatches(existing, normalizedRequest, category.Id, attachments);

                return TransactionMappings.ToDto(existing);

            }



            if (attachments.Any(item => item.FinancialTransactionId != null

                || item.TransactionCorrectionId != null))

            {

                throw new ConflictException("evidence_already_linked", "Evidence is already linked to another operation.");

            }

            transaction.LedgerTransactionId = posting.LedgerTransactionId;

            db.FinancialTransactions.Add(transaction);

            foreach (var attachment in attachments)

            {

                attachment.FinancialTransactionId = transaction.Id;

                transaction.Evidence.Add(attachment);

            }

            await db.SaveChangesAsync(token);



            transaction.Wallet = wallet;

            transaction.Category = category;

            return TransactionMappings.ToDto(transaction);

        }, cancellationToken);

    }



    private async Task<Guid> ApplyTransactionRulesAsync(

        Guid userId,

        CreateManualTransactionRequest request,

        CategoryKind categoryKind,

        CancellationToken cancellationToken)

    {

        // Bắt đầu bằng danh mục client chọn, sau đó ưu tiên rule có Priority cao nhất.

        var selectedCategoryId = request.CategoryId;

        var normalizedDescription = TransactionTextNormalizer.Normalize(request.Description);

        var rules = await db.TransactionRules

            .AsNoTracking()

            .Include(rule => rule.TargetCategory)

            .Where(rule => rule.OwnerUserId == userId

                && rule.IsActive

                && rule.TargetCategory.Kind == categoryKind)

            .OrderByDescending(rule => rule.Priority)

            .ToListAsync(cancellationToken);

        var matchingRule = rules.FirstOrDefault(

            rule => normalizedDescription.Contains(

                TransactionTextNormalizer.Normalize(rule.DescriptionPattern),

                StringComparison.Ordinal));



        return matchingRule?.TargetCategoryId ?? selectedCategoryId;

    }



    private FinancialTransaction CreateTransaction(

        Guid userId,

        Guid walletId,

        Guid categoryId,

        CreateManualTransactionRequest request,

        CategoryKind categoryKind,

        Guid? relatedBusinessId)

    {

        // Tạo lịch sử Income hoặc Expense từ request sau khi category đã được xác nhận.

        return new FinancialTransaction

        {

            OwnerUserId = userId,

            WalletId = walletId,

            CategoryId = categoryId,

            RelatedBusinessId = relatedBusinessId,

            Type = categoryKind == CategoryKind.Income

                ? FinancialTransactionType.Income

                : FinancialTransactionType.Expense,

            Amount = request.Amount,

            Description = request.Description.Trim(),

            CounterpartyName = request.Counterparty,

            IsManual = true,

            CorrectionAllowance = 1,

            CorrectionCount = 0,

            OccurredAt = request.OccurredAt?.ToUniversalTime() ?? clock.UtcNow

        };

    }



    private async Task<LedgerAccount> GetSystemAccountAsync(

        LedgerAccountType accountType,

        CancellationToken cancellationToken)

    {

        // Mỗi loại tài khoản hệ thống VND dùng một mã ổn định để tái sử dụng.

        var code = $"SYSTEM:{accountType}:VND";

        var account = await db.LedgerAccounts.SingleOrDefaultAsync(

            candidate => candidate.Code == code,

            cancellationToken);

        if (account is not null)

        {

            return account;

        }



        account = new LedgerAccount

        {

            Code = code,

            Name = accountType.ToString(),

            Type = accountType,

            IsSystem = true

        };

        db.LedgerAccounts.Add(account);

        await db.SaveChangesAsync(cancellationToken);



        return account;

    }



    private static void EnsureReplayMatches(

        FinancialTransaction existing,

        CreateManualTransactionRequest request,

        Guid categoryId,

        IReadOnlyCollection<EvidenceAttachment> attachments)

    {

        // Replay Thu/Chi chỉ hợp lệ khi ví, danh mục, số tiền, mô tả và thời điểm đều khớp.

        var matches = existing.WalletId == request.WalletId

            && existing.CategoryId == categoryId

            && existing.Amount == request.Amount

            && existing.Description == request.Description.Trim()

            && existing.CounterpartyName == request.Counterparty

            && existing.Evidence.Select(item => item.Id).OrderBy(id => id)

                .SequenceEqual(attachments.Select(item => item.Id).OrderBy(id => id))

            && (request.OccurredAt is null

                || existing.OccurredAt == request.OccurredAt.Value.ToUniversalTime());

        if (!matches)

        {

            // Cùng key nhưng nội dung khác phải Conflict thay vì ghi thêm giao dịch.

            throw new ConflictException(

                "idempotency_conflict",

                "The idempotency key was already used with different transaction details.");

        }

    }

}

