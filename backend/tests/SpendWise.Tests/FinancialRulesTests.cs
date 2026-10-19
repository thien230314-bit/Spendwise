using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Transfers.Commands.TransferBetweenWallets;
using SpendWise.Application.Features.Goals.Commands;
using SpendWise.Application.Features.Goals.Commands.ContributeGoal;
using SpendWise.Application.Features.Goals.Commands.CreateGoal;
using SpendWise.Application.Features.Goals.Commands.WithdrawGoal;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Features.Wallets.Queries.GetWallets;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Groups.Commands.CreateGroup;
using SpendWise.Application.Features.Groups.Commands.InviteGroupMember;
using SpendWise.Application.Features.Groups.Commands.UpdateGroup;
using SpendWise.Application.Features.Groups.Queries.GetGroupDetails;
using SpendWise.Application.Features.Admin.Dashboard.Queries.GetAdminDashboard;
using SpendWise.Application.Finance;
using SpendWise.Application.Groups;
using SpendWise.Application.Notifications;
using SpendWise.Application.Planning;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Social;
using SpendWise.Infrastructure.Admin;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Planning;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ Ledger cân bằng, phân loại dòng tiền, idempotency và quyền riêng tư.

public sealed class FinancialRulesTests
{
    private const string FinancialPin = "123456";
    private static readonly string FinancialPinHash = new SecurityPinService().Hash(FinancialPin);

    [Fact]
    public async Task Ledger_post_is_balanced_and_atomic()
    {
        // Arrange: tạo tài khoản nguồn 1.000.000 đồng và tài khoản đích bằng 0.
        await using var db = Db();
        var source = Account(1_000_000);
        var destination = Account(0);
        db.AddRange(source, destination);
        await db.SaveChangesAsync();
        var service = new LedgerService(db, new SystemClock());

        // Act: posting 200.000 đồng từ tài khoản nguồn sang tài khoản đích.
        var result = await service.PostAsync(
            source.Id,
            destination.Id,
            200_000,
            "VND",
            "Test",
            Guid.NewGuid(),
            "user:test",
            "post-1",
            "test",
            default);

        var entries = await db.LedgerEntries
            .Where(x => x.LedgerTransactionId == result.LedgerTransactionId)
            .ToListAsync();
        var debitTotal = entries
            .Where(x => x.Direction == LedgerEntryDirection.Debit)
            .Sum(x => x.Amount);
        var creditTotal = entries
            .Where(x => x.Direction == LedgerEntryDirection.Credit)
            .Sum(x => x.Amount);
        // Assert: tổng bên Nợ bằng bên Có và số dư hai tài khoản đổi đúng một lần.
        Assert.Equal(debitTotal, creditTotal);
        Assert.Equal(800_000, source.Balance);
        Assert.Equal(200_000, destination.Balance);
    }

    [Fact]
    public async Task Insufficient_balance_changes_nothing()
    {
        // Arrange: tài khoản nguồn chỉ có 100 đồng nhưng request cần 200 đồng.
        await using var db = Db();
        var source = Account(100);
        var destination = Account(0);
        db.AddRange(source, destination);
        await db.SaveChangesAsync();
        var service = new LedgerService(db, new SystemClock());

        // Act và Assert: posting phải bị từ chối và cả hai số dư giữ nguyên.
        await Assert.ThrowsAsync<ConflictException>(() => service.PostAsync(
            source.Id,
            destination.Id,
            200,
            "VND",
            "Test",
            Guid.NewGuid(),
            "user:test",
            "insufficient",
            "test",
            default));

        Assert.Equal(100, source.Balance);
        Assert.Equal(0, destination.Balance);
        Assert.Empty(db.LedgerTransactions);
    }

 #if RETIRED_LEGACY_TESTS
    [Fact]
    public async Task P2p_transfer_is_idempotent_and_creates_paired_history()
    {
        // Arrange: tạo người gửi, người nhận, hai Wallet và một request P2P có key cố định.
        await using var db = Db();
        var (sender, senderWallet) = AddUserWallet(db, "A", 1_000_000);
        var (recipient, recipientWallet) = AddUserWallet(db, "B", 300_000);
        await db.SaveChangesAsync();
        var service = Transfer(db);
        var request = new CreateTransferRequest(
            senderWallet.Id,
            recipient.Id,
            200_000,
            "Trả nợ",
            "same-key");

        // Act: gửi cùng request P2P hai lần.
        var first = await service.SendAsync(sender.Id, null, request, default);
        var second = await service.SendAsync(sender.Id, null, request, default);

        // Assert: lần hai replay; số dư đổi một lần, có hai lịch sử hai phía và một posting.
        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(800_000, senderWallet.AvailableBalance);
        Assert.Equal(500_000, recipientWallet.AvailableBalance);
        Assert.Equal(
            2,
            await db.FinancialTransactions.CountAsync(x => x.RelatedBusinessId == first.Id));
        Assert.Single(db.LedgerTransactions);
    }

