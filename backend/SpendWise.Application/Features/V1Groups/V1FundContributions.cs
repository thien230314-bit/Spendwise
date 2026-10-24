using Microsoft.EntityFrameworkCore;

using SpendWise.Application.Common;

using SpendWise.Application.Common.Exceptions;

using SpendWise.Domain.Finance;

using SpendWise.Domain.V1Groups;



namespace SpendWise.Application.Features.V1Groups;



public sealed partial class V1FundService

{

    public async Task<IReadOnlyList<V1ContributionDto>> ContributionsAsync(Guid userId, Guid fundId, int page, int pageSize, CancellationToken ct)

    {

        InputBoundary.Page(page, pageSize);

        if (page - 1 > int.MaxValue / pageSize) throw new AppException("invalid_pagination", "Page is too large.");

        await RequireActorAsync(userId, ct);

        if (!await db.V1FundMemberships.AsNoTracking().AnyAsync(m => m.FundId == fundId && m.UserId == userId, ct))

            throw new NotFoundException("fund_not_found", "Fund not found.");

        var rows = await db.V1FundContributions.AsNoTracking().Where(c => c.FundId == fundId

                && db.V1FundMemberships.Any(m => m.FundId == fundId && m.UserId == userId && m.JoinedAt <= c.CreatedAt

                    && (m.LeftAt == null || c.CreatedAt <= m.LeftAt)))

            .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return rows.Select(c => ContributionDto(c, false)).ToList();

    }



    public async Task<V1ContributionDto> ContributeAsync(Guid userId, Guid fundId, V1ContributeRequest request, CancellationToken ct)

