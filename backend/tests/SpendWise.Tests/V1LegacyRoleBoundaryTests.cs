using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SpendWise.Application.AccountRecovery;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.AccountRecovery.Commands.ReviewAccountRecovery;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Users.Commands.ChangeUserStatus;
using SpendWise.Application.Features.Admin.Users.Commands.ResetUserPassword;
using SpendWise.Application.Features.Admin.Users.Commands.ResetUserSecurityPin;
using SpendWise.Application.Features.Admin.Users.Commands.RevokeUserSessions;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1LegacyRoleBoundaryTests
{
    public static IEnumerable<object[]> Cases => from role in new[] { SystemRole.Admin, SystemRole.SuperAdmin }
        from action in new[] { "password", "pin", "status", "sessions", "recovery" }
        from stale in new[] { false, true } select new object[] { role, action, stale };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Legacy_operator_cannot_mutate_V1_privileged_accounts(SystemRole role, string action, bool stale)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Legacy operator", role: SystemRole.SystemAdmin);
        var target = await f.AddUserAsync("V1 operator", role: stale ? SystemRole.User : role);
        if (stale) await f.Db.Users.Where(u => u.Id == target.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SystemRole, role));
        var passwordHash = target.PasswordHash; var pinHash = target.SecurityPinHash;
        var session = await f.AddSessionAsync(target.Id);
        var auth = new AuthenticationSupport(f.Db, new TargetedTokenService(), f.Clock);
        var guard = new AdminSecurityGuard(f.Db, new SecurityPinService(), new AccountCredentialLockout(f.Db, f.Clock));
        var audit = new AdminAuditWriter(f.Db);
        var recovery = new AccountRecoveryRequest { FullName = "Synthetic", Phone = "0900000001", Description = "Synthetic recovery", ResolvedUserId = target.Id };
        f.Db.AccountRecoveryRequests.Add(recovery); await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ForbiddenException>(async () =>
        {
            switch (action)
            {
                case "password":
                    await new ResetUserPasswordCommandHandler(f.Db, guard, audit, new TemporaryPasswordService(), new PasswordService(), auth)
                        .Handle(new(actor.Id, target.Id, new("Attempted privileged reset", TargetedCorrectnessFixture.SecurityPin)), default);
                    break;
                case "pin":
                    await new ResetUserSecurityPinCommandHandler(f.Db, guard, audit,
                        new SecurityPinResetSupport(f.Db, new LoginChallengeTokenService(), f.Clock), auth, new NoEmail(), NullLogger<ResetUserSecurityPinCommandHandler>.Instance)
                        .Handle(new(actor.Id, target.Id, new("Attempted privileged reset", TargetedCorrectnessFixture.SecurityPin)), default);
                    break;
                case "status":
                    await new ChangeUserStatusCommandHandler(f.Db, auth, audit, guard)
                        .Handle(new(actor.Id, target.Id, new("Suspended", "Attempted status change", TargetedCorrectnessFixture.SecurityPin)), default);
                    break;
                case "sessions":
                    await new RevokeUserSessionsCommandHandler(f.Db, auth, audit)
                        .Handle(new(actor.Id, target.Id, "Attempted session revoke"), default);
                    break;
                case "recovery":
                    await new ApproveAccountRecoveryCommandHandler(f.Db, guard, audit, new TemporaryPasswordService(), new PasswordService(), auth,
                        new V1FriendLimitTests.CapturingNotifications(), f.Clock)
                        .Handle(new(actor.Id, recovery.Id, new(TargetedCorrectnessFixture.SecurityPin, "Attempted recovery", target.Id)), default);
                    break;
            }
        });
        Assert.Equal("admin_peer_protected", error.Code);
        f.Db.ClearTrackedEntities();
        var current = await f.Db.Users.SingleAsync(u => u.Id == target.Id);
        Assert.Equal(role, current.SystemRole); Assert.Equal(passwordHash, current.PasswordHash); Assert.Equal(pinHash, current.SecurityPinHash);
        Assert.Equal(UserStatus.Active, current.Status);
        Assert.True(await f.Db.UserSessions.AnyAsync(s => s.Id == session.Id && s.RevokedAt == null));
        Assert.Empty(await f.Db.SecurityPinResetTokens.ToListAsync());
    }

    private sealed class NoEmail : IEmailSender
    {
        public Task SendSecurityPinResetAsync(string email, string rawToken, DateTime expiresAt, CancellationToken ct) =>
            throw new InvalidOperationException("No email may be sent for a denied privileged target.");
    }
}
