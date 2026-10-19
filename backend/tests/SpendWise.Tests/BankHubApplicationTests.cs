using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.BankConnections.Commands.SyncBankConnection;
using SpendWise.Application.Features.BankIntegration.Commands.CreateSePaySandboxTransaction;
using SpendWise.Application.Features.Banking;
using SpendWise.Domain.Banking;
using SpendWise.Infrastructure.Notifications;
using Xunit;

namespace SpendWise.Tests;

// Các test mức Application bảo vệ manual sync và cổng Development-only của simulator.
public sealed class BankHubApplicationTests
{
    [Fact]
    public async Task Owner_syncs_pages_and_ipn_replay_stays_one_row()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Sync owner");
        var connection = await AddConnectionAsync(fixture, owner.Id, BankConnectionStatus.Active);
        var replay = Transaction("shared-provider-id", 10_000);
        var second = Transaction("second-provider-id", 20_000);
        var gateway = new PagingGateway([[replay], [second]]);
        var ingestion = new BankTransactionIngestionService(
            fixture.Db,
            new NotificationService(fixture.Db),
            fixture.Clock);

        // Arrange: IPN đã nhập giao dịch đầu trước khi manual sync nhìn thấy nó.
        await ingestion.IngestAsync(
            connection.Id,
            replay,
            BankTransactionSource.Webhook,
            "IPN:shared-provider-id",
            "HASH",
            default);
        var handler = new SyncBankConnectionCommandHandler(
            fixture.Db,
            gateway,
            new TestBankHubConfiguration(),
            ingestion,
            fixture.Clock);

