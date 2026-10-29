using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Features.Wallets.Commands.CreateWallet;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ giới hạn ba ví cá nhân mà không trộn với quỹ nhóm.
public sealed class PersonalWalletLimitTests
{
    [Fact]
    public async Task Registration_creates_exactly_one_zero_balance_default_main_wallet()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "Wallet registration");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var wallet = Assert.Single(await db.Wallets
            .Include(item => item.LedgerAccount)
            .Where(item => item.OwnerUserId == auth.User.Id)
            .ToListAsync());
        Assert.Equal(WalletType.Main, wallet.Type);
        Assert.True(wallet.IsDefault);
        Assert.Equal(0, wallet.AvailableBalance);
        Assert.Equal(0, wallet.LedgerAccount.Balance);
    }

    private static async Task LinkActiveBankAsync(SpendWiseApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        db.BankConnections.Add(new SpendWise.Domain.Banking.BankConnection
        {
            UserId = userId,
            Provider = SpendWise.Domain.Banking.BankProvider.SePayBankHub,
            AccountEnvironment = SpendWise.Domain.Banking.SePayAccountEnvironment.TestMode,
            ExternalAccountId = $"ACC_{userId:N}",
            BankBrandName = "MBBank",
            AccountHolderName = "Test User",
            MaskedAccountNumber = "****1234",
            Status = SpendWise.Domain.Banking.BankConnectionStatus.Active,
            LinkedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Registered_user_can_create_two_zero_balance_wallets_but_not_a_fourth()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "Three wallets");
        await LinkActiveBankAsync(factory, auth.User.Id);
        UseToken(client, auth.AccessToken);

        var savingsResponse = await client.PostAsJsonAsync(
            "/api/wallets",
            new CreateWalletRequest("Savings", "savings"));
        var emergencyResponse = await client.PostAsJsonAsync(
            "/api/wallets",
            new CreateWalletRequest("Emergency", "emergency"));

        Assert.Equal(HttpStatusCode.Created, savingsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, emergencyResponse.StatusCode);
        var savings = (await savingsResponse.Content.ReadFromJsonAsync<WalletDto>())!;
        var emergency = (await emergencyResponse.Content.ReadFromJsonAsync<WalletDto>())!;
        Assert.Equal(0, savings.AvailableBalance);
        Assert.Equal(0, emergency.AvailableBalance);

        var fourth = await client.PostAsJsonAsync(
            "/api/wallets",
            new CreateWalletRequest("Extra", "savings"));
        Assert.Equal(HttpStatusCode.Conflict, fourth.StatusCode);
        Assert.Equal("wallet_limit_reached", await ProblemCodeAsync(fourth));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(3, await db.Wallets.CountAsync(item => item.OwnerUserId == auth.User.Id));
    }

    [Fact]
    public async Task Existing_default_main_wallet_prevents_another_main_wallet()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "One Main wallet");
        await LinkActiveBankAsync(factory, auth.User.Id);
        UseToken(client, auth.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/wallets",
            new CreateWalletRequest("Another Main", "spending"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("main_wallet_exists", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Legacy_user_without_main_still_cannot_create_main_through_api_use_case()
    {
        // Arrange: mô phỏng dữ liệu cũ chưa có Main; handler vẫn không được tự sửa dữ liệu.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Legacy without Main");

        // Act: client thử tạo Main qua use case CreateWallet.
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateWalletCommandHandler(fixture.Db, new SpendWise.Application.Features.Banking.ActiveBankGuard(fixture.Db)).Handle(
                new CreateWalletCommand(user.Id, new("Main", "main")),
                default));

        // Assert: Main chỉ được tạo bởi luồng đăng ký, không có auto-repair tại đây.
        Assert.Equal("main_wallet_exists", error.Code);
        Assert.Empty(await fixture.Db.Wallets.Where(item => item.OwnerUserId == user.Id).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_creation_cannot_exceed_three_personal_wallets()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"spendwise-wallet-limit-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                DefaultTimeout = 30,
                Pooling = false
            }.ToString();
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
                .UseSqlite(connectionString)
                .Options;
            var clock = new SystemClock();
            Guid userId;
            await using (var setup = new SpendWiseDbContext(options, clock))
            {
                await setup.Database.EnsureCreatedAsync();
                var user = NewUser("Concurrent wallets");
                var main = WalletFactory.Create(user.Id, "Main", WalletType.Main, true);
                var savings = WalletFactory.Create(user.Id, "Savings", WalletType.Savings, false);
                var bank = new SpendWise.Domain.Banking.BankConnection
                {
                    UserId = user.Id,
                    Provider = SpendWise.Domain.Banking.BankProvider.SePayBankHub,
                    AccountEnvironment = SpendWise.Domain.Banking.SePayAccountEnvironment.TestMode,
                    ExternalAccountId = $"ACC_{user.Id:N}",
                    BankBrandName = "MBBank",
                    AccountHolderName = user.FullName,
                    MaskedAccountNumber = "****1234",
                    Status = SpendWise.Domain.Banking.BankConnectionStatus.Active,
                    LinkedAt = DateTime.UtcNow
                };
                setup.AddRange(user, main, savings, bank);
                await setup.SaveChangesAsync();
                userId = user.Id;
            }

            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = CreateConcurrentlyAsync(options, clock, gate.Task, userId, "Emergency A");
            var second = CreateConcurrentlyAsync(options, clock, gate.Task, userId, "Emergency B");
            gate.SetResult();
            var outcomes = await Task.WhenAll(first, second);

            Assert.Single(outcomes, outcome => outcome.Succeeded);
            Assert.Single(outcomes, outcome => outcome.ErrorCode == "wallet_limit_reached");
            await using var verification = new SpendWiseDbContext(options, clock);
            Assert.Equal(3, await verification.Wallets.CountAsync(item => item.OwnerUserId == userId));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task Shared_fund_does_not_count_as_a_personal_wallet()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Shared fund owner");
        var group = new Group { Name = "Group", Type = GroupType.Friends };
        var fundAccount = new LedgerAccount
        {
            Code = $"GROUP-FUND:{group.Id:N}",
            Name = "Group Shared Fund",
            Type = LedgerAccountType.SharedFund,
            GroupId = group.Id
        };
        group.SharedFund = new SharedFund
        {
            GroupId = group.Id,
            Group = group,
            LedgerAccountId = fundAccount.Id,
            LedgerAccount = fundAccount
        };
        fixture.Db.Groups.Add(group);
        await fixture.AddWalletAsync(user.Id, "Main", true);
        await fixture.Db.SaveChangesAsync();
        var handler = new CreateWalletCommandHandler(fixture.Db, new SpendWise.Application.Features.Banking.ActiveBankGuard(fixture.Db));

        await handler.Handle(new CreateWalletCommand(user.Id, new("Savings", "savings")), default);
        await handler.Handle(new CreateWalletCommand(user.Id, new("Emergency", "emergency")), default);

        Assert.Equal(3, await fixture.Db.Wallets.CountAsync(item => item.OwnerUserId == user.Id));
        Assert.Single(await fixture.Db.SharedFunds.ToListAsync());
    }

    [Fact]
    public async Task Closed_wallet_records_still_count_toward_the_limit()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Closed wallet limit");
        await fixture.AddWalletAsync(user.Id, "Main", true);
        await fixture.AddWalletAsync(
            user.Id,
            "Old Savings",
            false,
            WalletStatus.Closed,
            type: WalletType.Savings);
        await fixture.AddWalletAsync(
            user.Id,
            "Emergency",
            false,
            type: WalletType.Emergency);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateWalletCommandHandler(fixture.Db, new SpendWise.Application.Features.Banking.ActiveBankGuard(fixture.Db)).Handle(
                new CreateWalletCommand(user.Id, new CreateWalletRequest("Replacement", "savings")),
                default));

        Assert.Equal("wallet_limit_reached", error.Code);
        Assert.Equal(3, await fixture.Db.Wallets.CountAsync(item => item.OwnerUserId == user.Id));
    }

    private static async Task<(bool Succeeded, string? ErrorCode)> CreateConcurrentlyAsync(
        DbContextOptions<SpendWiseDbContext> options,
        SystemClock clock,
        Task gate,
        Guid userId,
        string name)
    {
        await gate;
        await using var db = new SpendWiseDbContext(options, clock);
        try
        {
            await new CreateWalletCommandHandler(db, new SpendWise.Application.Features.Banking.ActiveBankGuard(db)).Handle(
                new CreateWalletCommand(userId, new CreateWalletRequest(name, "emergency")),
                default);
            return (true, null);
        }
        catch (AppException exception)
        {
            return (false, exception.Code);
        }
    }

    private static User NewUser(string name)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new User
        {
            FullName = name,
            Email = $"{unique}@test.local",
            NormalizedEmail = $"{unique}@TEST.LOCAL",
            SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(),
            PasswordHash = "test-hash"
        };
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("code").GetString();
    }

    private static void UseToken(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
}
