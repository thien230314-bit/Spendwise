using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Banking;
using SpendWise.Application.Identity;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Các test đi qua HTTP thật để kiểm tra JWT ownership và xác thực webhook/IPN.
public sealed class BankHubHttpIntegrationTests
{
    [Fact]
    public async Task Mobile_link_uses_only_trusted_server_completion_and_posts_once()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Mobile bank owner");
        UseToken(client, user.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/bank-connections/link",
            new CreateBankLinkRequest(null, "vi", BankLinkClientPlatform.Mobile));

        response.EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var gateway = Assert.IsType<TestBankHubGateway>(
            scope.ServiceProvider.GetRequiredService<IBankHubGateway>());
        var providerRequest = Assert.Single(gateway.LinkRequests);
        Assert.Equal(BankLinkClientPlatform.Mobile, providerRequest.ClientPlatform);
        Assert.Equal("https://mobile.spendwise.test/bank-callback", providerRequest.CompletionRedirectUri);
        Assert.Single(scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().BankLinkSessions);

        var rejected = await client.PostAsJsonAsync(
            "/api/bank-connections/link",
            new CreateBankLinkRequest(
                "https://mobile.spendwise.test/attacker-selected",
                "vi",
                BankLinkClientPlatform.Mobile));

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("invalid_redirect_uri", await ErrorCodeAsync(rejected));
        Assert.Single(gateway.LinkRequests);