    [Fact]
    public async Task Different_senders_can_use_the_same_p2p_idempotency_key()
    {
        // Arrange: tạo hai người gửi độc lập cùng hai người nhận và hai cặp Wallet.
        await using var db = Db();
        var (senderOne, walletOne) = AddUserWallet(db, "Sender One", 1_000);
        var (recipientOne, _) = AddUserWallet(db, "Recipient One", 0);
        var (senderTwo, walletTwo) = AddUserWallet(db, "Sender Two", 1_000);
        var (recipientTwo, _) = AddUserWallet(db, "Recipient Two", 0);
        await db.SaveChangesAsync();
        var service = Transfer(db);

        // Act: hai người gửi dùng cùng chuỗi key trong scope riêng của họ.
        var first = await service.SendAsync(
            senderOne.Id,
            null,
            new CreateTransferRequest(walletOne.Id, recipientOne.Id, 100, "One", "shared-key"),
            default);
        var second = await service.SendAsync(
            senderTwo.Id,
            null,
            new CreateTransferRequest(walletTwo.Id, recipientTwo.Id, 150, "Two", "shared-key"),
            default);

        // Assert: hệ thống tạo hai Transfer và hai posting khác nhau.
        Assert.False(first.Replayed);
        Assert.False(second.Replayed);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await db.Transfers.CountAsync());
        Assert.Equal(2, await db.LedgerTransactions.CountAsync());
    }

 #endif
    [Fact]
    public async Task Internal_transfer_is_not_income_or_expense()
    {
        // Arrange: tạo hai Wallet cùng chủ với tổng số dư cố định.
        await using var db = Db();
        var (user, sourceWallet) = AddUserWallet(db, "A", 1_000);
        var destinationWallet = WalletFactory.Create(
            user.Id,
            "Savings",
            WalletType.Savings,
            false);
        db.Wallets.Add(destinationWallet);
        await db.SaveChangesAsync();

        var clock = new SystemClock();
        var handler = new TransferBetweenWalletsCommandHandler(
            db,
            new LedgerService(db, clock),
            clock,
            new TestPolicy(),
            new FinancialSecurityGuard(db, new SecurityPinService(),
                new AccountCredentialLockout(db, clock)));
        // Act: chuyển 250 đồng từ Wallet nguồn sang Wallet đích.
        await handler.Handle(
            new TransferBetweenWalletsCommand(
                user.Id,
                new InternalTransferRequest(
                    sourceWallet.Id,
                    destinationWallet.Id,
                    250,
                    "save",
                    "internal-1",
                    FinancialPin)),
            default);

        var history = await db.FinancialTransactions.SingleAsync();
        // Assert: lịch sử là InternalTransfer, không phát sinh Income hoặc Expense.
        Assert.Equal(FinancialTransactionType.InternalTransfer, history.Type);
        Assert.DoesNotContain(
            db.FinancialTransactions,
            x => x.Type is FinancialTransactionType.Income or FinancialTransactionType.Expense);
    }

    [Fact]
    public async Task Goal_contribution_is_not_expense()
    {
        // Arrange: tạo Wallet 1.000 đồng và Goal chưa có tiền phân bổ.
        await using var db = Db();
        var (user, wallet) = AddUserWallet(db, "A", 1_000);
        await db.SaveChangesAsync();
        var clock = new SystemClock();
        var movement = GoalMovement(db, clock);
        var goalHandler = new CreateGoalCommandHandler(db,
            new FinancialSecurityGuard(db, new SecurityPinService(),
                new AccountCredentialLockout(db, clock)), movement, clock);
        var contributeHandler = new ContributeGoalCommandHandler(movement);
        var goal = await goalHandler.Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Goal", 2_000, SecurityPin: FinancialPin)),
            default);

        await contributeHandler.Handle(
            new ContributeGoalCommand(
                user.Id,
                goal.Id,
                new GoalMovementRequest(wallet.Id, 500, "goal-1", FinancialPin)),
            default);

        var history = await db.FinancialTransactions.SingleAsync();
        var storedGoal = await db.Goals.FindAsync(goal.Id);
        // Act và Assert: đóng góp 500 đồng tạo GoalContribution, không tạo Expense.
        Assert.Equal(FinancialTransactionType.GoalContribution, history.Type);
        Assert.Equal(500, storedGoal!.CurrentAllocatedAmount);
    }

    [Fact]
    public async Task Goal_contribution_and_withdrawal_have_independent_key_scopes()
    {
        // Arrange: đóng góp 500 đồng vào Goal với key dùng chung cho bài test.
        await using var db = Db();
        var (user, wallet) = AddUserWallet(db, "A", 1_000);
        await db.SaveChangesAsync();
        var clock = new SystemClock();
        var movement = GoalMovement(db, clock);
        var goalHandler = new CreateGoalCommandHandler(db,
            new FinancialSecurityGuard(db, new SecurityPinService(),
                new AccountCredentialLockout(db, clock)), movement, clock);
        var contributeHandler = new ContributeGoalCommandHandler(movement);
        var withdrawHandler = new WithdrawGoalCommandHandler(movement);
        var goal = await goalHandler.Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Goal", 2_000, SecurityPin: FinancialPin)),
            default);

        await contributeHandler.Handle(
            new ContributeGoalCommand(
                user.Id,
                goal.Id,
                new GoalMovementRequest(wallet.Id, 500, "shared-key", FinancialPin)),
            default);
        // Act: rút 200 đồng bằng cùng chuỗi key nhưng scope Withdraw riêng.
        var result = await withdrawHandler.Handle(
            new WithdrawGoalCommand(
                user.Id,
                goal.Id,
                new GoalMovementRequest(wallet.Id, 200, "shared-key", FinancialPin)),
            default);

        // Assert: cả hai movement đều được ghi và Goal còn 300 đồng.
        Assert.Equal(300, result.CurrentAllocatedAmount);
        Assert.Equal(2, await db.GoalMovements.CountAsync());
        Assert.Equal(2, await db.LedgerTransactions.CountAsync());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Goal_ownership_precedes_foreign_key_replay_and_owner_replay_is_unchanged(
        bool contribute, bool matchingDetails)
    {
        await using var db = Db();
        var (owner, wallet) = AddUserWallet(db, "Owner", 1_000);
        var (outsider, outsiderWallet) = AddUserWallet(db, "Outsider", 1_000);
        await db.SaveChangesAsync();
        var clock = new SystemClock();
        var executor = GoalMovement(db, clock);
        var goal = await new CreateGoalCommandHandler(db,
            new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock)),
            executor, clock).Handle(new CreateGoalCommand(owner.Id,
                new CreateGoalRequest("Private", 2_000, SecurityPin: FinancialPin)), default);
        await executor.MoveAsync(owner.Id, goal.Id, new(wallet.Id, 200, "owned-key", FinancialPin), true, default);
        var request = new GoalMovementRequest(wallet.Id, contribute ? 200 : 50, "owned-key", FinancialPin);
        if (!contribute) await executor.MoveAsync(owner.Id, goal.Id, request, false, default);
        var count = contribute ? 1 : 2;
        var allocated = contribute ? 200m : 150m;

        var error = await Assert.ThrowsAsync<NotFoundException>(() => executor.MoveAsync(outsider.Id,
            goal.Id, matchingDetails ? request : request with { WalletId = outsiderWallet.Id, Amount = 1 },
            contribute, default));
        Assert.Equal("goal_not_found", error.Code);
        Assert.Equal(404, error.StatusCode);
        var replay = await executor.MoveAsync(owner.Id, goal.Id,
            request with { IdempotencyKey = " owned-key " }, contribute, default);
        Assert.Equal(allocated, replay.CurrentAllocatedAmount);
        Assert.Equal(count, await db.GoalMovements.CountAsync());
        Assert.Equal(count, await db.FinancialTransactions.CountAsync());
        Assert.Equal(count, await db.LedgerTransactions.CountAsync());
        Assert.Equal(allocated, (await db.Goals.Include(x => x.LedgerAccount).SingleAsync()).LedgerAccount.Balance);
        Assert.Equal(1_000m - allocated, wallet.AvailableBalance);
        Assert.Equal(1_000m, outsiderWallet.AvailableBalance);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Goal_ownership_guard_handles_new_added_InMemory_goal(bool ownsGoal)
    {
        await using var db = Db();
        var owner = Guid.NewGuid();
        var goal = new SpendWise.Domain.Planning.Goal { OwnerUserId = owner, Name = "New tracked goal", TargetAmount = 100 };
        db.Goals.Add(goal);
        var guard = typeof(GoalMovementExecutor).GetMethod("SerializeGoalAsync",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .CreateDelegate<Func<ISpendWiseDbContext, Guid, Guid, CancellationToken, Task>>();
        if (ownsGoal) await guard(db, owner, goal.Id, default);
        else
        {
            var error = await Assert.ThrowsAsync<NotFoundException>(() => guard(db, Guid.NewGuid(), goal.Id, default));
            Assert.Equal("goal_not_found", error.Code);
        }
        Assert.Equal(EntityState.Added, db.Entry(goal).State);
        Assert.Empty(await db.Goals.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Goal_initial_allocation_preserves_valid_new_tracked_owner()
    {
        await using var db = Db();
        var (owner, wallet) = AddUserWallet(db, "Owner", 1_000);
        await db.SaveChangesAsync();
        var clock = new SystemClock();
        var executor = GoalMovement(db, clock);
        var goal = await new CreateGoalCommandHandler(db,
            new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock)),
            executor, clock).Handle(new CreateGoalCommand(owner.Id,
                new CreateGoalRequest("New goal", 2_000, InitialAmount: 100, WalletId: wallet.Id,
                    IdempotencyKey: "initial-owned", SecurityPin: FinancialPin)), default);
        Assert.Equal(100m, goal.CurrentAllocatedAmount);
        Assert.Equal(900m, wallet.AvailableBalance);
        Assert.Single(await db.GoalMovements.ToListAsync());
    }

    [Fact]
    public async Task Goal_retry_with_different_amount_is_rejected()
    {
        // Arrange: đóng góp 200 đồng vào Goal với IdempotencyKey cố định.
        await using var db = Db();
        var (user, wallet) = AddUserWallet(db, "A", 1_000);
        await db.SaveChangesAsync();
        var clock = new SystemClock();
        var movement = GoalMovement(db, clock);
        var goalHandler = new CreateGoalCommandHandler(db,
            new FinancialSecurityGuard(db, new SecurityPinService(),
                new AccountCredentialLockout(db, clock)), movement, clock);
        var contributeHandler = new ContributeGoalCommandHandler(movement);
        var goal = await goalHandler.Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Goal", 2_000, SecurityPin: FinancialPin)),
            default);
        await contributeHandler.Handle(
            new ContributeGoalCommand(
                user.Id,
                goal.Id,
                new GoalMovementRequest(wallet.Id, 200, "goal-key", FinancialPin)),
            default);

        // Act và Assert: gửi lại key với 201 đồng phải Conflict và Goal vẫn giữ 200 đồng.
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            contributeHandler.Handle(
                new ContributeGoalCommand(
                    user.Id,
                    goal.Id,
                    new GoalMovementRequest(wallet.Id, 201, "goal-key", FinancialPin)),
                default));

        Assert.Equal("idempotency_conflict", error.Code);
        Assert.Equal(200, (await db.Goals.FindAsync(goal.Id))!.CurrentAllocatedAmount);
    }

 #if RETIRED_LEGACY_TESTS
    [Fact]
    public void Split_rules_validate_equal_percentage_exact_and_shares()
    {
        // Arrange: tạo dữ liệu chia đều, phần trăm, số tiền chính xác và số phần.
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var equalInputs = ids
            .Select(x => new ExpenseSplitInput(x, 1))
            .ToList();
        var invalidPercentageInputs = ids
            .Select(x => new ExpenseSplitInput(x, 20))
            .ToList();
        var exactInputs = ids
            .Select(x => new ExpenseSplitInput(x, 200_000))
            .ToList();
        var shareInputs = ids
            .Select((x, index) => new ExpenseSplitInput(x, index + 1))
            .ToList();

        // Act: tính phần tiền theo từng phương pháp chia chi phí nhóm.
        var equal = SharedExpenseCalculator.CalculateShares(
            800_000,
            SplitMethod.Equal,
            equalInputs,
            ids);
        var exact = SharedExpenseCalculator.CalculateShares(
            800_000,
            SplitMethod.ExactAmount,
            exactInputs,
            ids);
        var shares = SharedExpenseCalculator.CalculateShares(
            100,
            SplitMethod.Shares,
            shareInputs,
            ids);

        // Assert: tổng phần chia phải đúng và phần trăm không đủ 100 phải bị từ chối.
        Assert.All(equal.Values, x => Assert.Equal(200_000, x));
        Assert.Throws<AppException>(() => SharedExpenseCalculator.CalculateShares(
            800_000,
            SplitMethod.Percentage,
            invalidPercentageInputs,
            ids));
        Assert.Equal(800_000, exact.Values.Sum());
        Assert.Equal(100, shares.Values.Sum());
    }

 #endif
    [Fact]
    public async Task Group_authorization_blocks_nonmember_and_owner_action_from_member()
    {
        // Arrange: tạo Group gồm Owner, Member và một người ngoài nhóm.
        await using var db = Db();
        var owner = User("Owner");
        var member = User("Member");
        var outsider = User("Out");
        db.Users.AddRange(owner, member, outsider);
        var group = new Group
        {
            Name = "G",
            Type = GroupType.Friends,
            Members =
            [
                new GroupMember { UserId = owner.Id, Role = GroupRole.Owner },
                new GroupMember { UserId = member.Id, Role = GroupRole.Member }
            ]
        };
        var account = Account(0);
        account.Type = LedgerAccountType.SharedFund;
        account.GroupId = group.Id;
        group.SharedFund = new SharedFund
        {
            GroupId = group.Id,
            LedgerAccount = account,
            LedgerAccountId = account.Id
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        var access = new GroupAccess(db);
        var details = new GetGroupDetailsQueryHandler(db, access);
        var update = new UpdateGroupCommandHandler(db, access);

        // Act và Assert: người ngoài không được đọc Group; Member không được thực hiện quyền Owner.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            details.Handle(
                new GetGroupDetailsQuery(outsider.Id, group.Id),
                default));
        await Assert.ThrowsAsync<ForbiddenException>(() => update.Handle(
            new UpdateGroupCommand(
                member.Id,
                group.Id,
                new UpdateGroupRequest("X", null, true)),
            default));
    }

 #if RETIRED_LEGACY_TESTS
    [Fact]
    public async Task Debt_partial_settlement_does_not_create_new_expense()
    {
        // Arrange: tạo khoản nợ 200 đồng giữa hai thành viên và Wallet của con nợ.
        await using var db = Db();
        var (creditor, creditorWallet) = AddUserWallet(db, "Creditor", 100);
        var (debtor, debtorWallet) = AddUserWallet(db, "Debtor", 500);
        var group = new Group
        {
            Name = "G",
            Type = GroupType.Roommates,
            Members =
            [
                new GroupMember { UserId = creditor.Id, Role = GroupRole.Owner },
                new GroupMember { UserId = debtor.Id, Role = GroupRole.Member }
            ]
        };
        var fund = Account(0);
        fund.Type = LedgerAccountType.SharedFund;
        fund.GroupId = group.Id;
        group.SharedFund = new SharedFund
        {
            GroupId = group.Id,
            LedgerAccount = fund,
            LedgerAccountId = fund.Id
        };
        db.Groups.Add(group);
        var expense = ExpenseWithDebtorSplit(group.Id, creditor.Id, debtor.Id, "Bill");
        db.SharedExpenses.Add(expense);
        await db.SaveChangesAsync();
        var debt = DebtFor(expense, group.Id, creditor.Id, debtor.Id);
        db.GroupDebts.Add(debt);
        await db.SaveChangesAsync();

        var handler = DebtSettlement(db);
        // Act: thanh toán một phần 75 đồng cho chủ nợ.
        var result = await handler.Handle(
            new SettleDebtCommand(
                debtor.Id,
                group.Id,
                debt.Id,
                new SettleDebtRequest(debtorWallet.Id, 75, "settle-1")),
            default);

        // Assert: nợ còn 125, số dư chuyển đúng và không tạo thêm Expense.
        Assert.Equal(125, result.RemainingAmount);
        Assert.Equal("PartiallyPaid", result.Status);
        Assert.DoesNotContain(
            db.FinancialTransactions,
            x => x.Type == FinancialTransactionType.Expense);
        Assert.Equal(175, creditorWallet.AvailableBalance);
        Assert.Equal(425, debtorWallet.AvailableBalance);
    }

    [Fact]
    public async Task Same_settlement_key_on_another_debt_does_not_replay_the_first_debt()
    {
        // Arrange: tạo hai khoản nợ riêng giữa cùng hai thành viên.
        await using var db = Db();
        var (creditor, creditorWallet) = AddUserWallet(db, "Scoped Creditor", 100);
        var (debtor, debtorWallet) = AddUserWallet(db, "Scoped Debtor", 500);
        var group = new Group
        {
            Name = "Scoped Debts",
            Type = GroupType.Roommates,
            Members =
            [
                new GroupMember { UserId = creditor.Id, Role = GroupRole.Owner },
                new GroupMember { UserId = debtor.Id, Role = GroupRole.Member }
            ]
        };
        var fund = Account(0);
        fund.Type = LedgerAccountType.SharedFund;
        fund.GroupId = group.Id;
        group.SharedFund = new SharedFund
        {
            GroupId = group.Id,
            LedgerAccount = fund,
            LedgerAccountId = fund.Id
        };
        db.Groups.Add(group);

        var firstExpense = ExpenseWithDebtorSplit(group.Id, creditor.Id, debtor.Id, "First");
        var secondExpense = ExpenseWithDebtorSplit(group.Id, creditor.Id, debtor.Id, "Second");
        db.SharedExpenses.AddRange(firstExpense, secondExpense);
        await db.SaveChangesAsync();

        var firstDebt = DebtFor(firstExpense, group.Id, creditor.Id, debtor.Id);
        var secondDebt = DebtFor(secondExpense, group.Id, creditor.Id, debtor.Id);
        db.GroupDebts.AddRange(firstDebt, secondDebt);
        await db.SaveChangesAsync();
        var handler = DebtSettlement(db);

        // Act: thanh toán hai Debt bằng cùng chuỗi key nhưng scope theo DebtId khác nhau.
        await handler.Handle(
            new SettleDebtCommand(
                debtor.Id,
                group.Id,
                firstDebt.Id,
                new SettleDebtRequest(debtorWallet.Id, 50, "shared-settlement-key")),
            default);
        await handler.Handle(
            new SettleDebtCommand(
                debtor.Id,
                group.Id,
                secondDebt.Id,
                new SettleDebtRequest(debtorWallet.Id, 75, "shared-settlement-key")),
            default);

        // Assert: có hai settlement độc lập, số nợ và số dư đều cập nhật đúng.
        Assert.Equal(50, firstDebt.PaidAmount);
        Assert.Equal(75, secondDebt.PaidAmount);
        Assert.Equal(2, await db.DebtSettlements.CountAsync());
        Assert.Equal(225, creditorWallet.AvailableBalance);
        Assert.Equal(375, debtorWallet.AvailableBalance);
        Assert.DoesNotContain(
            db.FinancialTransactions,
            x => x.Type == FinancialTransactionType.Expense);
    }

 #endif
    [Fact]
    public async Task Reversal_preserves_original_history()
    {
        // Arrange: tạo một posting đã hoàn tất giữa tài khoản nguồn và đích.
        await using var db = Db();
        var source = Account(1_000);
        var destination = Account(0);
        db.AddRange(source, destination);
        await db.SaveChangesAsync();
        var ledger = new LedgerService(db, new SystemClock());
        var original = await ledger.PostAsync(
            source.Id,
            destination.Id,
            200,
            "VND",
            "Transfer",
            Guid.NewGuid(),
            "user:test",
            "orig",
            "original",
            default);
        var originalEntries = await db.LedgerEntries
            .AsNoTracking()
            .Where(item => item.LedgerTransactionId == original.LedgerTransactionId)
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.LedgerAccountId, item.Direction, item.Amount, item.Currency, item.Memo })
            .ToListAsync();

        // Act: tạo bút toán đảo cho posting gốc thay vì sửa các LedgerEntry cũ.
        await ledger.ReverseAsync(
            original.LedgerTransactionId,
            Guid.NewGuid(),
            "reverse",
            "refund",
            default);

        var storedOriginal = await db.LedgerTransactions.FindAsync(original.LedgerTransactionId);
        // Assert: chỉ trạng thái posting gốc đổi sang Reversed; các entry lịch sử vẫn nguyên vẹn.
        db.ChangeTracker.Clear();
        storedOriginal = await db.LedgerTransactions.FindAsync(original.LedgerTransactionId);
        var storedEntries = await db.LedgerEntries
            .AsNoTracking()
            .Where(item => item.LedgerTransactionId == original.LedgerTransactionId)
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.LedgerAccountId, item.Direction, item.Amount, item.Currency, item.Memo })
            .ToListAsync();
        Assert.Equal(2, await db.LedgerTransactions.CountAsync());
        Assert.Equal(LedgerTransactionStatus.Reversed, storedOriginal!.Status);
        Assert.Equal(originalEntries, storedEntries);
        var reversal = Assert.Single(db.LedgerTransactions, item =>
            item.ReversesLedgerTransactionId == original.LedgerTransactionId);
        var reversalEntries = await db.LedgerEntries
            .Where(item => item.LedgerTransactionId == reversal.Id)
            .ToListAsync();
        Assert.Equal(
            reversalEntries.Where(item => item.Direction == LedgerEntryDirection.Debit).Sum(item => item.Amount),
            reversalEntries.Where(item => item.Direction == LedgerEntryDirection.Credit).Sum(item => item.Amount));
        Assert.Equal(1_000, await db.LedgerAccounts.Where(item => item.Id == source.Id).Select(item => item.Balance).SingleAsync());
        Assert.Equal(0, await db.LedgerAccounts.Where(item => item.Id == destination.Id).Select(item => item.Balance).SingleAsync());
    }

 #if RETIRED_LEGACY_TESTS
    [Fact]
    public async Task Group_fund_contribution_is_not_expense()
    {
        // Arrange: tạo thành viên có Wallet và SharedFund của Group.
        await using var db = Db();
        var (user, wallet) = AddUserWallet(db, "Member", 1_000);
        var group = new Group
        {
            Name = "G",
            Type = GroupType.Friends,
            Members = [new GroupMember { UserId = user.Id, Role = GroupRole.Owner }]
        };
        var fundLedger = Account(0);
        fundLedger.Type = LedgerAccountType.SharedFund;
        fundLedger.GroupId = group.Id;
        group.SharedFund = new SharedFund
        {
            GroupId = group.Id,
            LedgerAccount = fundLedger,
            LedgerAccountId = fundLedger.Id
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var clock = new SystemClock();
        var handler = new ContributeSharedFundCommandHandler(
            db,
            new LedgerService(db, clock),
            clock,
            new TestPolicy(),
            new GroupAccess(db));
        // Act: đóng góp 300 đồng từ Wallet vào quỹ chung.
        var result = await handler.Handle(
            new ContributeSharedFundCommand(
                user.Id,
                group.Id,
                new ContributeFundRequest(wallet.Id, 300, "Fund", "fund-1")),
            default);

        var history = await db.FinancialTransactions.SingleAsync();
        // Assert: lịch sử là GroupFundContribution và không tạo Expense.
        Assert.Equal(300, result.Balance);
        Assert.Equal(FinancialTransactionType.GroupFundContribution, history.Type);
        Assert.DoesNotContain(
            db.FinancialTransactions,
            x => x.Type == FinancialTransactionType.Expense);
    }

    [Fact]
    public async Task Shared_expense_retry_does_not_post_or_create_history_twice()
    {
        // Arrange: tạo chi phí chung có IdempotencyKey cố định và Wallet của người trả.
        await using var db = Db();
        var (owner, wallet) = AddUserWallet(db, "Owner", 1_000);
        await db.SaveChangesAsync();
        var clock = new SystemClock();
        var access = new GroupAccess(db);
        var createGroup = new CreateGroupCommandHandler(db, clock, access,
            new FinancialSecurityGuard(db, new SecurityPinService(),
                new AccountCredentialLockout(db, clock)));
        var createExpense = new CreateSharedExpenseCommandHandler(
            db,
            new LedgerService(db, clock),
            clock,
            new TestPolicy(),
            access);
        var group = await createGroup.Handle(
            new CreateGroupCommand(
                owner.Id,
                new CreateGroupRequest("Group", null, "Friends", SecurityPin: FinancialPin)),
            default);
        var request = new CreateSharedExpenseRequest(
            200,
            "Food",
            "Dinner",
            "User",
            owner.Id,
            wallet.Id,
            "Equal",
            [new ExpenseSplitInput(owner.Id, 1)],
            IdempotencyKey: "expense-key");

        // Act: gửi cùng request tạo SharedExpense hai lần.
        var first = await createExpense.Handle(
            new CreateSharedExpenseCommand(owner.Id, group.Id, request),
            default);
        var second = await createExpense.Handle(
            new CreateSharedExpenseCommand(owner.Id, group.Id, request),
            default);

        // Assert: lần hai trả expense cũ; chỉ có một posting, một lịch sử và một lần trừ tiền.
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.SharedExpenses.CountAsync());
        Assert.Equal(1, await db.LedgerTransactions.CountAsync(x => x.BusinessType == "SharedExpense"));
        Assert.Equal(1, await db.FinancialTransactions.CountAsync(x => x.RelatedBusinessId == first.Id));
        Assert.Equal(800, wallet.AvailableBalance);
    }

 #endif
    [Fact]
    public async Task Wallet_queries_never_expose_another_users_wallet()
    {
        // Arrange: tạo hai người dùng, mỗi người có một Wallet với số dư khác nhau.
        await using var db = Db();
        var (user, _) = AddUserWallet(db, "A", 100);
        AddUserWallet(db, "B", 999);
        await db.SaveChangesAsync();

        // Act: truy vấn danh sách Wallet bằng UserId của người thứ nhất.
        var result = await new GetWalletsQueryHandler(db).Handle(
            new GetWalletsQuery(user.Id),
            default);

        // Assert: kết quả chỉ chứa Wallet 100 đồng của đúng người sở hữu.
        Assert.Single(result);
        Assert.Equal(100, result[0].AvailableBalance);
    }

    [Fact]
    public void Admin_contract_has_no_balance_or_ledger_edit_operation()
    {
        // Arrange: đọc tên các phương thức Admin trong Application bằng reflection.
        var methods = typeof(GetAdminDashboardQueryHandler).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "SpendWise.Application.Features.Admin",
                StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods())
            .Select(method => method.Name)
            .ToArray();

        // Act và Assert: xác nhận Admin không có API sửa số dư hoặc ledger trực tiếp.
        Assert.DoesNotContain(
            methods,
            x => x.Contains("EditBalance", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            methods,
            x => x.Contains("EditLedger", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Disabled_feature_flag_blocks_the_operation_policy()
    {
        // Arrange: lưu feature flag P2PTransfer ở trạng thái tắt.
        await using var db = Db();
        db.FeatureFlags.Add(new FeatureFlag { Name = "P2PTransfer", IsEnabled = false });
        await db.SaveChangesAsync();

        // Act và Assert: policy phải trả lỗi feature_disabled với HTTP status 503.
        var error = await Assert.ThrowsAsync<AppException>(() =>
            new OperationPolicy(db).EnsureFeatureEnabledAsync("P2PTransfer", default));

        Assert.Equal("feature_disabled", error.Code);
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public async Task Group_invite_creates_a_persistent_notification()
    {
        // Arrange: tạo Owner, người được mời và Group đang hoạt động.
        await using var db = Db();
        var owner = User("Owner");
        var invited = User("Invited");
        db.Users.AddRange(owner, invited);
        await db.SaveChangesAsync();
        var pair = owner.Id.CompareTo(invited.Id) < 0
            ? (owner.Id, invited.Id)
            : (invited.Id, owner.Id);
        var friendRequest = new FriendRequest
        {
            RequesterUserId = owner.Id,
            AddresseeUserId = invited.Id,
            LowerUserId = pair.Item1,
            HigherUserId = pair.Item2,
            Status = FriendRequestStatus.Accepted,
            ResolvedAt = DateTime.UtcNow
        };
        db.FriendRequests.Add(friendRequest);
        db.Friendships.Add(new Friendship
        {
            LowerUserId = pair.Item1,
            HigherUserId = pair.Item2,
            AcceptedRequest = friendRequest,
            AcceptedRequestId = friendRequest.Id,
            AcceptedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var clock = new SystemClock();
        var access = new GroupAccess(db);
        var group = await new CreateGroupCommandHandler(db, clock, access,
            new FinancialSecurityGuard(db, new SecurityPinService(),
                new AccountCredentialLockout(db, clock))).Handle(
            new CreateGroupCommand(
                owner.Id,
                new CreateGroupRequest("Trip", null, "Friends", SecurityPin: FinancialPin)),
            default);

        var invite = await new InviteGroupMemberCommandHandler(
            db,
            clock,
            Notifications(db),
            access).Handle(
            new InviteGroupMemberCommand(
                owner.Id,
                group.Id,
                new InviteGroupRequest(invited.Id)),
            default);

        var notification = await db.Notifications.SingleAsync(x => x.UserId == invited.Id);
        // Act và Assert: lời mời phải tạo Notification chứa đúng InviteId cho người nhận.
        Assert.Equal("GroupInvite", notification.Type.ToString());
        Assert.Contains(invite.Id.ToString(), notification.DataJson);
    }

    private static SpendWiseDbContext Db()
    {
        // Tạo database in-memory riêng để dữ liệu giữa các test không dùng chung.
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SpendWiseDbContext(options, new SystemClock());
    }

    private static LedgerAccount Account(decimal balance)
    {
        // Tạo LedgerAccount với mã duy nhất và số dư ban đầu cho bài test.
        return new LedgerAccount
        {
            Code = Guid.NewGuid().ToString("N"),
            Name = "Account",
            Type = LedgerAccountType.Wallet,
            Balance = balance
        };
    }

 #if RETIRED_LEGACY_TESTS
    private static SharedExpense ExpenseWithDebtorSplit(
        Guid groupId,
        Guid creditorId,
        Guid debtorId,
        string description) => new()
        {
            // Tạo hóa đơn 400 đồng chia đều giữa chủ nợ và con nợ.
            GroupId = groupId,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Amount = 400,
            Category = "Bill",
            Description = description,
            PaidByType = ExpensePayerType.User,
            PaidByUserId = creditorId,
            SplitMethod = SplitMethod.Equal,
            Date = DateTime.UtcNow,
            Splits =
        [
            new ExpenseSplit { UserId = creditorId, InputValue = 1, ShareAmount = 200 },
            new ExpenseSplit { UserId = debtorId, InputValue = 1, ShareAmount = 200 }
        ]
        };
    private static GroupDebt DebtFor(
        SharedExpense expense,
        Guid groupId,
        Guid creditorId,
        Guid debtorId) => new()
        {
            // Tạo Debt liên kết phần chia của con nợ trong SharedExpense.
            GroupId = groupId,
            SharedExpenseId = expense.Id,
            ExpenseSplitId = expense.Splits.Single(x => x.UserId == debtorId).Id,
            DebtorUserId = debtorId,
            CreditorUserId = creditorId,
            OriginalAmount = 200
        };
 #endif
    private static User User(string name)
    {
        // Tạo User tối thiểu với Email và SpendId duy nhất.
        return new User
        {
            FullName = name,
            Email = $"{Guid.NewGuid():N}@test",
            NormalizedEmail = Guid.NewGuid().ToString("N"),
            SpendId = Guid.NewGuid().ToString("N"),
            NormalizedSpendId = Guid.NewGuid().ToString("N"),
            PasswordHash = "test",
            SecurityPinHash = FinancialPinHash
        };
    }

    private static (User, Wallet) AddUserWallet(
        SpendWiseDbContext db,
        string name,
        decimal balance)
    {
        // Thêm User cùng Wallet/Ledger có số dư ban đầu vào DbContext.
        var user = User(name);
        var wallet = WalletFactory.Create(
            user.Id,
            "Main",
            WalletType.Main,
            true);
        wallet.AvailableBalance = balance;
        wallet.LedgerAccount.Balance = balance;
        db.Users.Add(user);
        db.Wallets.Add(wallet);
        return (user, wallet);
    }

 #if RETIRED_LEGACY_TESTS
    private static TransferExecutor Transfer(SpendWiseDbContext db)
    {
        // Tạo TransferExecutor với Ledger, Risk, Notification và policy dùng trong test.
        var clock = new SystemClock();
        var policy = new TestPolicy();
        return new TransferExecutor(
            db,
            new LedgerService(db, clock),
            clock,
            Notifications(db),
            new RiskService(db, policy, clock),
            policy);
    }

 #endif
    private static GoalMovementExecutor GoalMovement(SpendWiseDbContext db, IClock clock) => new(
        db,
        new LedgerService(db, clock),
        clock,
        new FinancialSecurityGuard(db, new SecurityPinService(),
            new AccountCredentialLockout(db, clock)));

 #if RETIRED_LEGACY_TESTS
    private static SettleDebtCommandHandler DebtSettlement(SpendWiseDbContext db)
    {
        // Tạo Handler quyết toán nợ bằng cùng DbContext và LedgerService.
        var clock = new SystemClock();
        return new SettleDebtCommandHandler(
            db,
            new LedgerService(db, clock),
            clock,
            new GroupAccess(db));
    }

 #endif
    private static NotificationService Notifications(SpendWiseDbContext db)
    {
        // NotificationService chỉ lưu Notification và Outbox; worker realtime chạy độc lập.
        return new NotificationService(db);
    }

    private sealed class NullRealtime : IRealtimeNotifier
    {
        public Task SendToUserAsync(
            Guid userId,
            string eventName,
            object payload,
            CancellationToken cancellationToken)
        {
            // Bộ gửi realtime giả không thực hiện I/O bên ngoài trong test.
            return Task.CompletedTask;
        }
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public string SystemRole => "User";
        public string Locale => "vi-VN";
        public string? SessionId => Guid.NewGuid().ToString();
    }
    private sealed class TestPolicy : IOperationPolicy
    {
        public Task EnsureFeatureEnabledAsync(string featureName, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken cancellationToken) =>
            Task.FromResult(key == "TransferMinimum" ? 0m : fallback);
    }
}
