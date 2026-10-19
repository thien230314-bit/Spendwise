using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Groups.Commands.CloseGroup;
using SpendWise.Application.Features.Groups.Commands.CreateGroup;
using SpendWise.Application.Features.Groups.Commands.TransferGroupOwnership;
using SpendWise.Application.Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Infrastructure.Admin;
using SpendWise.Infrastructure.Finance;
using Xunit;

namespace SpendWise.Tests;

// Các test này khóa những lỗ hổng High được tìm thấy trong giai đoạn audit Handler.
public sealed class StageOneAuditRegressionTests
{
 #if RETIRED_LEGACY_TESTS
    [Fact]
    public async Task Cancelled_debt_cannot_create_a_new_financial_settlement()
    {
        // Arrange: khoản nợ đã Cancelled vẫn còn RemainingAmount nhưng không còn được thanh toán.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var creditor = await fixture.AddUserAsync("Audit creditor");
        var debtor = await fixture.AddUserAsync("Audit debtor");
        var creditorWallet = await fixture.AddWalletAsync(creditor.Id, "Creditor", true);
        var debtorWallet = await fixture.AddWalletAsync(debtor.Id, "Debtor", true, balance: 500);
        var group = await CreateGroupAsync(fixture, creditor.Id, "Cancelled debt group");
        await AddMemberAsync(fixture, group.Id, debtor.Id);
        var debt = new GroupDebt
        {
            GroupId = group.Id,
            SharedExpenseId = Guid.NewGuid(),
            ExpenseSplitId = Guid.NewGuid(),
            DebtorUserId = debtor.Id,
            CreditorUserId = creditor.Id,
            OriginalAmount = 100,
            Status = DebtStatus.Cancelled
        };
        fixture.Db.GroupDebts.Add(debt);
        await fixture.Db.SaveChangesAsync();
        var handler = new SettleDebtCommandHandler(
            fixture.Db,
            new LedgerService(fixture.Db, fixture.Clock),
            fixture.Clock,
            new GroupAccess(fixture.Db));

        // Act: thử tạo một settlement mới cho khoản nợ đã kết thúc vòng đời.
        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new SettleDebtCommand(
                debtor.Id,
                group.Id,
                debt.Id,
                new SettleDebtRequest(debtorWallet.Id, 50, "cancelled-debt")),
            default));

        // Assert: không có posting và số dư hai bên giữ nguyên.
        Assert.Equal("debt_unavailable", error.Code);
        Assert.Empty(fixture.Db.LedgerTransactions);
        Assert.Equal(500, debtorWallet.AvailableBalance);
        Assert.Equal(0, creditorWallet.AvailableBalance);
    }

    [Fact]
    public async Task Contribution_recipient_from_another_group_cannot_be_marked_paid()
    {
        // Arrange: cùng thành viên thuộc hai nhóm nhưng recipient chỉ thuộc nhóm thứ hai.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Audit owner");
        var member = await fixture.AddUserAsync("Audit member");
        var wallet = await fixture.AddWalletAsync(member.Id, "Member wallet", true, balance: 500);
        var firstGroup = await CreateGroupAsync(fixture, owner.Id, "First group");
        var secondGroup = await CreateGroupAsync(fixture, owner.Id, "Second group");
        await AddMemberAsync(fixture, firstGroup.Id, member.Id);
        await AddMemberAsync(fixture, secondGroup.Id, member.Id);
        var recipient = await AddContributionRecipientAsync(
            fixture,
            secondGroup.Id,
            member.Id,
            ContributionRequestStatus.Pending);

        // Act: cố dùng recipient của nhóm khác khi đóng góp vào nhóm thứ nhất.
        var error = await Assert.ThrowsAsync<NotFoundException>(() => SharedFundHandler(fixture).Handle(
            new ContributeSharedFundCommand(
                member.Id,
                firstGroup.Id,
                new ContributeFundRequest(wallet.Id, 100, "Wrong group", "wrong-group", recipient.Id)),
            default));

        // Assert: request không bị đánh dấu Paid và không có tiền dịch chuyển.
        Assert.Equal("contribution_request_not_found", error.Code);
        Assert.Equal(ContributionRequestStatus.Pending, recipient.Status);
        Assert.Empty(fixture.Db.LedgerTransactions);
        Assert.Equal(500, wallet.AvailableBalance);
    }

    [Fact]
    public async Task Paid_contribution_recipient_cannot_charge_wallet_again()
    {
        // Arrange: recipient của đúng nhóm đã hoàn tất từ một contribution trước đó.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Paid request owner");
        var member = await fixture.AddUserAsync("Paid request member");
        var wallet = await fixture.AddWalletAsync(member.Id, "Member wallet", true, balance: 500);
        var group = await CreateGroupAsync(fixture, owner.Id, "Paid request group");
        await AddMemberAsync(fixture, group.Id, member.Id);
        var recipient = await AddContributionRecipientAsync(
            fixture,
            group.Id,
            member.Id,
            ContributionRequestStatus.Paid);

        // Act: dùng key mới để thử thanh toán lại cùng recipient.
        var error = await Assert.ThrowsAsync<ConflictException>(() => SharedFundHandler(fixture).Handle(
            new ContributeSharedFundCommand(
                member.Id,
                group.Id,
                new ContributeFundRequest(wallet.Id, 100, "Duplicate", "new-key", recipient.Id)),
            default));

        // Assert: state machine chặn trước khi ledger hoặc số dư thay đổi.
        Assert.Equal("contribution_request_unavailable", error.Code);
        Assert.Empty(fixture.Db.LedgerTransactions);
        Assert.Equal(500, wallet.AvailableBalance);
    }

 #endif
    [Fact]
    public async Task Group_with_shared_fund_balance_cannot_be_closed()
    {
        // Arrange: quỹ nhóm và tài khoản ledger cùng còn 200 đồng.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Close group owner");
        var groupDto = await CreateGroupAsync(fixture, owner.Id, "Funded group");
        var group = await fixture.Db.Groups
            .Include(item => item.SharedFund)
            .ThenInclude(fund => fund.LedgerAccount)
            .SingleAsync(item => item.Id == groupDto.Id);
        group.SharedFund.Balance = 200;
        group.SharedFund.LedgerAccount.Balance = 200;
        await fixture.Db.SaveChangesAsync();

        // Act: đóng nhóm khi tiền vẫn đang được phân bổ trong quỹ.
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            new CloseGroupCommandHandler(fixture.Db, new GroupAccess(fixture.Db), fixture.Clock)
                .Handle(new CloseGroupCommand(owner.Id, group.Id), default));

        // Assert: nhóm vẫn Active để thành viên còn đường xử lý số tiền đó.
        Assert.Equal("group_fund_not_empty", error.Code);
        Assert.Equal(GroupStatus.Active, group.Status);
    }

 #if RETIRED_LEGACY_TESTS
    [Fact]
    public async Task Shared_expense_with_null_description_can_be_replayed_safely()
    {
        // Arrange: JSON có thể gửi null dù contract C# khai báo chuỗi không nullable.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Null description owner");
        var wallet = await fixture.AddWalletAsync(owner.Id, "Expense wallet", true, balance: 500);
        var group = await CreateGroupAsync(fixture, owner.Id, "Replay group");
        var handler = new CreateSharedExpenseCommandHandler(
            fixture.Db,
            new LedgerService(fixture.Db, fixture.Clock),
            fixture.Clock,
            new OperationPolicy(fixture.Db),
            new GroupAccess(fixture.Db));
        var request = new CreateSharedExpenseRequest(
            100,
            "Food",
            null!,
            "User",
            owner.Id,
            wallet.Id,
            "Equal",
            [new ExpenseSplitInput(owner.Id, 1)],
            IdempotencyKey: "null-description");

        // Act: gửi lại đúng payload sau lần posting đầu tiên.
        var first = await handler.Handle(
            new CreateSharedExpenseCommand(owner.Id, group.Id, request),
            default);
        var replay = await handler.Handle(
            new CreateSharedExpenseCommand(owner.Id, group.Id, request),
            default);

        // Assert: replay trả bản ghi cũ thay vì NullReference hoặc posting lần hai.
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, await fixture.Db.SharedExpenses.CountAsync());
        Assert.Equal(1, await fixture.Db.LedgerTransactions.CountAsync());
    }

 #endif
    [Fact]
    public async Task Ownership_transfer_keeps_exactly_one_owner()
    {
        // Arrange: nhóm có một Owner hiện tại và một Member hợp lệ.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Old owner");
        var member = await fixture.AddUserAsync("New owner");
        var group = await CreateGroupAsync(fixture, owner.Id, "Ownership group");
        await AddMemberAsync(fixture, group.Id, member.Id);

        // Act: chuyển quyền bằng hai UPDATE có điều kiện trong một transaction.
        await new TransferGroupOwnershipCommandHandler(fixture.Db, new GroupAccess(fixture.Db))
            .Handle(new TransferGroupOwnershipCommand(owner.Id, group.Id, member.Id), default);

        // Assert: chỉ người nhận có vai trò Owner sau khi transaction hoàn tất.
        fixture.Db.ChangeTracker.Clear();
        var memberships = await fixture.Db.GroupMembers
            .Where(item => item.GroupId == group.Id)
            .ToListAsync();
        Assert.Single(memberships, item => item.Role == GroupRole.Owner);
        Assert.Equal(GroupRole.Owner, memberships.Single(item => item.UserId == member.Id).Role);
    }

    private static async Task<GroupDto> CreateGroupAsync(
        TargetedCorrectnessFixture fixture,
        Guid ownerId,
        string name) => await new CreateGroupCommandHandler(
            fixture.Db,
            fixture.Clock,
            new GroupAccess(fixture.Db),
            fixture.Security)
            .Handle(new CreateGroupCommand(ownerId, new CreateGroupRequest(name, null, "Friends", SecurityPin: TargetedCorrectnessFixture.SecurityPin)), default);

    private static async Task AddMemberAsync(
        TargetedCorrectnessFixture fixture,
        Guid groupId,
        Guid userId)
    {
        fixture.Db.GroupMembers.Add(new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            Role = GroupRole.Member,
            JoinedAt = fixture.Clock.UtcNow
        });
        await fixture.Db.SaveChangesAsync();
    }

 #if RETIRED_LEGACY_TESTS
    private static async Task<GroupContributionRecipient> AddContributionRecipientAsync(
        TargetedCorrectnessFixture fixture,
        Guid groupId,
        Guid userId,
        ContributionRequestStatus status)
    {
        var request = new GroupContributionRequest
        {
            GroupId = groupId,
            Purpose = "Audit contribution",
            AmountPerMember = 100,
            Recipients =
            [
                new GroupContributionRecipient
                {
                    UserId = userId,
                    Status = status,
                    ContributionId = status == ContributionRequestStatus.Paid
                        ? Guid.NewGuid()
                        : null
                }
            ]
        };
        fixture.Db.GroupContributionRequests.Add(request);
        await fixture.Db.SaveChangesAsync();
        return request.Recipients[0];
    }

    private static ContributeSharedFundCommandHandler SharedFundHandler(
        TargetedCorrectnessFixture fixture) => new(
            fixture.Db,
            new LedgerService(fixture.Db, fixture.Clock),
            fixture.Clock,
            new OperationPolicy(fixture.Db),
            new GroupAccess(fixture.Db));
 #endif
}
