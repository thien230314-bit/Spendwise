using SpendWise.Application.Identity;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Transfers.Commands.P2PTransfer;
using SpendWise.Application.Features.Transfers.Commands.TransferBetweenWallets;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Finance;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Admin;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using Xunit;

namespace SpendWise.Tests;

public sealed class Task16_1_ApprovedDecisionsTests
{
    private const string Pin = TargetedCorrectnessFixture.SecurityPin;

    private static V1FundService Service(TargetedCorrectnessFixture f) => V1ElectionTests.Configured(f);

    private static AdminA5Service CreateA5(TargetedCorrectnessFixture f) =>
        new(
            f.Db,
            new(f.Db),
            new(f.Db, new(f.Db), new(f.Db), new NotificationService(f.Db), f.Clock),
            new(f.Db),
            new NotificationService(f.Db),
            new(new EmailTemplateRenderer(), new NullEmailDeliveryQueue(), new TestEmailVerificationPolicy()),
            new EmailTemplateRenderer(),
            new CaseAttachmentValidator(),
            f.Clock);

    private sealed class NullEmailDeliveryQueue : IEmailDeliveryQueue
    {
        public Task<bool> EnqueueAsync(EmailMessage message, string deduplicationKey, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class TestEmailVerificationPolicy : IEmailVerificationPolicy
    {
        public TimeSpan OtpLifetime => TimeSpan.FromMinutes(15);
        public TimeSpan ResendCooldown => TimeSpan.FromMinutes(1);
        public int MaximumFailedAttempts => 5;
        public int MaximumSendsPerHourPerAccount => 10;
        public int MaximumSendsPerHourPerIp => 30;
        public string WebsiteUrl => "https://spendwise.local";
    }

    [Fact]
    public async Task Decision1_DisputeResolution_Valid_CancelsExpense_And_ReleasesHold()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("Admin", role: SystemRole.Admin);
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var fundService = Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new("FixedTrip", "Fixed", 1_000_000, Pin), default);

        var invite = await fundService.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await fundService.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 2_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 2_000_000);
        var config = await fundService.ConfirmObligationsAsync(owner.Id, fund.Id, new(1_000_000, Pin), default);

