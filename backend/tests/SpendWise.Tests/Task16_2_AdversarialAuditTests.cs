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
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Finance;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Admin;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using Xunit;

namespace SpendWise.Tests;

public sealed class Task16_2_AdversarialAuditTests
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
    public async Task CaseR1_FreeFund_Dissolution_ZeroEntitlement_MustNotDivideEqually_ThrowsConflict()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var memberA = await f.AddUserAsync("MemberA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var fundService = Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new("FreeTrip", "Free", null, Pin), default);

        var inviteA = await fundService.InviteAsync(owner.Id, fund.Id, memberA.Id, default);
        await fundService.AcceptAsync(memberA.Id, inviteA.Id, default);

        var walletA = await f.AddWalletAsync(memberA.Id, "WalletA", true, balance: 1_000_000);
        await fundService.ContributeAsync(memberA.Id, fund.Id, new(walletA.Id, 500_000, "c-A", Pin, null), default);

        // Member A leaves without receiving ledger refund (e.g. membership marked LeftAt)
        var memA = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == memberA.Id);
        memA.LeftAt = DateTime.UtcNow.AddDays(-1);
        await f.Db.SaveChangesAsync();

        // Member B joins (new member, 0 contribution)
        var inviteB = await fundService.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await fundService.AcceptAsync(memberB.Id, inviteB.Id, default);

        // Active members are Owner (paid 0) and Member B (paid 0). Total net equity of active members is 0!
        // But treasury balance has 500_000 VND from Member A.
        // Dissolution must NOT divide money to zero-entitlement members, must throw ConflictException!
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            fundService.DissolveFundAsync(owner.Id, fund.Id, new(Pin, "dissolve-zero-eq"), default));
        Assert.Equal("zero_entitlement_dissolution_blocked", ex.Code);
    }

    [Fact]
    public async Task CaseR2_FixedFund_Dissolution_ZeroEntitlement_MustNotDivideEqually_ThrowsConflict()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var memberA = await f.AddUserAsync("MemberA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var fundService = Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new("FixedTrip", "Fixed", 500_000, Pin), default);

        var inviteA = await fundService.InviteAsync(owner.Id, fund.Id, memberA.Id, default);
        await fundService.AcceptAsync(memberA.Id, inviteA.Id, default);

        var walletA = await f.AddWalletAsync(memberA.Id, "WalletA", true, balance: 1_000_000);
        var cfg = await fundService.ConfirmObligationsAsync(owner.Id, fund.Id, new(500_000, Pin), default);
        var obA = await f.Db.V1FundObligations.SingleAsync(o => o.FundId == fund.Id && o.MembershipId == (f.Db.V1FundMemberships.Single(m => m.UserId == memberA.Id).Id));
        await fundService.ContributeAsync(memberA.Id, fund.Id, new(walletA.Id, obA.RequiredVnd, "c-A", Pin, cfg.Revision), default);

        // Member A membership ended
        var memA = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == memberA.Id);
        memA.LeftAt = DateTime.UtcNow.AddDays(-1);
        await f.Db.SaveChangesAsync();

        // Member B joins (new member, 0 contribution)
        var inviteB = await fundService.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await fundService.AcceptAsync(memberB.Id, inviteB.Id, default);

        // Active members are Owner (paid 0) and Member B (paid 0). sumWeights = 0!
        // Dissolution must NOT divide equally to members who contributed 0!
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            fundService.DissolveFundAsync(owner.Id, fund.Id, new(Pin, "dissolve-zero-weights"), default));
        Assert.Equal("zero_entitlement_dissolution_blocked", ex.Code);
    }

    [Fact]
    public async Task CaseR3_R5_MultipleRejoinEpisodes_StrictlyIsolateEntitlements_And_NoRefundLeakage()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var fundService = Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new("FreeClub", "Free", null, Pin), default);
        var wallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 2_000_000);

        // Episode 1: Join, contribute 400_000, leave and refund 400_000
        var inv1 = await fundService.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await fundService.AcceptAsync(member.Id, inv1.Id, default);
        await fundService.ContributeAsync(member.Id, fund.Id, new(wallet.Id, 400_000, "c-ep1", Pin, null), default);
        var leave1 = await fundService.LeaveFundAsync(member.Id, fund.Id, new(Pin, "leave-ep1"), default);
        Assert.Equal(400_000, leave1.RefundAmountVnd);

        // Episode 2: Rejoin, contribute 0, preview refund is 0, leave is 0
        var inv2 = await fundService.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await fundService.AcceptAsync(member.Id, inv2.Id, default);
        var prev2 = await fundService.PreviewRefundAsync(member.Id, fund.Id, member.Id, default);
        Assert.Equal(0, prev2.EligibleRefundAmountVnd);
        var leave2 = await fundService.LeaveFundAsync(member.Id, fund.Id, new(Pin, "leave-ep2"), default);
        Assert.Equal(0, leave2.RefundAmountVnd);

        // Episode 3: Rejoin, contribute 250_000, preview is 250_000, leave is 250_000
        var inv3 = await fundService.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await fundService.AcceptAsync(member.Id, inv3.Id, default);
        await fundService.ContributeAsync(member.Id, fund.Id, new(wallet.Id, 250_000, "c-ep3", Pin, null), default);
        var prev3 = await fundService.PreviewRefundAsync(member.Id, fund.Id, member.Id, default);
        Assert.Equal(250_000, prev3.EligibleRefundAmountVnd);
        var leave3 = await fundService.LeaveFundAsync(member.Id, fund.Id, new(Pin, "leave-ep3"), default);
        Assert.Equal(250_000, leave3.RefundAmountVnd);
    }

    [Fact]
    public async Task CaseR7_FreeFund_UnequalContributions_ZeroEntitlementMemberGetsZero()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var memberA = await f.AddUserAsync("MemberA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var fundService = Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new("FreeUnequal", "Free", null, Pin), default);

        var invA = await fundService.InviteAsync(owner.Id, fund.Id, memberA.Id, default);
        await fundService.AcceptAsync(memberA.Id, invA.Id, default);
        var invB = await fundService.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await fundService.AcceptAsync(memberB.Id, invB.Id, default);

        var walletA = await f.AddWalletAsync(memberA.Id, "WA", true, balance: 1_000_000);
        // Only member A contributes
        await fundService.ContributeAsync(memberA.Id, fund.Id, new(walletA.Id, 600_000, "c-A", Pin, null), default);

        // Member B preview refund is exactly 0 VND
        var prevB = await fundService.PreviewRefundAsync(memberB.Id, fund.Id, memberB.Id, default);
        Assert.Equal(0, prevB.EligibleRefundAmountVnd);
        Assert.Equal("NoContribution", prevB.CaseType);

        // Member A preview refund is 600_000 VND
        var prevA = await fundService.PreviewRefundAsync(memberA.Id, fund.Id, memberA.Id, default);
        Assert.Equal(600_000, prevA.EligibleRefundAmountVnd);

        // Member B leaves -> gets 0 VND
        var leaveB = await fundService.LeaveFundAsync(memberB.Id, fund.Id, new(Pin, "leave-B"), default);
        Assert.Equal(0, leaveB.RefundAmountVnd);
    }

    [Fact]
    public async Task CaseD4_D5_DisputeResolution_NonSuspendedRequest_ThrowsConflict()
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

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 2_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 2_000_000);
        var config = await fundService.ConfirmObligationsAsync(owner.Id, fund.Id, new(1_000_000, Pin), default);

        await fundService.ContributeAsync(owner.Id, fund.Id, new(ownerWallet.Id, 500_000, "o-c", Pin, config.Revision), default);
        await fundService.ContributeAsync(member.Id, fund.Id, new(memberWallet.Id, 500_000, "m-c", Pin, config.Revision), default);
        await fundService.SetSpendingPermissionAsync(owner.Id, fund.Id, owner.Id, true, Pin, default);

        var expense = await fundService.CreateFundRequestAsync(owner.Id, fund.Id,
            new("Expense", 300_000, owner.Id, "Trip supplies", "exp-1", Pin), default);

        // Member reports -> Suspended
        await fundService.ReportFundRequestAsync(member.Id, expense.Id, "Suspicious", default);

        // Assign support case and create real operator session
        var ticket = await f.Db.SupportCases.SingleAsync(c => c.Category == "V1FundDispute" && c.LinkedTransactionId == expense.Id);
        ticket.AssignedAdminId = admin.Id;
        ticket.Status = CaseStatus.Investigating;

        var session = new UserSession { UserId = admin.Id, LastSeenAt = DateTime.UtcNow };
        f.Db.UserSessions.Add(session);
        await f.Db.SaveChangesAsync();

        // Simulate request being Cancelled (e.g. by owner or concurrent action)
        var reqEntity = await f.Db.V1FundRequests.SingleAsync(r => r.Id == expense.Id);
        reqEntity.State = "Cancelled";
        await f.Db.SaveChangesAsync();

        var adminService = CreateA5(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        // Admin attempting to resolve a non-suspended request MUST throw ConflictException
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            adminService.ResolveDisputeRequestAsync(principal, expense.Id,
                new("Investigation confirms report", "Valid report from member", false), default));
        Assert.Equal("disputed_request_not_suspended", ex.Code);
    }

    [Fact]
    public async Task CaseD8_DisputeResolution_NonAdmin_ThrowsForbidden()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("Admin", role: SystemRole.Admin);
        var nonAdmin = await f.AddUserAsync("RegularUser", role: SystemRole.User);
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var fundService = Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new("FixedDispute", "Fixed", 1_000_000, Pin), default);
        var invite = await fundService.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await fundService.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 2_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 2_000_000);
        var config = await fundService.ConfirmObligationsAsync(owner.Id, fund.Id, new(1_000_000, Pin), default);

        await fundService.ContributeAsync(owner.Id, fund.Id, new(ownerWallet.Id, 500_000, "o-c", Pin, config.Revision), default);
        await fundService.ContributeAsync(member.Id, fund.Id, new(memberWallet.Id, 500_000, "m-c", Pin, config.Revision), default);
        await fundService.SetSpendingPermissionAsync(owner.Id, fund.Id, owner.Id, true, Pin, default);

        var expense = await fundService.CreateFundRequestAsync(owner.Id, fund.Id,
            new("Expense", 300_000, owner.Id, "Trip supplies", "exp-1", Pin), default);

        await fundService.ReportFundRequestAsync(member.Id, expense.Id, "Suspicious", default);

        var session = new UserSession { UserId = nonAdmin.Id, LastSeenAt = DateTime.UtcNow };
        f.Db.UserSessions.Add(session);
        await f.Db.SaveChangesAsync();

        var adminService = CreateA5(f);
        var principal = V1PermissionTests.Principal(nonAdmin.Id, session.Id, "User");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            adminService.ResolveDisputeRequestAsync(principal, expense.Id,
                new("Reason", "Valid report message", false), default));
    }
}