        var result = await handler.Handle(new SyncBankConnectionCommand(owner.Id, connection.Id), default);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(2, await fixture.Db.BankTransactions.CountAsync());
        Assert.Equal(2, gateway.PagesRequested);
        Assert.NotNull(connection.LastSyncedAt);
    }

    [Fact]
    public async Task Sync_rejects_other_owner_and_inactive_connection()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner");
        var other = await fixture.AddUserAsync("Other");
        var connection = await AddConnectionAsync(fixture, owner.Id, BankConnectionStatus.Inactive);
        var handler = Handler(fixture, new PagingGateway([]));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SyncBankConnectionCommand(other.Id, connection.Id), default));
        var inactive = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new SyncBankConnectionCommand(owner.Id, connection.Id), default));
        Assert.Equal("bank_connection_not_active", inactive.Code);
    }

    [Fact]
    public async Task Provider_failure_does_not_set_last_synced_at()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Failure owner");
        var connection = await AddConnectionAsync(fixture, owner.Id, BankConnectionStatus.Active);
        var gateway = new PagingGateway([]) { Fail = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(fixture, gateway).Handle(
                new SyncBankConnectionCommand(owner.Id, connection.Id),
                default));

        Assert.Null(connection.LastSyncedAt);
        Assert.Empty(fixture.Db.BankTransactions);
    }

    [Fact]
    public async Task Sandbox_command_calls_provider_but_does_not_insert_transaction()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Sandbox admin");
        var connection = await AddConnectionAsync(fixture, owner.Id, BankConnectionStatus.Active);
        var gateway = new PagingGateway([]);
        var handler = new CreateSePaySandboxTransactionCommandHandler(
            fixture.Db,
            gateway,
            new TestBankHubConfiguration());

        var result = await handler.Handle(
            new CreateSePaySandboxTransactionCommand(
                true,
                new CreateBankSandboxTransactionRequest(connection.Id, "credit", 500_000, "Demo")),
            default);

        Assert.True(result.Accepted);
        Assert.Equal(1, gateway.SandboxCalls);
        Assert.Empty(fixture.Db.BankTransactions);
    }

    [Fact]
    public async Task Sandbox_command_is_forbidden_outside_development_or_provider_sandbox()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var request = new CreateBankSandboxTransactionRequest(Guid.NewGuid(), "credit", 1, "Demo");
        var gateway = new PagingGateway([]);
        var sandboxHandler = new CreateSePaySandboxTransactionCommandHandler(
            fixture.Db,
            gateway,
            new TestBankHubConfiguration());
        var productionProviderHandler = new CreateSePaySandboxTransactionCommandHandler(
            fixture.Db,
            gateway,
            new TestBankHubConfiguration { IsSandbox = false });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sandboxHandler.Handle(new CreateSePaySandboxTransactionCommand(false, request), default));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            productionProviderHandler.Handle(new CreateSePaySandboxTransactionCommand(true, request), default));
        Assert.Equal(0, gateway.SandboxCalls);
    }

    private static SyncBankConnectionCommandHandler Handler(
        TargetedCorrectnessFixture fixture,
        PagingGateway gateway)
    {
        var ingestion = new BankTransactionIngestionService(
            fixture.Db,
            new NotificationService(fixture.Db),
            fixture.Clock);
        return new SyncBankConnectionCommandHandler(
            fixture.Db,
            gateway,
            new TestBankHubConfiguration(),
            ingestion,
            fixture.Clock);
    }

    private static async Task<BankConnection> AddConnectionAsync(
        TargetedCorrectnessFixture fixture,
        Guid userId,
        BankConnectionStatus status)
    {
        var connection = new BankConnection
        {
            UserId = userId,
            Provider = BankProvider.SePayBankHub,
            ExternalAccountId = $"account-{Guid.NewGuid():N}",
            BankBrandName = "Test Bank",
            MaskedAccountNumber = "****1234",
            Status = status,
            LinkedAt = fixture.Clock.UtcNow
        };
        fixture.Db.BankConnections.Add(connection);
        await fixture.Db.SaveChangesAsync();
        return connection;
    }

    private static ExternalBankTransactionData Transaction(string id, decimal amount) => new(
        id,
        BankTransactionDirection.Credit,
        amount,
        DateTime.UtcNow,
        "Synced transaction",
        null,
        null,
        "Test Bank");

    private sealed class PagingGateway(IReadOnlyList<IReadOnlyList<ExternalBankTransactionData>> pages) : IBankHubGateway
    {
        public int PagesRequested { get; private set; }
        public int SandboxCalls { get; private set; }
        public bool Fail { get; set; }

        public Task<BankProviderLinkResult> CreateLinkAsync(BankProviderLinkRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ExternalBankTransactionPage> GetTransactionsAsync(
            string externalBankAccountId,
            DateTime from,
            DateTime to,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if (Fail) throw new InvalidOperationException("Provider failed.");
            PagesRequested++;
            var items = page <= pages.Count ? pages[page - 1] : [];
            return Task.FromResult(new ExternalBankTransactionPage(items, page, Math.Max(1, pages.Count)));
        }

        public Task CreateSandboxTransactionAsync(
            string externalBankAccountId,
            BankTransactionDirection direction,
            decimal amount,
            string content,
            CancellationToken cancellationToken)
        {
            SandboxCalls++;
            return Task.CompletedTask;
        }
    }
}

// Cấu hình test không chứa secret và chỉ mô phỏng quyết định availability/môi trường.
internal sealed class TestBankHubConfiguration : IBankHubConfiguration
{
    public bool IsSandbox { get; set; } = true;
    public void EnsureAvailable() { }
    public string ResolveCompletionRedirectUri(string? requestedUri) => requestedUri ?? "https://localhost/callback";
    public string ResolveMobileCompletionRedirectUri(string? requestedUri) =>
        string.IsNullOrWhiteSpace(requestedUri)
            ? "https://mobile.spendwise.test/bank-callback"
            : throw new AppException("invalid_redirect_uri", "Mobile completion redirect URI is controlled by the server.");
    public bool VerifyLifecycleSecret(string suppliedSecret) => suppliedSecret == "secret";
    public bool VerifyIpnAuthorization(string authorizationHeader) => authorizationHeader == "Apikey key";
    public SePayBankHubStatusDto GetSafeStatus() => new(true, IsSandbox ? "Sandbox" : "Production", true, true, true, true, null, null);
    public void MarkWebhookReceived(DateTime receivedAt) { }
}