        // Both contribute to satisfy obligations
        await fundService.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 500_000, "owner-c1", Pin, config.Revision), default);
        await fundService.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 500_000, "member-c1", Pin, config.Revision), default);

        // Enable spending permission for owner
        await fundService.SetSpendingPermissionAsync(owner.Id, fund.Id, owner.Id, true, Pin, default);

        // Owner creates large expense request (> 200,000 VND triggers Pending24h)
        var expense = await fundService.CreateFundRequestAsync(owner.Id, fund.Id,
            new("Expense", 300_000, owner.Id, "Big expense for groceries", "exp-1", Pin), default);
        Assert.Equal("Pending24h", expense.State);

        // Member reports the expense hold -> State becomes Suspended and SupportCase is created
        var reported = await fundService.ReportFundRequestAsync(member.Id, expense.Id, "Suspicious expense", default);
        Assert.Equal("Suspended", reported.State);

        var supportCase = await f.Db.SupportCases.SingleAsync(c => c.Category == "V1FundDispute" && c.LinkedTransactionId == expense.Id);
        Assert.NotNull(supportCase);
        supportCase.AssignedAdminId = admin.Id;
        supportCase.Status = CaseStatus.Investigating;
        await f.Db.SaveChangesAsync();

        // Admin resolves dispute as VALID (IsRejected = false)
        var adminA5 = CreateA5(f);
        var adminPrincipal = V1PermissionTests.Principal(admin.Id, (await f.AddSessionAsync(admin.Id)).Id, "Admin");
        var resolveDto = await adminA5.ResolveDisputeRequestAsync(adminPrincipal, expense.Id,
            new A5ResolveRequest("Dispute justified", "Expense is illegitimate", false), default);

        Assert.Equal("Resolved", resolveDto.NewStatus);

        var updatedExpense = await f.Db.V1FundRequests.FindAsync(expense.Id);
        Assert.Equal("Cancelled", updatedExpense!.State);
    }

    [Fact]
    public async Task Decision1_DisputeResolution_Invalid_UnsuspendsExpense_BackTo_Pending24h()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("Admin", role: SystemRole.Admin);
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var fundService = Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new("FixedTrip", "Fixed", 1_000_000, Pin), default);

        var invite = await fundService.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await fundService.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 2_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 2_000_000);
        var config = await fundService.ConfirmObligationsAsync(owner.Id, fund.Id, new(1_000_000, Pin), default);

        // Both contribute to satisfy obligations
        await fundService.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 500_000, "owner-c1", Pin, config.Revision), default);
        await fundService.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 500_000, "member-c1", Pin, config.Revision), default);

        await fundService.SetSpendingPermissionAsync(owner.Id, fund.Id, owner.Id, true, Pin, default);

        var expense = await fundService.CreateFundRequestAsync(owner.Id, fund.Id,
            new("Expense", 300_000, owner.Id, "Big expense for groceries", "exp-1", Pin), default);
        await fundService.ReportFundRequestAsync(member.Id, expense.Id, "False alarm report", default);

        var supportCase = await f.Db.SupportCases.SingleAsync(c => c.Category == "V1FundDispute" && c.LinkedTransactionId == expense.Id);
        supportCase.AssignedAdminId = admin.Id;
        supportCase.Status = CaseStatus.Investigating;
        await f.Db.SaveChangesAsync();

        // Admin resolves dispute as INVALID (IsRejected = true)
        var adminA5 = CreateA5(f);
        var adminPrincipal = V1PermissionTests.Principal(admin.Id, (await f.AddSessionAsync(admin.Id)).Id, "Admin");
        var resolveDto = await adminA5.ResolveDisputeRequestAsync(adminPrincipal, expense.Id,
            new A5ResolveRequest("Expense verified as legitimate", "Report is rejected", true), default);

        Assert.Equal("Rejected", resolveDto.NewStatus);

        var updatedExpense = await f.Db.V1FundRequests.FindAsync(expense.Id);
        Assert.Equal("Pending24h", updatedExpense!.State);
    }

    [Fact]
    public async Task Decision2_Level1_RestrictedUser_CanLogin_And_Read_ButCannotDebit()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var restrictedUser = await f.AddUserAsync("RestrictedUser", UserStatus.Restricted);
        var session = await f.AddSessionAsync(restrictedUser.Id);

        // 1. Session is valid for reading balances/profile
        var validator = new AccessSessionValidator(f.Db);
        Assert.True(await validator.IsValidAsync(restrictedUser.Id, session.Id, default));

        // 2. Outgoing debit: P2P transfer is blocked
        var otherUser = await f.AddUserAsync("OtherUser");
        var wallet1 = await f.AddWalletAsync(restrictedUser.Id, "W1", true, balance: 500_000);
        var wallet2 = await f.AddWalletAsync(otherUser.Id, "W2", true, balance: 50_000);

        var p2pHandler = new P2PTransferCommandHandler(
            f.Db,
            new LedgerService(f.Db, f.Clock),
            f.Clock,
            new OperationPolicy(f.Db),
            new FinancialSecurityGuard(f.Db, new SecurityPinService(), new AccountCredentialLockout(f.Db, f.Clock)));

        var exP2p = await Assert.ThrowsAsync<AppException>(() => p2pHandler.Handle(new P2PTransferCommand(
            restrictedUser.Id, new P2PTransferRequest(wallet1.Id, otherUser.Id, 100_000, "Test", "p2p-key-1", Pin)), default));
        Assert.Equal("account_restricted", exP2p.Code);

        // 3. Outgoing debit: Transfer between wallets is blocked
        var wallet3 = await f.AddWalletAsync(restrictedUser.Id, "W3", false, balance: 10_000);
        var tbwHandler = new TransferBetweenWalletsCommandHandler(
            f.Db,
            new LedgerService(f.Db, f.Clock),
            f.Clock,
            new OperationPolicy(f.Db),
            new FinancialSecurityGuard(f.Db, new SecurityPinService(), new AccountCredentialLockout(f.Db, f.Clock)));

        var exTbw = await Assert.ThrowsAsync<AppException>(() => tbwHandler.Handle(new TransferBetweenWalletsCommand(
            restrictedUser.Id, new InternalTransferRequest(wallet1.Id, wallet3.Id, 50_000, "Test", "tbw-key-1", Pin)), default));
        Assert.Equal("account_restricted", exTbw.Code);
    }

    [Fact]
    public async Task Decision2_Level2_LockedUser_CannotLogin_AndSessionIsRevoked()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var lockedUser = await f.AddUserAsync("LockedUser", UserStatus.Locked);
        var session = await f.AddSessionAsync(lockedUser.Id);

        // Session validator denies
        var validator = new AccessSessionValidator(f.Db);
        Assert.False(await validator.IsValidAsync(lockedUser.Id, session.Id, default));
    }

    [Fact]
    public async Task Decision3_FreeFund_RejoiningMember_WithoutContribution_GetsZeroRefund()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var fundService = Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new("FreeTrip", "Free", null, Pin), default);

        // Member joins, contributes 300_000 VND
        var invite = await fundService.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await fundService.AcceptAsync(member.Id, invite.Id, default);

        var memberWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1_000_000);
        await fundService.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 300_000, "m-c1", Pin, null), default);

        // Member leaves and gets 300_000 VND refund
        var leaveResult = await fundService.LeaveFundAsync(member.Id, fund.Id, new(Pin, "leave-1"), default);
        Assert.Equal(300_000, leaveResult.RefundAmountVnd);

        // Member rejoins (new membership episode)
        var invite2 = await fundService.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await fundService.AcceptAsync(member.Id, invite2.Id, default);

        // Preview refund before making any new contribution -> Exactly 0 VND
        var preview = await fundService.PreviewRefundAsync(member.Id, fund.Id, member.Id, default);
        Assert.Equal(0, preview.EligibleRefundAmountVnd);

        // Member leaves again -> Exactly 0 VND refund
        var leaveResult2 = await fundService.LeaveFundAsync(member.Id, fund.Id, new(Pin, "leave-2"), default);
        Assert.Equal(0, leaveResult2.RefundAmountVnd);
    }

    [Fact]
    public async Task Decision4_ElectionGovernance_TimelineDefaults_And_WinnerRejection()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        // Verify policy snapshot defaults are fully approved without throwing POLICY_BLOCKED
        var policy = new V1ElectionPolicy();
        var snapshot = policy.Snapshot();
        Assert.Equal("V1.0", snapshot.Version);
        Assert.Equal(86400, snapshot.RegistrationSeconds);
        Assert.Equal(172800, snapshot.NominationSeconds);
        Assert.Equal(86400, snapshot.RunoffSeconds);
        Assert.Equal(86400, snapshot.ConfirmationSeconds);
        Assert.Equal(86400, snapshot.AcceptanceSeconds);
        Assert.False(snapshot.AllowContributions);

        // Open an election using the built-in flow
        var fundService = Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new("Free", "Free", null, Pin), default);
        f.Db.Add(new V1FundMembership { FundId = fund.Id, UserId = member.Id, Seat = 2 });
        await f.Db.SaveChangesAsync();

        var openedElection = await fundService.OpenElectionAsync(member.Id, fund.Id, new(0), default);

        // Transition election to AwaitingAcceptance with member as winner
        var electionEntity = await f.Db.V1Elections.SingleAsync(e => e.Id == openedElection.Id);
        electionEntity.Phase = V1ElectionPhase.AwaitingAcceptance;
        electionEntity.WinnerUserId = member.Id;
        await f.Db.SaveChangesAsync();

        // Verify RejectElectedOwnershipAsync transitions election to Failed with WinnerRejected
        var rejectedElection = await fundService.RejectElectedOwnershipAsync(member.Id, openedElection.Id, Pin, default);
        Assert.Equal("Failed", rejectedElection.Phase);
        Assert.Equal("WinnerRejected", rejectedElection.FailureReason);
        Assert.NotNull(rejectedElection.FinishedAt);
    }

    [Fact]
    public async Task Decision4_FinanceOperations_ArePaused_DuringActiveElection()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var fundService = Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, Pin), default);
        var invite = await fundService.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await fundService.AcceptAsync(member.Id, invite.Id, default);

        var memberWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1_000_000);
        var config = await fundService.ConfirmObligationsAsync(owner.Id, fund.Id, new(100, Pin), default);

        // Open an active election using standard flow
        await fundService.OpenElectionAsync(member.Id, fund.Id, new(0), default);

        // Contribution is paused
        var exContrib = await Assert.ThrowsAsync<ConflictException>(() =>
            fundService.ContributeAsync(member.Id, fund.Id,
                new(memberWallet.Id, 100, "c-fail", Pin, config.Revision), default));
        Assert.Equal("election_contributions_paused", exContrib.Code);

        // Fund request is paused
        var exReq = await Assert.ThrowsAsync<ConflictException>(() =>
            fundService.CreateFundRequestAsync(owner.Id, fund.Id,
                new("Expense", 50, owner.Id, "Exp", "req-fail", Pin), default));
        Assert.Equal("election_finance_paused", exReq.Code);
    }
}
