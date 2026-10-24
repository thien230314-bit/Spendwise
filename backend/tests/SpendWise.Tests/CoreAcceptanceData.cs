using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Features.Wallets;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Banking;
using SpendWise.Application.Features.Banking;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;

namespace SpendWise.Tests;

internal sealed record CoreFundSeed(Guid FundId, User Owner, User Member, User Admin, UserSession AdminSession, Guid OwnerWallet, Guid MemberWallet);

internal static class CoreAcceptanceData
{
    internal const string Pin = IdentityTestData.DefaultPin;
    internal static V1FundService Funds(SpendWiseDbContext db, IClock? suppliedClock = null)
    {
        var clock = suppliedClock ?? new SystemClock();
        return new(db, clock, new(db, new SecurityPinService(), new AccountCredentialLockout(db, clock)),
            new NotificationService(db), new LedgerService(db, clock), new ActiveBankGuard(db));
    }

    internal static async Task<CoreFundSeed> SeedAsync(SpendWiseDbContext db, bool fixedFund = true, bool ledgerOpening = false)
    {
        var owner = CoreAcceptanceHttpTests.SyntheticUser(); var member = CoreAcceptanceHttpTests.SyntheticUser();
        owner.EmailVerifiedAt = member.EmailVerifiedAt = DateTime.UtcNow;
        var admin = CoreAcceptanceHttpTests.SyntheticUser(SystemRole.Admin);
        var session = new UserSession { UserId = admin.Id, LastSeenAt = DateTime.UtcNow };
        var ow = WalletFactory.Create(owner.Id, "Synthetic owner", WalletType.Main, true);
        var mw = WalletFactory.Create(member.Id, "Synthetic member", WalletType.Main, true);
        ow.AvailableBalance = ow.LedgerAccount.Balance = mw.AvailableBalance = mw.LedgerAccount.Balance = ledgerOpening ? 0 : 2_000_000;
        var ownerBank = new BankConnection
        {
            UserId = owner.Id,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = $"ACC_{owner.Id:N}",
            BankBrandName = "MBBank",
            AccountHolderName = owner.FullName,
            MaskedAccountNumber = "****1234",
            Status = BankConnectionStatus.Active,
            LinkedAt = DateTime.UtcNow
        };
        var memberBank = new BankConnection
        {
            UserId = member.Id,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = $"ACC_{member.Id:N}",
            BankBrandName = "MBBank",
            AccountHolderName = member.FullName,
            MaskedAccountNumber = "****5678",
            Status = BankConnectionStatus.Active,
            LinkedAt = DateTime.UtcNow
        };
        db.AddRange(owner, member, admin, session, ow, mw, ownerBank, memberBank); await db.SaveChangesAsync();
        if (ledgerOpening)
        {
            var source = await db.LedgerAccounts.SingleOrDefaultAsync(x => x.Code == "SYNTHETIC-OPENING");
            if (source is null)
            {
                source = new LedgerAccount { Code = "SYNTHETIC-OPENING", Name = "Synthetic opening source", Type = LedgerAccountType.ExternalSource, IsSystem = true };
                db.LedgerAccounts.Add(source);
                await db.SaveChangesAsync();
            }
            var ledger = new LedgerService(db, new SystemClock());
            foreach (var wallet in new[] { ow, mw })
                await ledger.PostAsync(source.Id, wallet.LedgerAccount.Id, 2_000_000, "VND", "SyntheticOpening", wallet.Id, "synthetic-opening", wallet.Id.ToString("N"), "Synthetic fixture funding", default);
        }
        var funds = Funds(db);
        var fund = await funds.CreateAsync(owner.Id, new("Synthetic core acceptance", fixedFund ? "Fixed" : "Free", fixedFund ? 1_000_000 : null, Pin), default);
        db.V1FundMemberships.Add(new() { FundId = fund.Id, UserId = member.Id, Seat = 2 }); await db.SaveChangesAsync();
        long? revision = fixedFund ? (await funds.ConfirmObligationsAsync(owner.Id, fund.Id, new(1_000_000, Pin), default)).Revision : null;
        await funds.ContributeAsync(owner.Id, fund.Id, new(ow.Id, 500_000, "synthetic-owner", Pin, revision), default);
        await funds.ContributeAsync(member.Id, fund.Id, new(mw.Id, 500_000, "synthetic-member", Pin, revision), default);
        return new(fund.Id, owner, member, admin, session, ow.Id, mw.Id);
    }

    internal static async Task<Guid> PendingAsync(SpendWiseDbContext db, CoreFundSeed seed)
    {
        var request = await Funds(db).CreateFundRequestAsync(seed.Owner.Id, seed.FundId,
            new("Expense", 300_000, seed.Owner.Id, "Synthetic expense", "synthetic-expense", Pin), default);
        return request.Id;
    }

    internal static async Task<decimal> HeldAsync(SpendWiseDbContext db, Guid fundId) =>
        (await db.V1FundRequests.AsNoTracking().Where(r => r.FundId == fundId && (r.State == "Pending24h" || r.State == "Suspended")).ToListAsync()).Sum(r => r.AmountVnd);
}