        var unsupported = await client.PostAsJsonAsync(
            "/api/bank-connections/link",
            new { language = "vi", clientPlatform = 999 });
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Equal("invalid_input", await ErrorCodeAsync(unsupported));
        Assert.Single(gateway.LinkRequests);
    }

    [Fact]
    public async Task Missing_mobile_completion_fails_only_mobile_while_web_still_works()
    {
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["SePayBankHub:MobileCompletionRedirectUri"] = null
        });
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Optional mobile configuration user");
        UseToken(client, user.AccessToken);

        var web = await client.PostAsJsonAsync(
            "/api/bank-connections/link",
            new CreateBankLinkRequest());
        web.EnsureSuccessStatusCode();

        var mobile = await client.PostAsJsonAsync(
            "/api/bank-connections/link",
            new CreateBankLinkRequest(null, "vi", BankLinkClientPlatform.Mobile));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, mobile.StatusCode);
        Assert.Equal("bank_mobile_completion_unavailable", await ErrorCodeAsync(mobile));
        using var scope = factory.Services.CreateScope();
        var gateway = Assert.IsType<TestBankHubGateway>(
            scope.ServiceProvider.GetRequiredService<IBankHubGateway>());
        var webRequest = Assert.Single(gateway.LinkRequests);
        Assert.Equal(BankLinkClientPlatform.Web, webRequest.ClientPlatform);
    }

    [Fact]
    public async Task SystemAdmin_cannot_initiate_mobile_bank_link()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var candidate = await RegisterAsync(client, "Mobile bank admin");
        await PromoteToAdminAsync(factory, candidate.User.Id);
        var admin = await IdentityTestData.LoginAndVerifyPinAsync(
            client,
            candidate.User.Email,
            "StrongPass1");
        UseToken(client, admin.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/bank-connections/link",
            new CreateBankLinkRequest(null, "vi", BankLinkClientPlatform.Mobile));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var gateway = Assert.IsType<TestBankHubGateway>(
            scope.ServiceProvider.GetRequiredService<IBankHubGateway>());
        Assert.Empty(gateway.LinkRequests);
    }

    [Fact]
    public async Task User_creates_link_session_bound_to_jwt_and_cannot_read_another_connection()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await RegisterAsync(client, "Bank owner");
        var other = await RegisterAsync(client, "Other bank user");
        UseToken(client, owner.AccessToken);

        // userId thừa trong JSON không được dùng; Handler lấy owner từ JWT.
        var response = await client.PostAsJsonAsync("/api/bank-connections/link", new
        {
            userId = other.User.Id,
            completionRedirectUri = "https://localhost:5173/bank-callback",
            language = "vi"
        });
        response.EnsureSuccessStatusCode();
        var link = await response.Content.ReadFromJsonAsync<BankLinkSessionDto>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var session = await db.BankLinkSessions.SingleAsync(item => item.Id == link!.SessionId);
        Assert.Equal(owner.User.Id, session.UserId);
        Assert.DoesNotContain("raw", session.ExternalLinkTokenXid, StringComparison.OrdinalIgnoreCase);

        var connection = await AddConnectionAsync(db, owner.User.Id, "account-owner");
        UseToken(client, other.AccessToken);
        var forbiddenRead = await client.GetAsync($"/api/bank-connections/{connection.Id}");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenRead.StatusCode);
    }

    [Fact]
    public async Task Invalid_redirect_is_rejected_and_disabled_integration_returns_503()
    {
        using var invalidFactory = new SpendWiseApiFactory();
        using var invalidClient = invalidFactory.CreateHttpsClient();
        var user = await RegisterAsync(invalidClient, "Redirect user");
        UseToken(invalidClient, user.AccessToken);
        var invalid = await invalidClient.PostAsJsonAsync(
            "/api/bank-connections/link",
            new CreateBankLinkRequest("https://evil.example/callback", "vi"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var disabledFactory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["SePayBankHub:Enabled"] = "false"
        });
        using var disabledClient = disabledFactory.CreateHttpsClient();
        var disabledUser = await RegisterAsync(disabledClient, "Disabled integration user");
        UseToken(disabledClient, disabledUser.AccessToken);
        var disabled = await disabledClient.PostAsJsonAsync(
            "/api/bank-connections/link",
            new CreateBankLinkRequest(null, "vi"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, disabled.StatusCode);
        Assert.Equal("bank_integration_unavailable", await ErrorCodeAsync(disabled));
    }

    [Fact]
    public async Task Lifecycle_link_maps_by_link_token_and_duplicate_notifies_once()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Lifecycle owner");
        UseToken(client, user.AccessToken);
        var linkResponse = await client.PostAsJsonAsync(
            "/api/bank-connections/link",
            new CreateBankLinkRequest(null, "vi"));
        var link = await linkResponse.Content.ReadFromJsonAsync<BankLinkSessionDto>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var session = await db.BankLinkSessions.AsNoTracking().SingleAsync(item => item.Id == link!.SessionId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/sepay/bankhub/events")
        {
            Content = JsonContent.Create(new
            {
                timestamp = 1788470000,
                xid = "event-linked-1",
                version = "1",
                @event = "BANK_ACCOUNT_LINKED",
                metadata = new
                {
                    link_token_xid = session.ExternalLinkTokenXid,
                    bank_account_xid = "external-account-1",
                    brand_name = "Test Bank",
                    account_number = "1234567890",
                    account_holder_name = "Lifecycle owner"
                }
            })
        };
        request.Headers.Add("X-Secret-Key", "test-lifecycle-secret");
        var first = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Gửi lại cùng event xid; side effect phải không lặp.
        using var duplicate = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/sepay/bankhub/events")
        {
            Content = JsonContent.Create(new
            {
                timestamp = 1788470000,
                xid = "event-linked-1",
                version = "1",
                @event = "BANK_ACCOUNT_LINKED",
                metadata = new { link_token_xid = session.ExternalLinkTokenXid, bank_account_xid = "external-account-1" }
            })
        };
        duplicate.Headers.Add("X-Secret-Key", "test-lifecycle-secret");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(duplicate)).StatusCode);

        db.ChangeTracker.Clear();
        var connection = await db.BankConnections.SingleAsync();
        Assert.Equal(user.User.Id, connection.UserId);
        Assert.Equal("****7890", connection.MaskedAccountNumber);
        Assert.Equal(BankConnectionStatus.Active, connection.Status);
        Assert.Single(await db.Notifications.Where(item => item.Type == SpendWise.Domain.Notifications.NotificationType.BankAccountLinked).ToListAsync());
        Assert.Single(await db.OutboxMessages.ToListAsync());
        var integrationEvent = await db.BankIntegrationEvents.SingleAsync();
        Assert.Equal(BankIntegrationEventStatus.Processed, integrationEvent.ProcessingStatus);
        Assert.Equal(1, integrationEvent.DuplicateDeliveryCount);
        Assert.NotNull(integrationEvent.LastDuplicateReceivedAt);
    }

    [Fact]
    public async Task Unknown_link_token_is_unmatched_and_invalid_secret_mutates_nothing()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var payload = new
        {
            timestamp = 1788470000,
            xid = "event-unknown-link",
            version = "1",
            @event = "BANK_ACCOUNT_LINKED",
            metadata = new { link_token_xid = "missing", bank_account_xid = "unknown-account" }
        };

        var invalid = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/sepay/bankhub/events")
        {
            Content = JsonContent.Create(payload)
        };
        invalid.Headers.Add("X-Secret-Key", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(invalid)).StatusCode);

        using (var beforeScope = factory.Services.CreateScope())
        {
            var before = beforeScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            Assert.Empty(before.BankIntegrationEvents);
        }

        var valid = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/sepay/bankhub/events")
        {
            Content = JsonContent.Create(payload)
        };
        valid.Headers.Add("X-Secret-Key", "test-lifecycle-secret");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(valid)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Empty(db.BankConnections);
        Assert.Equal(BankIntegrationEventStatus.Unmatched, (await db.BankIntegrationEvents.SingleAsync()).ProcessingStatus);
    }

    [Theory]
    [InlineData("BANK_ACCOUNT_INACTIVATED")]
    [InlineData("BANK_ACCOUNT_INACTIVED")]
    public async Task Both_inactive_event_spellings_map_to_inactive(string eventName)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Inactive owner");
        Guid connectionId;
        using (var setupScope = factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            connectionId = (await AddConnectionAsync(setupDb, user.User.Id, $"account-{eventName}")).Id;
        }
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/sepay/bankhub/events")
        {
            Content = JsonContent.Create(new
            {
                timestamp = 1788470000,
                xid = $"event-{eventName}",
                version = "1",
                @event = eventName,
                metadata = new { bank_account_xid = $"account-{eventName}" }
            })
        };
        request.Headers.Add("X-Secret-Key", "test-lifecycle-secret");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(BankConnectionStatus.Inactive, (await db.BankConnections.FindAsync(connectionId))!.Status);
    }

    [Theory]
    [InlineData("credit", BankTransactionDirection.Credit)]
    [InlineData("debit", BankTransactionDirection.Debit)]
    public async Task Valid_ipn_imports_external_transaction_without_wallet_or_ledger_mutation(
        string transferType,
        BankTransactionDirection expectedDirection)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, $"IPN {transferType}");
        decimal walletBefore;
        int ledgerBefore;
        using (var setupScope = factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            await AddConnectionAsync(setupDb, user.User.Id, $"ipn-account-{transferType}");
            walletBefore = await setupDb.Wallets.Where(item => item.OwnerUserId == user.User.Id).Select(item => item.AvailableBalance).SingleAsync();
            ledgerBefore = await setupDb.LedgerTransactions.CountAsync();
        }

        var response = await SendIpnAsync(
            client,
            "test-ipn-key",
            $"ipn-account-{transferType}",
            $"transaction-{transferType}",
            transferType,
            125_000m);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(expectedDirection, (await db.BankTransactions.SingleAsync()).Direction);
        Assert.Equal(walletBefore, await db.Wallets.Where(item => item.OwnerUserId == user.User.Id).Select(item => item.AvailableBalance).SingleAsync());
        Assert.Equal(ledgerBefore, await db.LedgerTransactions.CountAsync());
        Assert.Empty(db.FinancialTransactions);
    }

    [Fact]
    public async Task Duplicate_ipn_imports_once_and_unknown_account_is_unmatched()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Duplicate IPN owner");
        using (var setupScope = factory.Services.CreateScope())
            await AddConnectionAsync(setupScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>(), user.User.Id, "duplicate-account");

        Assert.Equal(HttpStatusCode.OK, (await SendIpnAsync(client, "test-ipn-key", "duplicate-account", "same-id", "credit", 10_000)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendIpnAsync(client, "test-ipn-key", "duplicate-account", "same-id", "credit", 10_000)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendIpnAsync(client, "test-ipn-key", "missing-account", "unknown-id", "debit", 20_000)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Single(db.BankTransactions);
        Assert.Single(db.Notifications.Where(item => item.Type == SpendWise.Domain.Notifications.NotificationType.BankTransactionImported));
        Assert.Single(db.OutboxMessages.Where(item => item.UserId == user.User.Id));
        var processedEvent = await db.BankIntegrationEvents.SingleAsync(item => item.ExternalTransactionId == "same-id");
        Assert.Equal(BankIntegrationEventStatus.Unmatched, processedEvent.ProcessingStatus);
        Assert.Equal(1, processedEvent.DuplicateDeliveryCount);
        Assert.NotNull(processedEvent.LastDuplicateReceivedAt);
        Assert.Contains(db.BankIntegrationEvents, item => item.ExternalTransactionId == "unknown-id" && item.ProcessingStatus == BankIntegrationEventStatus.Unmatched);
    }

    [Fact]
    public async Task Concurrent_duplicate_ipn_preserves_status_and_has_one_financially_safe_side_effect()
    {
        // File SQLite cấp connection riêng cho từng request scope để kiểm tra delivery đồng thời thật.
        using var factory = new SpendWiseApiFactory(useIndependentDatabaseConnections: true);
        using var firstClient = factory.CreateHttpsClient();
        using var secondClient = factory.CreateHttpsClient();
        var user = await RegisterAsync(firstClient, "Concurrent IPN owner");
        decimal walletBefore;
        decimal ledgerBalanceBefore;
        int ledgerTransactionsBefore;
        int ledgerEntriesBefore;
        int financialTransactionsBefore;
        using (var setupScope = factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            await AddConnectionAsync(setupDb, user.User.Id, "concurrent-ipn-account");
            walletBefore = await setupDb.Wallets
                .Where(item => item.OwnerUserId == user.User.Id)
                .Select(item => item.AvailableBalance)
                .SingleAsync();
            ledgerBalanceBefore = await setupDb.LedgerAccounts
                .Where(item => item.OwnerUserId == user.User.Id)
                .SumAsync(item => item.Balance);
            ledgerTransactionsBefore = await setupDb.LedgerTransactions.CountAsync();
            ledgerEntriesBefore = await setupDb.LedgerEntries.CountAsync();
            financialTransactionsBefore = await setupDb.FinancialTransactions.CountAsync();
        }

        // Hai request dùng HttpClient khác nhau và được phát cùng lúc qua hai request scope độc lập.
        var responses = await Task.WhenAll(
            SendIpnAsync(firstClient, "test-ipn-key", "concurrent-ipn-account", "concurrent-ipn-id", "credit", 75_000),
            SendIpnAsync(secondClient, "test-ipn-key", "concurrent-ipn-account", "concurrent-ipn-id", "credit", 75_000));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(1, await db.BankTransactions.CountAsync(item => item.ExternalTransactionId == "concurrent-ipn-id"));
        var integrationEvent = await db.BankIntegrationEvents.SingleAsync(
            item => item.ExternalEventId == "IPN:concurrent-ipn-id");
        Assert.Equal(BankIntegrationEventStatus.Unmatched, integrationEvent.ProcessingStatus);
        Assert.Equal(1, integrationEvent.DuplicateDeliveryCount);
        Assert.NotNull(integrationEvent.LastDuplicateReceivedAt);
        Assert.Equal(1, await db.Notifications.CountAsync(
            item => item.UserId == user.User.Id
                && item.Type == SpendWise.Domain.Notifications.NotificationType.BankTransactionImported));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(item => item.UserId == user.User.Id));
        Assert.Equal(walletBefore, await db.Wallets
            .Where(item => item.OwnerUserId == user.User.Id)
            .Select(item => item.AvailableBalance)
            .SingleAsync());
        Assert.Equal(ledgerBalanceBefore, await db.LedgerAccounts
            .Where(item => item.OwnerUserId == user.User.Id)
            .SumAsync(item => item.Balance));
        Assert.Equal(ledgerTransactionsBefore, await db.LedgerTransactions.CountAsync());
        Assert.Equal(ledgerEntriesBefore, await db.LedgerEntries.CountAsync());
        Assert.Equal(financialTransactionsBefore, await db.FinancialTransactions.CountAsync());

        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Concurrent_duplicate_lifecycle_event_creates_one_connection_and_one_notification()
    {
        using var factory = new SpendWiseApiFactory(useIndependentDatabaseConnections: true);
        using var firstClient = factory.CreateHttpsClient();
        using var secondClient = factory.CreateHttpsClient();
        var user = await RegisterAsync(firstClient, "Concurrent lifecycle owner");
        UseToken(firstClient, user.AccessToken);
        var linkResponse = await firstClient.PostAsJsonAsync(
            "/api/bank-connections/link",
            new CreateBankLinkRequest(null, "vi"));
        linkResponse.EnsureSuccessStatusCode();
        var link = (await linkResponse.Content.ReadFromJsonAsync<BankLinkSessionDto>())!;
        string linkTokenXid;
        using (var setupScope = factory.Services.CreateScope())
        {
            linkTokenXid = await setupScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
                .BankLinkSessions
                .Where(item => item.Id == link.SessionId)
                .Select(item => item.ExternalLinkTokenXid)
                .SingleAsync();
        }

        var responses = await Task.WhenAll(
            SendLinkedLifecycleAsync(firstClient, "concurrent-lifecycle-id", linkTokenXid, "concurrent-bank-account"),
            SendLinkedLifecycleAsync(secondClient, "concurrent-lifecycle-id", linkTokenXid, "concurrent-bank-account"));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(1, await db.BankConnections.CountAsync(
            item => item.ExternalAccountId == "concurrent-bank-account"));
        var integrationEvent = await db.BankIntegrationEvents.SingleAsync(
            item => item.ExternalEventId == "concurrent-lifecycle-id");
        Assert.Equal(BankIntegrationEventStatus.Processed, integrationEvent.ProcessingStatus);
        Assert.Equal(1, integrationEvent.DuplicateDeliveryCount);
        Assert.NotNull(integrationEvent.LastDuplicateReceivedAt);
        Assert.Equal(1, await db.Notifications.CountAsync(
            item => item.UserId == user.User.Id
                && item.Type == SpendWise.Domain.Notifications.NotificationType.BankAccountLinked));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(item => item.UserId == user.User.Id));

        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Ipn_rejects_bad_api_key_transfer_type_and_amount()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendIpnAsync(client, "wrong", "account", "id-1", "credit", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendIpnAsync(client, "test-ipn-key", "account", "id-2", "sideways", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendIpnAsync(client, "test-ipn-key", "account", "id-3", "credit", 0)).StatusCode);
    }

    [Fact]
    public async Task Normal_user_cannot_call_admin_sandbox_simulator()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Normal simulator user");
        UseToken(client, user.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/admin/integrations/sepay/sandbox/transactions",
            new CreateBankSandboxTransactionRequest(Guid.NewGuid(), "credit", 500_000, "Demo"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().BankTransactions);
    }

    private static async Task<HttpResponseMessage> SendIpnAsync(
        HttpClient client,
        string apiKey,
        string bankAccountXid,
        string transactionId,
        string transferType,
        decimal amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/sepay/bankhub/ipn")
        {
            Content = JsonContent.Create(new
            {
                gateway = "TestBank",
                transaction_date = "2026-09-04 08:30:00",
                account_number = "1234567890",
                bank_account_xid = bankAccountXid,
                payment_code = "PAY-1",
                content = "External observation",
                transfer_type = transferType,
                amount,
                reference_code = "REF-1",
                transaction_id = transactionId
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Apikey", apiKey);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendLinkedLifecycleAsync(
        HttpClient client,
        string eventId,
        string linkTokenXid,
        string bankAccountXid)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/integrations/sepay/bankhub/events")
        {
            Content = JsonContent.Create(new
            {
                timestamp = 1788470000,
                xid = eventId,
                version = "1",
                @event = "BANK_ACCOUNT_LINKED",
                metadata = new
                {
                    link_token_xid = linkTokenXid,
                    bank_account_xid = bankAccountXid,
                    brand_name = "Concurrent Test Bank",
                    account_number = "1234567890",
                    account_holder_name = "Concurrent lifecycle owner"
                }
            })
        };
        request.Headers.Add("X-Secret-Key", "test-lifecycle-secret");
        return await client.SendAsync(request);
    }

    private static async Task<BankConnection> AddConnectionAsync(
        SpendWiseDbContext db,
        Guid userId,
        string externalId)
    {
        var connection = new BankConnection
        {
            UserId = userId,
            Provider = BankProvider.SePayBankHub,
            ExternalAccountId = externalId,
            BankBrandName = "Test Bank",
            MaskedAccountNumber = "****7890",
            Status = BankConnectionStatus.Active,
            LinkedAt = DateTime.UtcNow
        };
        db.BankConnections.Add(connection);
        await db.SaveChangesAsync();
        return connection;
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string name)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return await IdentityTestData.RegisterAndSetupPinAsync(client, name);
    }

    private static void UseToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task PromoteToAdminAsync(SpendWiseApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await db.Users.FindAsync(userId);
        user!.SystemRole = SystemRole.SystemAdmin;
        await db.SaveChangesAsync();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
