using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Identity;
using SpendWise.Application.Features.Wallets;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FinanceHttpTests
{
    [Fact]
    public async Task Fixed_confirm_then_partial_wallet_contribution_posts_once_and_replays_exactly()
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient();
        Guid fundId, walletId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var key = Guid.NewGuid().ToString("N");
            var user = new User { FullName = "Synthetic", Email = key + "@test.local", NormalizedEmail = key.ToUpperInvariant() + "@TEST.LOCAL",
                SpendId = key, NormalizedSpendId = key.ToUpperInvariant(), PasswordHash = "synthetic", SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin) };
            var session = new UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow };
            var fund = new V1Fund("Fixed", "SPW000001", V1FundKind.Fixed, user.Id, 100);
            var wallet = WalletFactory.Create(user.Id, "Synthetic wallet", WalletType.Main, true);
            wallet.AvailableBalance = wallet.LedgerAccount.Balance = 100;
            db.AddRange(user, session, fund, wallet, new V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 1 });
            await db.SaveChangesAsync(); fundId = fund.Id; walletId = wallet.Id;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, session.Id).Token);
        }
        using var confirm = await client.PostAsJsonAsync($"/api/v1/funds/{fundId}/obligations/confirm", new { TargetVnd = 100, SecurityPin = IdentityTestData.DefaultPin });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        using var config = JsonDocument.Parse(await confirm.Content.ReadAsStreamAsync());
        var revision = config.RootElement.GetProperty("revision").GetInt64();
        var body = new { WalletId = walletId, AmountVnd = 25, ObligationRevision = revision, IdempotencyKey = "synthetic-partial", SecurityPin = IdentityTestData.DefaultPin };
        using var first = await client.PostAsJsonAsync($"/api/v1/funds/{fundId}/contributions", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var replay = await client.PostAsJsonAsync($"/api/v1/funds/{fundId}/contributions", body);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var json = JsonDocument.Parse(await replay.Content.ReadAsStreamAsync());
        Assert.True(json.RootElement.GetProperty("replayed").GetBoolean());
        using var verifyScope = factory.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(75, await verify.Wallets.Where(w => w.Id == walletId).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Single(await verify.LedgerTransactions.ToListAsync());
        Assert.Equal(2, await verify.LedgerEntries.CountAsync());
        Assert.Equal(25, (await verify.LedgerEntries.Where(e => e.Direction == LedgerEntryDirection.Debit).ToListAsync()).Sum(e => e.Amount));
        Assert.Equal(25, (await verify.LedgerEntries.Where(e => e.Direction == LedgerEntryDirection.Credit).ToListAsync()).Sum(e => e.Amount));
        Assert.Single(await verify.FinancialTransactions.ToListAsync());
    }
}