    {

        if (request.AmountVnd is <= 0 or > MaximumVnd || request.WalletId == Guid.Empty)

            throw new AppException("invalid_contribution", "Select a wallet and positive integer VND amount.");

        var key = InputBoundary.RequiredText(request.IdempotencyKey, 100, "Idempotency key");

        var purpose = InputBoundary.OptionalText(request.Purpose, 500, "Purpose") ?? "Fund contribution";

        await RequireActorAsync(userId, ct); if (activeBankGuard is not null) await activeBankGuard.EnsureActiveBankAsync(userId, ct); await security.VerifyAsync(userId, request.SecurityPin, ct);

        return await db.ExecuteInTransactionAsync(async token =>

        {

            await LockUsersAsync([userId], token); var fund = await LockFundAsync(fundId, token);

            await RequireMemberAsync(userId, fundId, token);

            var replay = await db.V1FundContributions.AsNoTracking().SingleOrDefaultAsync(c => c.UserId == userId && c.IdempotencyKey == key, token);

            if (replay is not null)

            {

                if (replay.FundId != fundId || replay.WalletId != request.WalletId || replay.AmountVnd != request.AmountVnd

                    || replay.ObligationRevision != request.ObligationRevision || replay.Purpose != purpose)

                    throw new ConflictException("idempotency_conflict", "Key was used for another contribution intent.");

                return ContributionDto(replay, true);

            }

            if (fund.ActivityClosedAt is not null) throw new ConflictException("activity_closed", "Closed activities do not accept new contributions.");

            var election = await db.V1Elections.AsNoTracking().SingleOrDefaultAsync(e => e.FundId == fundId && e.FinishedAt == null, token);

            if (election is not null && !ElectionPolicy(election).AllowContributions)

                throw new ConflictException("election_contributions_paused", "Contributions are paused by the election policy.");

            var contribUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, token);

            if (contribUser?.Status == SpendWise.Domain.Identity.UserStatus.Restricted)

                throw new AppException("account_restricted", "Tài khoản đang bị hạn chế giao dịch tài chính.", 403);



            if (await db.V1Elections.AnyAsync(e => e.FundId == fundId && e.FinishedAt == null, token))
                throw new ConflictException("election_finance_paused", "Contributions are paused during an active election.");

            var treasury = await TreasuryAsync(fund, token);

            if (fund.Kind == V1FundKind.Fixed) await RequireContributionAccountingAsync(treasury, token);

            else await RequireReconciledAccountingAsync(treasury, token);

            var membership = await db.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fundId && m.UserId == userId && m.LeftAt == null, token);

            if (fund.Kind == V1FundKind.Fixed)

            {

                var members = await ActiveMembersAsync(fundId, token);

                if (treasury.Revision == 0 || request.ObligationRevision != treasury.Revision || treasury.TargetVnd != fund.TargetVnd

                    || treasury.MembershipSignature != MembershipSignature(members))

                    throw new ConflictException("fund_configuration_changed", "Owner must confirm the current membership configuration.");

                var required = await db.V1FundObligations.Where(o => o.FundId == fundId && o.Revision == treasury.Revision && o.MembershipId == membership.Id)

                    .Select(o => o.RequiredVnd).SingleAsync(token);

                var paid = (await ActivityPaidAsync(fundId, token)).GetValueOrDefault(membership.Id);

                if (request.AmountVnd > required - paid) throw new ConflictException("contribution_exceeds_obligation", "Contribution exceeds current unpaid obligation.");

            }

            else if (request.ObligationRevision is not null) throw new AppException("invalid_obligation_revision", "Free contributions do not use a Fixed revision.");

            var wallet = await db.Wallets.SingleOrDefaultAsync(w => w.Id == request.WalletId && w.OwnerUserId == userId, token)

                ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");

            if (db is DbContext context)

            {

                if (context.Entry(wallet).State != EntityState.Unchanged) throw new ConflictException("pending_financial_state", "Pending wallet changes cannot be posted.");

                await context.Entry(wallet).ReloadAsync(token);

            }

            var sourceId = await db.LedgerAccounts.Where(a => a.WalletId == wallet.Id).Select(a => a.Id).SingleAsync(token);

            var source = await FreshAccountAsync(sourceId, token); var destination = await FreshAccountAsync(treasury.LedgerAccountId, token);

            if (wallet.Status != WalletStatus.Active) throw new ConflictException("wallet_unavailable", "Wallet is not active.");

            if (wallet.Currency != "VND" || source.Currency != "VND" || wallet.AvailableBalance != source.Balance)

                throw new ConflictException("wallet_ledger_mismatch", "Wallet requires reconciliation.");

            if (destination.Balance + request.AmountVnd > MaximumVnd) throw new ConflictException("fund_amount_limit", "Fund amount limit exceeded.");

            var contribution = new V1FundContribution { FundId = fundId, UserId = userId, MembershipId = membership.Id, WalletId = wallet.Id,

                AmountVnd = request.AmountVnd, ObligationRevision = request.ObligationRevision, IdempotencyKey = key, Purpose = purpose };

            var posting = await ledger.PostAsync(source.Id, destination.Id, request.AmountVnd, "VND", "V1FundContribution", contribution.Id,

                "v1-fund-contribution:" + userId.ToString("N"), key, purpose, token);

            if (posting.Replayed) throw new ConflictException("fund_ledger_inconsistent", "Posting has no corresponding contribution record.");

            contribution.LedgerTransactionId = posting.LedgerTransactionId;

            db.V1FundContributions.Add(contribution);

            db.FinancialTransactions.Add(new FinancialTransaction { OwnerUserId = userId, WalletId = wallet.Id,

                LedgerTransactionId = posting.LedgerTransactionId, RelatedBusinessId = fundId, Type = FinancialTransactionType.GroupFundContribution,

                Amount = request.AmountVnd, Description = purpose, OccurredAt = clock.UtcNow });

            await ActivityAsync(fundId, userId, "ContributionPosted", token, detail: request.AmountVnd.ToString(System.Globalization.CultureInfo.InvariantCulture), referenceId: contribution.Id);

            return ContributionDto(contribution, false);

        }, ct);

    }



    private static V1ContributionDto ContributionDto(V1FundContribution c, bool replayed) =>

        new(c.Id, c.FundId, c.UserId, c.WalletId, c.AmountVnd, c.ObligationRevision, c.LedgerTransactionId, c.CreatedAt, replayed);

}

