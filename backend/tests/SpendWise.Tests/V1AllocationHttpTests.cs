using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1AllocationHttpTests
{
    [Fact]
    public async Task Fixed_integer_allocation_is_actual_read_contract_not_posted_money()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        Guid fundId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var users = Enumerable.Range(0, 3).Select(_ =>
            {
                var id = Guid.NewGuid().ToString("N");
                return new User { FullName = "Synthetic", SpendId = id, NormalizedSpendId = id.ToUpperInvariant(),
                    Email = id + "@test.local", NormalizedEmail = id.ToUpperInvariant() + "@TEST.LOCAL", PasswordHash = "synthetic" };
            }).ToArray();
            var session = new UserSession { UserId = users[0].Id, LastSeenAt = DateTime.UtcNow };
            var fund = new V1Fund("Fixed", "SPW000001", V1FundKind.Fixed, users[0].Id, 101);
            db.AddRange(users); db.AddRange(session, fund);
            var seats = new[] { 1, 3, 9 };
            for (var i = 0; i < 3; i++) db.Add(new V1FundMembership { FundId = fund.Id, UserId = users[i].Id, Seat = seats[i] });
            await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(users[0], session.Id).Token);
            fundId = fund.Id;
        }
        using var response = await client.GetAsync($"/api/v1/funds/{fundId}/contribution-allocation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        Assert.False(json.RootElement.GetProperty("postingEnabled").GetBoolean());
        Assert.Equal(101, json.RootElement.GetProperty("targetVnd").GetInt64());
        var items = json.RootElement.GetProperty("members").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 3, 9 }, items.Select(x => x.GetProperty("seat").GetInt32()));
        Assert.Equal(new long[] { 34, 34, 33 }, items.Select(x => x.GetProperty("requiredVnd").GetInt64()));
        using var verifyScope = factory.Services.CreateScope();
        Assert.Empty(await verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().LedgerTransactions.ToListAsync());
    }
}
