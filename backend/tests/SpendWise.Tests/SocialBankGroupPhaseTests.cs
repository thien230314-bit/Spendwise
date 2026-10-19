using System.Text.Json;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.BankDirectPayments;
using SpendWise.Application.Features.Friends;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Groups.Commands.CreateGroup;
using SpendWise.Application.Features.Groups.Commands.InviteGroupMember;
using SpendWise.Application.Features.Groups.Commands.RemoveGroupMember;
using SpendWise.Application.Features.Groups.Commands.TransferGroupOwnership;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Groups;
using SpendWise.Application.Social;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Social;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ hợp đồng social, bank-direct và Group/Fund hiện tại.
public sealed class SocialBankGroupPhaseTests
{
    private const string Pin = "123456";

    [Fact]
    public async Task Friend_search_is_exact_and_rejects_self_admin_inactive_and_fuzzy_name()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var current = await fixture.AddUserAsync("Current User", "0900000001");
        var target = await fixture.AddUserAsync("Nguyễn Văn Bạn", "0900000002");
        var admin = await fixture.AddUserAsync("Admin", "0900000003", SystemRole.SystemAdmin);
        var inactive = await fixture.AddUserAsync("Inactive", "0900000004", status: UserStatus.Locked);
        var handler = new SearchFriendContactQueryHandler(fixture.Db);

        var byEmail = await handler.Handle(new SearchFriendContactQuery(current.Id, target.Email.ToUpperInvariant()), default);
        var byPhone = await handler.Handle(new SearchFriendContactQuery(current.Id, target.Phone!), default);

        Assert.Equal(target.Id, byEmail.UserId);
        Assert.Equal(target.Id, byPhone.UserId);
        Assert.Equal("NONE", byPhone.State);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SearchFriendContactQuery(current.Id, "Nguyễn Văn"), default));
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new SearchFriendContactQuery(current.Id, current.Phone!), default));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SearchFriendContactQuery(current.Id, admin.Phone!), default));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SearchFriendContactQuery(current.Id, inactive.Phone!), default));
    }

    [Fact]
    public async Task Friend_requests_are_idempotent_and_reciprocal_request_accepts_deterministically()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var alice = await fixture.AddUserAsync("Alice", "0900000011");
        var bob = await fixture.AddUserAsync("Bob", "0900000012");
        var search = new SearchFriendContactQueryHandler(fixture.Db);
        var handler = new SendFriendRequestCommandHandler(fixture.Db, fixture.Clock, fixture.Notifications, search);

        var first = await handler.Handle(new SendFriendRequestCommand(alice.Id,
            new SendFriendRequest(bob.Phone!, "Hello")), default);
        var replay = await handler.Handle(new SendFriendRequestCommand(alice.Id,
            new SendFriendRequest(bob.Phone!, "Hello")), default);
        var reciprocal = await handler.Handle(new SendFriendRequestCommand(bob.Id,
            new SendFriendRequest(alice.Email)), default);

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal("Accepted", reciprocal.Status);
        Assert.Single(await fixture.Db.FriendRequests.ToListAsync());
        Assert.Single(await fixture.Db.Friendships.Where(item => item.EndedAt == null).ToListAsync());
        Assert.Equal(2, await fixture.Db.FriendPreferences.CountAsync());
        Assert.Equal("FRIENDS", await FriendFeatureSupportForTest.StateAsync(fixture.Db, alice.Id, bob.Id));
    }

    [Fact]
    public async Task Friend_request_accept_reject_cancel_and_unfriend_preserve_history()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var alice = await fixture.AddUserAsync("Alice", "0900000021");
        var bob = await fixture.AddUserAsync("Bob", "0900000022");
        var carol = await fixture.AddUserAsync("Carol", "0900000023");
        var dave = await fixture.AddUserAsync("Dave", "0900000024");
        var send = fixture.SendFriendHandler();

        var acceptRequest = await send.Handle(new SendFriendRequestCommand(alice.Id, new SendFriendRequest(bob.Phone!)), default);
        await new AcceptFriendRequestCommandHandler(fixture.Db, fixture.Clock, fixture.Notifications)
            .Handle(new AcceptFriendRequestCommand(bob.Id, acceptRequest.Id), default);
        var rejectRequest = await send.Handle(new SendFriendRequestCommand(alice.Id, new SendFriendRequest(carol.Phone!)), default);
        var rejected = await new RejectFriendRequestCommandHandler(fixture.Db, fixture.Clock, fixture.Notifications)
            .Handle(new RejectFriendRequestCommand(carol.Id, rejectRequest.Id), default);
        var cancelRequest = await send.Handle(new SendFriendRequestCommand(alice.Id, new SendFriendRequest(dave.Phone!)), default);
        var cancelled = await new CancelFriendRequestCommandHandler(fixture.Db, fixture.Clock)
            .Handle(new CancelFriendRequestCommand(alice.Id, cancelRequest.Id), default);
        await new UnfriendCommandHandler(fixture.Db, fixture.Clock)
            .Handle(new UnfriendCommand(alice.Id, bob.Id), default);

        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.NotNull((await fixture.Db.Friendships.SingleAsync()).EndedAt);
        Assert.Equal(3, await fixture.Db.FriendRequests.CountAsync());
    }

    [Fact]
    public async Task Friend_nickname_and_favorite_are_private_and_control_current_users_sorting()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var alice = await fixture.AddUserAsync("Alice", "0900000031");
        var bob = await fixture.AddUserAsync("Bob", "0900000032");
        var carol = await fixture.AddUserAsync("Carol", "0900000033");
        await fixture.AddFriendshipAsync(alice.Id, bob.Id);
        await fixture.AddFriendshipAsync(alice.Id, carol.Id);
        var preferences = new UpdateFriendPreferenceCommandHandler(fixture.Db);

        await preferences.Handle(new UpdateFriendPreferenceCommand(alice.Id, bob.Id,
            new UpdateFriendPreferenceRequest("B riêng", true)), default);
        var aliceList = await new GetFriendsQueryHandler(fixture.Db).Handle(new GetFriendsQuery(alice.Id), default);
        var bobList = await new GetFriendsQueryHandler(fixture.Db).Handle(new GetFriendsQuery(bob.Id), default);

        Assert.Equal(bob.Id, aliceList[0].UserId);
        Assert.True(aliceList[0].IsFavorite);
        Assert.Equal("B riêng", aliceList[0].Nickname);
        Assert.Null(Assert.Single(bobList).Nickname);
        Assert.False(Assert.Single(bobList).IsFavorite);
    }

    [Fact]
    public async Task Group_invite_requires_an_accepted_friend()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000041");
        var candidate = await fixture.AddUserAsync("Candidate", "0900000042");
        var group = await fixture.AddGroupAsync(owner.Id, "Trip");
        var handler = new InviteGroupMemberCommandHandler(fixture.Db, fixture.Clock, fixture.Notifications, fixture.Access);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new InviteGroupMemberCommand(owner.Id, group.Id, new InviteGroupRequest(candidate.Id)), default));
        await fixture.AddFriendshipAsync(owner.Id, candidate.Id);
        var invite = await handler.Handle(
            new InviteGroupMemberCommand(owner.Id, group.Id, new InviteGroupRequest(candidate.Id)), default);

        Assert.Equal(candidate.Id, invite.InvitedUserId);
        Assert.Single(await fixture.Db.GroupInvites.ToListAsync());
    }

    [Fact]
    public async Task Group_creator_becomes_owner_but_system_admin_cannot_become_group_member()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Owner", "0900000043");
        var admin = await fixture.AddUserAsync("Admin", "0900000044", SystemRole.SystemAdmin);
        var handler = new CreateGroupCommandHandler(fixture.Db, fixture.Clock, fixture.Access, fixture.Security);

        var group = await handler.Handle(new CreateGroupCommand(user.Id,
            new CreateGroupRequest("Friends trip", null, "Trip", SecurityPin: Pin)), default);

        Assert.Equal(user.Id, group.OwnerId);
        Assert.Equal("Owner", Assert.Single(group.Members).Role);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new CreateGroupCommand(admin.Id,
            new CreateGroupRequest("Admin group", null, "Other", SecurityPin: Pin)), default));
    }

    [Fact]
    public async Task Bank_direct_p2p_requires_active_bank_rejects_self_and_never_changes_wallets()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var sender = await fixture.AddUserAsync("Sender", "0900000051");
        var recipient = await fixture.AddUserAsync("Recipient", "0900000052");
        var senderWallet = await fixture.AddWalletAsync(sender.Id, 10_000);
        var recipientWallet = await fixture.AddWalletAsync(recipient.Id, 5_000);
        var handler = fixture.CreateTransferHandler();

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new CreateBankDirectTransferCommand(sender.Id, null,
                new CreateBankDirectTransferRequest(recipient.Id, 2_000, "P2P", "p2p-no-bank")), default));
        await fixture.AddBankAsync(recipient.Id);
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CreateBankDirectTransferCommand(sender.Id, null,
                new CreateBankDirectTransferRequest(sender.Id, 2_000, "Self", "p2p-self")), default));
        var payment = await handler.Handle(new CreateBankDirectTransferCommand(sender.Id, null,
            new CreateBankDirectTransferRequest(recipient.Id, 2_000, "P2P", "p2p-1")), default);

        Assert.Equal("Pending", payment.Status);
        Assert.Equal(10_000, (await fixture.Db.Wallets.FindAsync(senderWallet.Id))!.AvailableBalance);
        Assert.Equal(5_000, (await fixture.Db.Wallets.FindAsync(recipientWallet.Id))!.AvailableBalance);
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Sender_confirmation_requires_pin_and_does_not_claim_completion()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var sender = await fixture.AddUserAsync("Sender", "0900000061");
        var recipient = await fixture.AddUserAsync("Recipient", "0900000062");
        await fixture.AddBankAsync(recipient.Id);
        var payment = await fixture.CreateTransferHandler().Handle(new CreateBankDirectTransferCommand(sender.Id, null,
            new CreateBankDirectTransferRequest(recipient.Id, 3_000, null, "confirm-1")), default);
        var confirm = fixture.ConfirmHandler();

        await Assert.ThrowsAsync<AppException>(() => confirm.Handle(new ConfirmBankDirectPaymentCommand(sender.Id,
            payment.Id, new ConfirmBankDirectPaymentRequest("000000")), default));
        var storedBefore = await fixture.Db.BankDirectPayments.AsNoTracking().SingleAsync();
        Assert.Equal(BankDirectPaymentStatus.Pending, storedBefore.Status);
        var confirmed = await confirm.Handle(new ConfirmBankDirectPaymentCommand(sender.Id,
            payment.Id, new ConfirmBankDirectPaymentRequest(Pin)), default);

        Assert.Equal("AwaitingReconciliation", confirmed.Status);
        Assert.NotNull(confirmed.SenderConfirmedAt);
        Assert.Null(confirmed.CompletedAt);
    }

    [Fact]
    public async Task Reconciliation_requires_exact_credit_amount_bank_and_reference_and_completes_once()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var sender = await fixture.AddUserAsync("Sender", "0900000071");
        var recipient = await fixture.AddUserAsync("Recipient", "0900000072");
        var bank = await fixture.AddBankAsync(recipient.Id);
        var payment = await fixture.CreateTransferHandler().Handle(new CreateBankDirectTransferCommand(sender.Id, null,
            new CreateBankDirectTransferRequest(recipient.Id, 4_000, "Exact", "reconcile-1")), default);
        await fixture.ConfirmHandler().Handle(new ConfirmBankDirectPaymentCommand(sender.Id, payment.Id,
            new ConfirmBankDirectPaymentRequest(Pin)), default);

        Assert.False(await fixture.ReconcileAsync(bank, 4_001, payment.PaymentReference));
        Assert.False(await fixture.ReconcileAsync(bank, 4_000, "WRONG-REFERENCE"));
        Assert.False(await fixture.ReconcileAsync(bank, 4_000, payment.PaymentReference, BankTransactionDirection.Debit));
        Assert.True(await fixture.ReconcileAsync(bank, 4_000, $"PAY {payment.PaymentReference}"));
        var matched = await fixture.Db.BankDirectPayments.AsNoTracking().SingleAsync(item => item.Id == payment.Id);
        Assert.Equal(BankDirectPaymentStatus.Completed, matched.Status);
        Assert.NotNull(matched.MatchedBankTransactionId);
        var matchedTransaction = await fixture.Db.BankTransactions.AsNoTracking().SingleAsync(item => item.Id == matched.MatchedBankTransactionId);
        Assert.False(await fixture.Reconciler.ReconcileAsync(matchedTransaction, default));
        Assert.Equal(1, await fixture.Db.BankDirectPayments.CountAsync(item => item.Status == BankDirectPaymentStatus.Completed));
    }

    [Fact]
    public async Task Payment_history_persists_recipient_bank_snapshot()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var sender = await fixture.AddUserAsync("Sender", "0900000081");
        var recipient = await fixture.AddUserAsync("Recipient", "0900000082");
        var bank = await fixture.AddBankAsync(recipient.Id, "Original Bank", "******8888");
        var payment = await fixture.CreateTransferHandler().Handle(new CreateBankDirectTransferCommand(sender.Id, null,
            new CreateBankDirectTransferRequest(recipient.Id, 5_000, "History", "history-1")), default);
        bank.BankBrandName = "Renamed Bank";
        bank.MaskedAccountNumber = "******9999";
        await fixture.Db.SaveChangesAsync();

        var history = await new GetBankDirectPaymentHistoryQueryHandler(fixture.Db).Handle(
            new GetBankDirectPaymentHistoryQuery(sender.Id, new BankDirectPaymentHistoryFilter()), default);
        var item = Assert.Single(history.Items);
        Assert.Equal(payment.Id, item.Id);
        Assert.Equal("Original Bank", item.BankBrand);
        Assert.Equal("******8888", item.MaskedAccount);
    }

    [Fact]
    public async Task Receiving_bank_is_owner_only_active_and_ownership_transfer_invalidates_it()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000091");
        var member = await fixture.AddUserAsync("Member", "0900000092");
        var group = await fixture.AddGroupAsync(owner.Id, "Family", member.Id);
        var ownerBank = await fixture.AddBankAsync(owner.Id);
        var memberBank = await fixture.AddBankAsync(member.Id);
        var set = new SetGroupReceivingBankCommandHandler(fixture.Db, fixture.Access);

        await Assert.ThrowsAsync<ForbiddenException>(() => set.Handle(new SetGroupReceivingBankCommand(member.Id,
            group.Id, new GroupReceivingBankRequest(memberBank.Id)), default));
        var selected = await set.Handle(new SetGroupReceivingBankCommand(owner.Id, group.Id,
            new GroupReceivingBankRequest(ownerBank.Id)), default);
        Assert.Equal(owner.Id, selected.OwnerUserId);
        await new TransferGroupOwnershipCommandHandler(fixture.Db, fixture.Access).Handle(
            new TransferGroupOwnershipCommand(owner.Id, group.Id, member.Id), default);

        Assert.Empty(await fixture.Db.GroupReceivingBanks.ToListAsync());
    }

    [Fact]
    public async Task Wallet_group_contribution_credits_fund_once_and_creates_no_bank_payment()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000101");
        var member = await fixture.AddUserAsync("Member", "0900000102");
        var memberWallet = await fixture.AddWalletAsync(member.Id, 2_000);
        var group = await fixture.AddGroupAsync(owner.Id, "Fund", member.Id);
        var create = new CreateFundContributionPaymentCommandHandler(fixture.Accounting);
        var contribution = await create.Handle(new CreateFundContributionPaymentCommand(member.Id, group.Id,
            new CreateFundContributionPaymentRequest(700, "Contribution", "group-contribution-1", memberWallet.Id, Pin)), default);
        var replay = await create.Handle(new CreateFundContributionPaymentCommand(member.Id, group.Id,
            new CreateFundContributionPaymentRequest(700, "Contribution", "group-contribution-1", memberWallet.Id, Pin)), default);

        Assert.Equal(contribution.Id, replay.Id);
        Assert.Equal(700, (await fixture.Db.SharedFunds.SingleAsync(item => item.GroupId == group.Id)).Balance);
        Assert.Equal(1_300, (await fixture.Db.Wallets.FindAsync(memberWallet.Id))!.AvailableBalance);
        Assert.Equal(700, (await fixture.Db.GroupFundShares.SingleAsync()).RemainingAmount);
        Assert.Empty(await fixture.Db.BankDirectPayments.ToListAsync());
    }

    [Fact]
    public async Task Wallet_group_contribution_enforces_wallet_ownership_amount_key_and_conflicting_replay()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000103");
        var member = await fixture.AddUserAsync("Member", "0900000104");
        var ownerWallet = await fixture.AddWalletAsync(owner.Id, 1_000);
        var memberWallet = await fixture.AddWalletAsync(member.Id, 1_000);
        var group = await fixture.AddGroupAsync(owner.Id, "Validated fund", member.Id);
        var handler = new CreateFundContributionPaymentCommandHandler(fixture.Accounting);

        await handler.Handle(new CreateFundContributionPaymentCommand(owner.Id, group.Id,
            new CreateFundContributionPaymentRequest(300, "Owner share", "owner-share", ownerWallet.Id, Pin)), default);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new CreateFundContributionPaymentCommand(member.Id,
            group.Id, new CreateFundContributionPaymentRequest(100, null, "foreign-wallet", ownerWallet.Id, Pin)), default));
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(new CreateFundContributionPaymentCommand(member.Id,
            group.Id, new CreateFundContributionPaymentRequest(100, null, " ", memberWallet.Id, Pin)), default));
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new CreateFundContributionPaymentCommand(member.Id,
            group.Id, new CreateFundContributionPaymentRequest(1_001, null, "insufficient", memberWallet.Id, Pin)), default));
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new CreateFundContributionPaymentCommand(owner.Id,
            group.Id, new CreateFundContributionPaymentRequest(301, "Owner share", "owner-share", ownerWallet.Id, Pin)), default));

        var fund = await fixture.Db.SharedFunds.Include(item => item.LedgerAccount).SingleAsync(item => item.GroupId == group.Id);
        Assert.Equal(300, fund.Balance);
        Assert.Equal(fund.Balance, fund.LedgerAccount.Balance);
        Assert.Equal(700, (await fixture.Db.Wallets.FindAsync(ownerWallet.Id))!.AvailableBalance);
        Assert.Equal(1_000, (await fixture.Db.Wallets.FindAsync(memberWallet.Id))!.AvailableBalance);
    }

    [Fact]
    public async Task Failed_outer_financial_transaction_rolls_back_contribution_posting_and_history()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000107");
        var wallet = await fixture.AddWalletAsync(owner.Id, 1_000);
        var group = await fixture.AddGroupAsync(owner.Id, "Rollback fund");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.ExecuteInTransactionAsync(async token =>
        {
            await fixture.Accounting.ContributeAsync(owner.Id, group.Id, wallet.Id, 400, "Rollback",
                "rollback-contribution", Pin, null, token);
            if (wallet.Id != Guid.Empty)
                throw new InvalidOperationException("Forced failure after posting.");
            return true;
        }, default));

        fixture.Db.ClearTrackedEntities();
        Assert.Equal(1_000, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        var fund = await fixture.Db.SharedFunds.Include(item => item.LedgerAccount).SingleAsync(item => item.GroupId == group.Id);
        Assert.Equal(0, fund.Balance);
        Assert.Equal(0, fund.LedgerAccount.Balance);
        Assert.Empty(await fixture.Db.GroupContributions.ToListAsync());
        Assert.Empty(await fixture.Db.GroupFundShares.ToListAsync());
        Assert.Empty(await fixture.Db.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Group_expense_rounding_is_deterministic_and_conserves_every_cent()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000108");
        var member = await fixture.AddUserAsync("Member", "0900000109");
        var ownerWallet = await fixture.AddWalletAsync(owner.Id, 10);
        var memberWallet = await fixture.AddWalletAsync(member.Id, 10);
        var group = await fixture.AddGroupAsync(owner.Id, "Rounding", member.Id);
        await fixture.Accounting.ContributeAsync(owner.Id, group.Id, ownerWallet.Id, 1, "Owner", "rounding-owner", Pin, null, default);
        await fixture.Accounting.ContributeAsync(member.Id, group.Id, memberWallet.Id, 2, "Member", "rounding-member", Pin, null, default);
        var handler = new CreateCurrentGroupExpenseCommandHandler(fixture.Db, fixture.Access, fixture.Clock,
            fixture.Security, fixture.Accounting, fixture.Ledger);

        var expense = await handler.Handle(new CreateCurrentGroupExpenseCommand(owner.Id, group.Id,
            new CreateGroupExpenseRequest("Rounded", 1, null, "Other", "fund", null, null, null, Pin, "rounding-expense")), default);

        var allocations = await fixture.Db.GroupExpenseShareAllocations.Where(item => item.GroupExpenseId == expense.Id)
            .OrderBy(item => item.UserId).ToListAsync();
        Assert.Equal(1, allocations.Sum(item => item.Amount));
        Assert.True((allocations.Any(item => item.Amount == 0.34m) && allocations.Any(item => item.Amount == 0.66m))
            || (allocations.Any(item => item.Amount == 0.33m) && allocations.Any(item => item.Amount == 0.67m)));
        Assert.Equal(2, (await fixture.Db.SharedFunds.SingleAsync(item => item.GroupId == group.Id)).Balance);
        Assert.Equal(2, await fixture.Db.GroupFundShares.SumAsync(item => item.RemainingAmount));
    }

    [Fact]
    public async Task Pending_legacy_group_bank_payment_cannot_credit_wallet_funded_group()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000105");
        var member = await fixture.AddUserAsync("Member", "0900000106");
        var group = await fixture.AddGroupAsync(owner.Id, "Legacy", member.Id);
        var bank = await fixture.AddBankAsync(owner.Id);
        var payment = await fixture.Payments.CreateAsync(new BankDirectPaymentDraft(member.Id, owner, bank, 500,
            "Legacy contribution", "legacy-group-bank", BankDirectPaymentContextType.GroupContribution, group.Id), default);
        await fixture.ConfirmHandler().Handle(new ConfirmBankDirectPaymentCommand(member.Id, payment.Id,
            new ConfirmBankDirectPaymentRequest(Pin)), default);

        Assert.False(await fixture.ReconcileAsync(bank, 500, payment.PaymentReference));
        Assert.Equal(0, (await fixture.Db.SharedFunds.SingleAsync(item => item.GroupId == group.Id)).Balance);
        Assert.Empty(await fixture.Db.FundTransactions.ToListAsync());
    }

    [Fact]
    public async Task Fund_request_is_exactly_once_and_blocks_member_removal_while_pending()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000111");
        var member = await fixture.AddUserAsync("Member", "0900000112");
        var group = await fixture.AddGroupAsync(owner.Id, "Request", member.Id);
        var wallet = await fixture.AddWalletAsync(member.Id, 2_000);
        var requestHandler = new CreateFundRequestCommandHandler(fixture.Db, fixture.Access, fixture.Clock, fixture.Notifications);
        var first = await requestHandler.Handle(new CreateFundRequestCommand(owner.Id, group.Id,
            new CreateFundRequestRequest("Rent", 900, fixture.Clock.UtcNow.AddDays(2), [member.Id])), default);

        await Assert.ThrowsAsync<ConflictException>(() => new RemoveGroupMemberCommandHandler(fixture.Db, fixture.Access)
            .Handle(new RemoveGroupMemberCommand(owner.Id, group.Id, member.Id), default));
        var payment = await new PayFundRequestCommandHandler(fixture.Db, fixture.Accounting).Handle(
            new PayFundRequestCommand(member.Id, group.Id, first.Id,
                new PayFundRequestRequest("fund-request-1", wallet.Id, Pin)), default);
        var replay = await new PayFundRequestCommandHandler(fixture.Db, fixture.Accounting).Handle(
            new PayFundRequestCommand(member.Id, group.Id, first.Id,
                new PayFundRequestRequest("fund-request-1", wallet.Id, Pin)), default);
        Assert.Equal(payment.Id, replay.Id);
        await Assert.ThrowsAsync<ConflictException>(() => new PayFundRequestCommandHandler(fixture.Db, fixture.Accounting).Handle(
            new PayFundRequestCommand(member.Id, group.Id, first.Id,
                new PayFundRequestRequest("fund-request-other", wallet.Id, Pin)), default));

        var recipient = await fixture.Db.FundRequestRecipients.SingleAsync();
        Assert.Equal(FundRequestRecipientStatus.Paid, recipient.Status);
        Assert.Equal(900, (await fixture.Db.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(1_100, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        Assert.Single(await fixture.Db.FundTransactions.Where(item => item.LedgerTransactionId == payment.LedgerTransactionId).ToListAsync());
        await Assert.ThrowsAsync<ConflictException>(() => new RemoveGroupMemberCommandHandler(fixture.Db, fixture.Access)
            .Handle(new RemoveGroupMemberCommand(owner.Id, group.Id, member.Id), default));
    }

    [Fact]
    public async Task Fund_round_splits_vnd_remainder_and_supports_partial_then_full_payment()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000121");
        var member = await fixture.AddUserAsync("Member", "0900000122");
        var group = await fixture.AddGroupAsync(owner.Id, "Round", member.Id);
        var wallet = await fixture.AddWalletAsync(member.Id, 1_000);
        var round = await new CreateFundRoundCommandHandler(fixture.Db, fixture.Access, fixture.Notifications).Handle(
            new CreateFundRoundCommand(owner.Id, group.Id, new CreateFundRoundRequest("Round 101", null, 101)), default);

        Assert.Equal(101, round.Members.Sum(item => item.RequiredAmount));
        var ordered = round.Members.OrderBy(item => item.UserId).ToList();
        Assert.Equal(51, ordered[0].RequiredAmount);
        Assert.Equal(50, ordered[1].RequiredAmount);
        var memberShare = round.Members.Single(item => item.UserId == member.Id).RequiredAmount;
        var pay = new PayFundRoundCommandHandler(fixture.Db, fixture.Accounting, fixture.Clock);
        var partial = await pay.Handle(new PayFundRoundCommand(member.Id, group.Id, round.Id,
            new PayFundRoundRequest(20, "round-partial", wallet.Id, Pin)), default);
        var partialReplay = await pay.Handle(new PayFundRoundCommand(member.Id, group.Id, round.Id,
            new PayFundRoundRequest(20, "round-partial", wallet.Id, Pin)), default);
        Assert.Equal(partial.Id, partialReplay.Id);
        var remainder = memberShare - 20;
        var full = await pay.Handle(new PayFundRoundCommand(member.Id, group.Id, round.Id,
            new PayFundRoundRequest(remainder, "round-full", wallet.Id, Pin)), default);

        var storedMember = await fixture.Db.GroupFundRoundMembers.SingleAsync(item => item.UserId == member.Id);
        Assert.Equal(memberShare, storedMember.PaidAmount);
        Assert.Equal(GroupFundRoundMemberStatus.Paid, storedMember.Status);
        Assert.Equal(memberShare, (await fixture.Db.GroupFundRounds.SingleAsync()).CollectedAmount);
    }

    [Fact]
    public async Task Group_expense_from_fund_checks_balance_and_member_paid_creates_no_wallet_or_debt()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000131");
        var member = await fixture.AddUserAsync("Member", "0900000132");
        var ownerWallet = await fixture.AddWalletAsync(owner.Id, 1_000);
        var wallet = await fixture.AddWalletAsync(member.Id, 10_000);
        var group = await fixture.AddGroupAsync(owner.Id, "Expense", member.Id);
        await fixture.Accounting.ContributeAsync(owner.Id, group.Id, ownerWallet.Id, 300, "Owner", "expense-owner", Pin, null, default);
        await fixture.Accounting.ContributeAsync(member.Id, group.Id, wallet.Id, 700, "Member", "expense-member", Pin, null, default);
        var handler = new CreateCurrentGroupExpenseCommandHandler(fixture.Db, fixture.Access, fixture.Clock,
            fixture.Security, fixture.Accounting, fixture.Ledger);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new CreateCurrentGroupExpenseCommand(owner.Id,
            group.Id, new CreateGroupExpenseRequest("Too much", 1_001, null, "Other", "fund", null, null, null, Pin, "too-much")), default));
        await handler.Handle(new CreateCurrentGroupExpenseCommand(owner.Id, group.Id,
            new CreateGroupExpenseRequest("Fund expense", 400, null, "Other", "fund", null, null, null, Pin, "fund-expense")), default);
        await handler.Handle(new CreateCurrentGroupExpenseCommand(member.Id, group.Id,
            new CreateGroupExpenseRequest("External expense", 300, null, "Other", "user", member.Id, null, null, Pin)), default);

        Assert.Equal(600, (await fixture.Db.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(9_300, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        Assert.Equal(2, await fixture.Db.GroupExpenses.CountAsync());
        Assert.Single(await fixture.Db.FundTransactions.Where(item => item.Type == FundTransactionType.Expense).ToListAsync());
        var allocations = await fixture.Db.GroupExpenseShareAllocations.OrderBy(item => item.UserId).ToListAsync();
        Assert.Equal(400, allocations.Sum(item => item.Amount));
        Assert.Equal(180, (await fixture.Db.GroupFundShares.SingleAsync(item => item.UserId == owner.Id)).RemainingAmount);
        Assert.Equal(420, (await fixture.Db.GroupFundShares.SingleAsync(item => item.UserId == member.Id)).RemainingAmount);
    }

    [Fact]
    public async Task Group_member_can_spend_fund_but_non_member_cannot_and_wallets_are_untouched()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000133");
        var member = await fixture.AddUserAsync("Member", "0900000134");
        var outsider = await fixture.AddUserAsync("Outsider", "0900000135");
        var ownerWallet = await fixture.AddWalletAsync(owner.Id, 1_000);
        var memberWallet = await fixture.AddWalletAsync(member.Id, 1_000);
        var group = await fixture.AddGroupAsync(owner.Id, "Member spending", member.Id);
        await fixture.Accounting.ContributeAsync(owner.Id, group.Id, ownerWallet.Id, 600, "Owner", "member-spend-owner", Pin, null, default);
        await fixture.Accounting.ContributeAsync(member.Id, group.Id, memberWallet.Id, 400, "Member", "member-spend-member", Pin, null, default);
        var handler = new CreateCurrentGroupExpenseCommandHandler(fixture.Db, fixture.Access, fixture.Clock,
            fixture.Security, fixture.Accounting, fixture.Ledger);

        await handler.Handle(new CreateCurrentGroupExpenseCommand(member.Id, group.Id,
            new CreateGroupExpenseRequest("Member fund expense", 100, null, "Other", "fund", null, null, null, Pin, "member-fund-expense")), default);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new CreateCurrentGroupExpenseCommand(outsider.Id, group.Id,
            new CreateGroupExpenseRequest("Outsider fund expense", 10, null, "Other", "fund", null, null, null, Pin, "outsider-fund-expense")), default));

        Assert.Equal(900, (await fixture.Db.SharedFunds.SingleAsync(item => item.GroupId == group.Id)).Balance);
        Assert.Equal(400, (await fixture.Db.Wallets.FindAsync(ownerWallet.Id))!.AvailableBalance);
        Assert.Equal(600, (await fixture.Db.Wallets.FindAsync(memberWallet.Id))!.AvailableBalance);
        var history = await fixture.Db.FundTransactions.SingleAsync(item => item.Type == FundTransactionType.Expense);
        Assert.Equal(member.Id, history.UserId);
        Assert.Equal(member.FullName, history.UserNameSnapshot);
    }

    [Fact]
    public async Task Fund_expense_idempotency_replays_without_spending_twice_and_conflicts_on_payload_change()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000136");
        var wallet = await fixture.AddWalletAsync(owner.Id, 1_000);
        var group = await fixture.AddGroupAsync(owner.Id, "Expense replay", balance: 500);
        var handler = new CreateCurrentGroupExpenseCommandHandler(fixture.Db, fixture.Access, fixture.Clock,
            fixture.Security, fixture.Accounting, fixture.Ledger);
        var request = new CreateGroupExpenseRequest("Replay expense", 100, null, "Other", "fund", null, null, null, Pin, "expense-replay");

        var first = await handler.Handle(new CreateCurrentGroupExpenseCommand(owner.Id, group.Id, request), default);
        var replay = await handler.Handle(new CreateCurrentGroupExpenseCommand(owner.Id, group.Id, request), default);
        Assert.Equal(first.Id, replay.Id);
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new CreateCurrentGroupExpenseCommand(owner.Id, group.Id,
            request with { Amount = 101 }), default));
        Assert.Equal(400, (await fixture.Db.SharedFunds.SingleAsync(item => item.GroupId == group.Id)).Balance);
        Assert.Single(await fixture.Db.GroupExpenses.Where(item => item.IdempotencyKey == "expense-replay").ToListAsync());
    }

    [Fact]
    public async Task Fund_expense_same_key_from_different_member_is_a_controlled_conflict()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000137");
        var member = await fixture.AddUserAsync("Member", "0900000138");
        var ownerWallet = await fixture.AddWalletAsync(owner.Id, 500);
        var memberWallet = await fixture.AddWalletAsync(member.Id, 500);
        var group = await fixture.AddGroupAsync(owner.Id, "Cross actor", member.Id, 500);
        await fixture.Accounting.ContributeAsync(owner.Id, group.Id, ownerWallet.Id, 250, "Owner", "cross-owner", Pin, null, default);
        await fixture.Accounting.ContributeAsync(member.Id, group.Id, memberWallet.Id, 250, "Member", "cross-member", Pin, null, default);
        var handler = new CreateCurrentGroupExpenseCommandHandler(fixture.Db, fixture.Access, fixture.Clock,
            fixture.Security, fixture.Accounting, fixture.Ledger);
        var request = new CreateGroupExpenseRequest("Shared", 100, null, "Other", "fund", null, null, null, Pin, "cross-actor-key");
        var first = await handler.Handle(new CreateCurrentGroupExpenseCommand(owner.Id, group.Id, request), default);
        fixture.Db.GroupActivities.Add(new GroupActivity
        {
            GroupId = group.Id,
            ActorUserId = member.Id,
            Type = GroupActivityType.FundExpense,
            DataJson = JsonSerializer.Serialize(new { title = $"unrelated-{first.Id}" })
        });
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new CreateCurrentGroupExpenseCommand(member.Id, group.Id, request), default));
        Assert.Single(await fixture.Db.GroupExpenses.Where(item => item.IdempotencyKey == "cross-actor-key").ToListAsync());
        Assert.Equal(900, (await fixture.Db.SharedFunds.SingleAsync(item => item.GroupId == group.Id)).Balance);
    }

    [Fact]
    public async Task Group_deletion_votes_create_deterministic_refunds_and_require_explicit_finalize()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000141");
        var member = await fixture.AddUserAsync("Member", "0900000142");
        var ownerWallet = await fixture.AddWalletAsync(owner.Id, 0);
        var memberWallet = await fixture.AddWalletAsync(member.Id, 0);
        var group = await fixture.AddGroupAsync(owner.Id, "Delete", member.Id, 100);
        var ownerShare = await fixture.Db.GroupFundShares.SingleAsync(item => item.GroupId == group.Id && item.UserId == owner.Id);
        ownerShare.RemainingAmount = 30;
        fixture.Db.GroupFundShares.Add(new GroupFundShare { GroupId = group.Id, UserId = member.Id, RemainingAmount = 70 });
        await fixture.Db.SaveChangesAsync();
        var deletion = new CreateGroupDeletionRequestCommandHandler(fixture.Db, fixture.Access, fixture.Clock,
            fixture.Notifications, fixture.Deletion);
        var rejectedRequest = await deletion.Handle(new CreateGroupDeletionRequestCommand(owner.Id, group.Id), default);
        Assert.Equal("Approved", rejectedRequest.Votes.Single(item => item.UserId == owner.Id).Status);
        var rejected = await new VoteGroupDeletionCommandHandler(fixture.Db, fixture.Clock, fixture.Notifications,
            fixture.Deletion, fixture.Security).Handle(new VoteGroupDeletionCommand(member.Id, group.Id, rejectedRequest.Id,
                new GroupDeletionVoteRequest(false, Pin)), default);
        Assert.Equal("Rejected", rejected.Status);

        var request = await deletion.Handle(new CreateGroupDeletionRequestCommand(owner.Id, group.Id), default);
        var ready = await new VoteGroupDeletionCommandHandler(fixture.Db, fixture.Clock, fixture.Notifications,
            fixture.Deletion, fixture.Security).Handle(new VoteGroupDeletionCommand(member.Id, group.Id, request.Id,
                new GroupDeletionVoteRequest(true, Pin)), default);
        Assert.Equal("Ready", ready.Status);
        Assert.Equal(100, ready.Refunds.Sum(item => item.RefundAmount));
        Assert.Equal(30, ready.Refunds.Single(item => item.UserId == owner.Id).RefundAmount);
        Assert.Equal(70, ready.Refunds.Single(item => item.UserId == member.Id).RefundAmount);
        Assert.Equal(GroupStatus.DeletionPending, (await fixture.Db.Groups.FindAsync(group.Id))!.Status);

        var refundHandler = new CreateGroupRefundPaymentCommandHandler(fixture.Db, fixture.Access,
            fixture.Notifications, fixture.Security, fixture.Accounting, fixture.Ledger, fixture.Clock);
        var ownerRefund = ready.Refunds.Single(item => item.UserId == owner.Id);
        var ownerPayment = await refundHandler.Handle(new CreateGroupRefundPaymentCommand(owner.Id, group.Id,
            request.Id, ownerRefund.Id, new CreateGroupRefundPaymentRequest("refund-owner", Pin, ownerWallet.Id)), default);
        var ownerReplay = await refundHandler.Handle(new CreateGroupRefundPaymentCommand(owner.Id, group.Id,
            request.Id, ownerRefund.Id, new CreateGroupRefundPaymentRequest("refund-owner", Pin, ownerWallet.Id)), default);
        Assert.Equal(ownerPayment.Id, ownerReplay.Id);
        Assert.Equal(GroupStatus.DeletionPending, (await fixture.Db.Groups.FindAsync(group.Id))!.Status);
        var memberRefund = ready.Refunds.Single(item => item.UserId == member.Id);
        memberWallet.Status = WalletStatus.Closed;
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => refundHandler.Handle(new CreateGroupRefundPaymentCommand(owner.Id,
            group.Id, request.Id, memberRefund.Id, new CreateGroupRefundPaymentRequest("refund-member", Pin)), default));
        Assert.Equal(GroupRefundStatus.Pending, (await fixture.Db.GroupRefunds.FindAsync(memberRefund.Id))!.Status);
        var refundWallet = WalletFactory.Create(member.Id, "Refund wallet", WalletType.Savings, false);
        fixture.Db.Wallets.Add(refundWallet);
        await fixture.Db.SaveChangesAsync();
        var memberPayment = await refundHandler.Handle(new CreateGroupRefundPaymentCommand(owner.Id, group.Id,
            request.Id, memberRefund.Id, new CreateGroupRefundPaymentRequest("refund-member", Pin, refundWallet.Id)), default);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(GroupStatus.DeletionPending, (await fixture.Db.Groups.FindAsync(group.Id))!.Status);
        await new FinalizeGroupDeletionCommandHandler(fixture.Db, fixture.Access, fixture.Security, fixture.Clock)
            .Handle(new FinalizeGroupDeletionCommand(owner.Id, group.Id, request.Id,
                new FinalizeGroupDeletionRequest(Pin)), default);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(GroupStatus.Closed, (await fixture.Db.Groups.FindAsync(group.Id))!.Status);
        Assert.Equal(0, (await fixture.Db.SharedFunds.SingleAsync(item => item.GroupId == group.Id)).Balance);
        Assert.Equal(2, await fixture.Db.GroupRefunds.CountAsync(item => item.Status == GroupRefundStatus.Completed));
        Assert.Equal(30, (await fixture.Db.Wallets.FindAsync(ownerWallet.Id))!.AvailableBalance);
        Assert.Equal(0, (await fixture.Db.Wallets.FindAsync(memberWallet.Id))!.AvailableBalance);
        Assert.Equal(70, (await fixture.Db.Wallets.FindAsync(refundWallet.Id))!.AvailableBalance);
        Assert.Empty(await fixture.Db.BankDirectPayments.ToListAsync());
    }

    [Fact]
    public async Task Refund_plan_uses_remaining_member_shares_without_equal_fallback()
    {
        await using var fixture = await PhaseFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner", "0900000151");
        var member = await fixture.AddUserAsync("Member", "0900000152");
        var group = await fixture.AddGroupAsync(owner.Id, "Fallback", member.Id, 5);
        var request = await new CreateGroupDeletionRequestCommandHandler(fixture.Db, fixture.Access, fixture.Clock,
            fixture.Notifications, fixture.Deletion).Handle(new CreateGroupDeletionRequestCommand(owner.Id, group.Id), default);
        var ready = await new VoteGroupDeletionCommandHandler(fixture.Db, fixture.Clock, fixture.Notifications,
            fixture.Deletion, fixture.Security).Handle(new VoteGroupDeletionCommand(member.Id, group.Id, request.Id,
                new GroupDeletionVoteRequest(true, Pin)), default);

        var ordered = ready.Refunds.OrderBy(item => item.UserId).ToList();
        Assert.Equal(5, ordered.Sum(item => item.RefundAmount));
        Assert.Equal(5, ready.Refunds.Single(item => item.UserId == owner.Id).RefundAmount);
        Assert.Equal(0, ready.Refunds.Single(item => item.UserId == member.Id).RefundAmount);
    }

    private sealed class PhaseFixture : IAsyncDisposable
    {
        private static readonly string PinHash = new SecurityPinService().Hash(Pin);
        private readonly SqliteConnection connection;
        private int bankSequence;

        private PhaseFixture(SqliteConnection connection, SpendWiseDbContext db, FixedClock clock)
        {
            this.connection = connection;
            Db = db;
            Clock = clock;
            Notifications = new NotificationService(db);
            Access = new GroupAccess(db);
            Payments = new BankDirectPaymentSupport(db);
            Security = new FinancialSecurityGuard(db, new SecurityPinService(),
                new AccountCredentialLockout(db, clock));
            Ledger = new LedgerService(db, clock);
            Accounting = new WalletFundAccounting(db, Access, Ledger, Security, clock);
            Reconciler = new BankDirectPaymentReconciler(db, clock, Notifications);
            Deletion = new GroupDeletionSupport(db, clock, Notifications, Accounting);
        }

        public SpendWiseDbContext Db { get; }
        public FixedClock Clock { get; }
        public NotificationService Notifications { get; }
        public GroupAccess Access { get; }
        public BankDirectPaymentSupport Payments { get; }
        public FinancialSecurityGuard Security { get; }
        public LedgerService Ledger { get; }
        public WalletFundAccounting Accounting { get; }
        public BankDirectPaymentReconciler Reconciler { get; }
        public GroupDeletionSupport Deletion { get; }

        public static async Task<PhaseFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var clock = new FixedClock(new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc));
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlite(connection).Options;
            var db = new SpendWiseDbContext(options, clock);
            await db.Database.EnsureCreatedAsync();
            return new PhaseFixture(connection, db, clock);
        }

        public async Task<User> AddUserAsync(string name, string phone,
            SystemRole role = SystemRole.User, UserStatus status = UserStatus.Active)
        {
            var unique = Guid.NewGuid().ToString("N");
            var user = new User
            {
                FullName = name,
                Email = $"{unique}@test.local",
                NormalizedEmail = $"{unique}@test.local".ToUpperInvariant(),
                SpendId = unique,
                NormalizedSpendId = unique.ToUpperInvariant(),
                Phone = phone,
                PasswordHash = "test",
                SecurityPinHash = PinHash,
                SystemRole = role,
                Status = status
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            return user;
        }

        public async Task<Wallet> AddWalletAsync(Guid ownerId, decimal balance)
        {
            var wallet = WalletFactory.Create(ownerId, "Wallet", WalletType.Main, true);
            wallet.AvailableBalance = balance;
            wallet.LedgerAccount.Balance = balance;
            Db.Wallets.Add(wallet);
            await Db.SaveChangesAsync();
            return wallet;
        }

        public async Task<BankConnection> AddBankAsync(Guid userId, string brand = "Test Bank", string masked = "******1234")
        {
            bankSequence++;
            var bank = new BankConnection
            {
                UserId = userId,
                Provider = BankProvider.SePayBankHub,
                ExternalAccountId = $"bank-{userId:N}-{bankSequence}",
                BankBrandName = brand,
                AccountHolderName = "Account Holder",
                MaskedAccountNumber = masked,
                Status = BankConnectionStatus.Active,
                LinkedAt = Clock.UtcNow
            };
            Db.BankConnections.Add(bank);
            await Db.SaveChangesAsync();
            return bank;
        }

        public async Task<Group> AddGroupAsync(Guid ownerId, string name, Guid? memberId = null, decimal balance = 0)
        {
            var account = new LedgerAccount
            {
                Code = $"GROUP-FUND:{Guid.NewGuid():N}",
                Name = name,
                Type = LedgerAccountType.SharedFund,
                Balance = balance
            };
            var group = new Group
            {
                Name = name,
                Type = GroupType.Friends,
                Members = [new GroupMember { UserId = ownerId, Role = GroupRole.Owner, JoinedAt = Clock.UtcNow }],
                SharedFund = new SharedFund { LedgerAccount = account, LedgerAccountId = account.Id, Balance = balance }
            };
            account.GroupId = group.Id;
            group.SharedFund.GroupId = group.Id;
            if (memberId is Guid value)
                group.Members.Add(new GroupMember { UserId = value, Role = GroupRole.Member, JoinedAt = Clock.UtcNow });
            Db.Groups.Add(group);
            if (balance > 0)
                Db.GroupFundShares.Add(new GroupFundShare { GroupId = group.Id, UserId = ownerId, RemainingAmount = balance });
            await Db.SaveChangesAsync();
            return group;
        }

        public async Task AddFriendshipAsync(Guid first, Guid second)
        {
            var lower = first.CompareTo(second) < 0 ? first : second;
            var higher = first.CompareTo(second) < 0 ? second : first;
            var request = new FriendRequest
            {
                RequesterUserId = first,
                AddresseeUserId = second,
                LowerUserId = lower,
                HigherUserId = higher,
                Status = FriendRequestStatus.Accepted,
                ResolvedAt = Clock.UtcNow
            };
            Db.FriendRequests.Add(request);
            Db.Friendships.Add(new Friendship
            {
                LowerUserId = lower,
                HigherUserId = higher,
                AcceptedRequest = request,
                AcceptedRequestId = request.Id,
                AcceptedAt = Clock.UtcNow,
                Preferences =
                [
                    new FriendPreference { UserId = first },
                    new FriendPreference { UserId = second }
                ]
            });
            await Db.SaveChangesAsync();
        }

        public SendFriendRequestCommandHandler SendFriendHandler() => new(Db, Clock, Notifications,
            new SearchFriendContactQueryHandler(Db));

        public CreateBankDirectTransferCommandHandler CreateTransferHandler() =>
            new(Db, Payments, new AllowRisk(), new AllowPolicy());

        public ConfirmBankDirectPaymentCommandHandler ConfirmHandler() => new(Db, Clock, Security, Reconciler);

        public Task ConfigureReceivingBankAsync(Guid ownerId, Guid groupId, Guid bankId) =>
            new SetGroupReceivingBankCommandHandler(Db, Access).Handle(
                new SetGroupReceivingBankCommand(ownerId, groupId, new GroupReceivingBankRequest(bankId)), default);

        public async Task<bool> ReconcileAsync(BankConnection bank, decimal amount, string content,
            BankTransactionDirection direction = BankTransactionDirection.Credit)
        {
            var transaction = new BankTransaction
            {
                UserId = bank.UserId,
                BankConnectionId = bank.Id,
                Provider = BankProvider.SePayBankHub,
                ExternalTransactionId = Guid.NewGuid().ToString("N"),
                Direction = direction,
                Amount = amount,
                TransactionDate = Clock.UtcNow,
                Content = content,
                Source = BankTransactionSource.Webhook,
                ImportedAt = Clock.UtcNow
            };
            Db.BankTransactions.Add(transaction);
            await Db.SaveChangesAsync();
            return await Reconciler.ReconcileAsync(transaction, default);
        }

        public async Task ConfirmAndReconcileAsync(Guid senderId, BankConnection bank, BankDirectPaymentDto payment)
        {
            await ConfirmHandler().Handle(new ConfirmBankDirectPaymentCommand(senderId, payment.Id,
                new ConfirmBankDirectPaymentRequest(Pin)), default);
            Assert.True(await ReconcileAsync(bank, payment.Amount, payment.PaymentReference));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private static class FriendFeatureSupportForTest
    {
        public static async Task<string> StateAsync(ISpendWiseDbContext db, Guid current, Guid target)
        {
            var handler = new SearchFriendContactQueryHandler(db);
            var phone = await db.Users.Where(item => item.Id == target).Select(item => item.Phone).SingleAsync();
            return (await handler.Handle(new SearchFriendContactQuery(current, phone!), default)).State;
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class AllowPolicy : IOperationPolicy
    {
        public Task EnsureFeatureEnabledAsync(string featureName, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken cancellationToken) =>
            Task.FromResult(key == "TransferMinimum" ? 1m : fallback);
    }

    private sealed class AllowRisk : IRiskService
    {
        public Task<RiskAssessment> AssessTransferAsync(Guid userId, decimal amount, string? sessionId,
            CancellationToken cancellationToken) => Task.FromResult(new RiskAssessment(0, "Low", "Allow", [], null));
    }
}
