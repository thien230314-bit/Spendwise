using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Banking;
using SpendWise.Application.Finance;
using SpendWise.Application.Identity;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Các test đi qua JWT policy và query EF thật để bảo vệ contract quản trị BankTransaction.
public sealed class AdminBankTransactionApiTests
{
    [Fact]
    public async Task Admin_list_is_paged_filterable_newest_first_and_does_not_expose_secrets()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        using var unauthenticatedClient = factory.CreateHttpsClient();
        var firstOwner = await RegisterAsync(client, "Alice Bank Owner");
        var secondOwner = await RegisterAsync(client, "Bob Bank Owner");
        var adminCandidate = await RegisterAsync(client, "Bank Admin");
        await PromoteToAdminAsync(factory, adminCandidate.User.Id);
        var admin = await LoginAsync(client, adminCandidate.User.Email);

        Guid firstConnectionId;
        Guid secondConnectionId;
        var now = DateTime.UtcNow;
        using (var setupScope = factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var firstConnection = Connection(firstOwner.User.Id, "admin-first-account", "Alpha Bank", "****1111");
            var secondConnection = Connection(secondOwner.User.Id, "admin-second-account", "Beta Bank", "****2222");
            db.BankConnections.AddRange(firstConnection, secondConnection);
            await db.SaveChangesAsync();
            firstConnectionId = firstConnection.Id;
            secondConnectionId = secondConnection.Id;
            db.BankTransactions.AddRange(
                Transaction(firstOwner.User.Id, firstConnection.Id, "admin-tx-old", BankTransactionDirection.Credit, now.AddDays(-3), "Old salary", "REF-OLD"),
                Transaction(secondOwner.User.Id, secondConnection.Id, "admin-tx-middle", BankTransactionDirection.Debit, now.AddDays(-2), "Rent payment", "REF-RENT"),
                Transaction(firstOwner.User.Id, firstConnection.Id, "admin-tx-new", BankTransactionDirection.Credit, now.AddDays(-1), "Newest coffee", "REF-COFFEE"));
            await db.SaveChangesAsync();
        }

        // Endpoint bắt buộc JWT SystemAdmin; role không được lấy từ query/body.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthenticatedClient.GetAsync("/api/admin/bank-transactions")).StatusCode);
        UseToken(client, firstOwner.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/admin/bank-transactions")).StatusCode);
        UseToken(client, admin.AccessToken);

        var pageResponse = await client.GetAsync("/api/admin/bank-transactions?page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        var page = (await pageResponse.Content.ReadFromJsonAsync<PagedResult<AdminBankTransactionDto>>())!;
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(new[] { "admin-tx-new", "admin-tx-middle" },
            page.Items.Select(item => item.ExternalTransactionId));
        Assert.Equal("Alice Bank Owner", page.Items[0].UserFullName);
        Assert.Equal(firstOwner.User.SpendId, page.Items[0].UserSpendId);
        Assert.Equal("****1111", page.Items[0].MaskedAccountNumber);

        // Mỗi filter chạy trong SQL và trả đúng totalCount thay vì lọc mảng đã tải.
        Assert.Equal(2, (await GetPageAsync(client,
            $"/api/admin/bank-transactions?userId={firstOwner.User.Id}")).TotalCount);
        Assert.Equal(1, (await GetPageAsync(client,
            $"/api/admin/bank-transactions?bankConnectionId={secondConnectionId}")).TotalCount);
        Assert.Equal(3, (await GetPageAsync(client,
            "/api/admin/bank-transactions?provider=SePayBankHub")).TotalCount);
        Assert.Equal(1, (await GetPageAsync(client,
            "/api/admin/bank-transactions?direction=Debit")).TotalCount);
        Assert.Equal(1, (await GetPageAsync(client,
            $"/api/admin/bank-transactions?from={Uri.EscapeDataString(now.AddDays(-1.5).ToString("O"))}")).TotalCount);
        Assert.Equal(2, (await GetPageAsync(client,
            $"/api/admin/bank-transactions?to={Uri.EscapeDataString(now.AddDays(-1.5).ToString("O"))}")).TotalCount);
        Assert.Equal(2, (await GetPageAsync(client,
            $"/api/admin/bank-transactions?search={Uri.EscapeDataString(firstOwner.User.SpendId)}")).TotalCount);
        Assert.Equal(1, (await GetPageAsync(client,
            "/api/admin/bank-transactions?search=Beta%20Bank")).TotalCount);
        Assert.Equal(1, (await GetPageAsync(client,
            "/api/admin/bank-transactions?search=REF-COFFEE")).TotalCount);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/admin/bank-transactions?provider=UnknownProvider")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/admin/bank-transactions?pageSize=101")).StatusCode);

        var json = await pageResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("test-secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-ipn-key", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-lifecycle-secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"accountNumber\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1234567890", json, StringComparison.Ordinal);
        Assert.NotEqual(Guid.Empty, firstConnectionId);
    }

    [Fact]
    public async Task Admin_detail_returns_external_transaction_while_user_is_forbidden_and_unknown_is_404()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await RegisterAsync(client, "Detail Bank Owner");
        var adminCandidate = await RegisterAsync(client, "Detail Bank Admin");
        await PromoteToAdminAsync(factory, adminCandidate.User.Id);
        var admin = await LoginAsync(client, adminCandidate.User.Email);
        Guid transactionId;
        using (var setupScope = factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var connection = Connection(owner.User.Id, "detail-account", "Detail Bank", "****3333");
            db.BankConnections.Add(connection);
            await db.SaveChangesAsync();
            var transaction = Transaction(
                owner.User.Id,
                connection.Id,
                "detail-external-id",
                BankTransactionDirection.Credit,
                DateTime.UtcNow,
                "Detail transaction",
                "DETAIL-REF");
            db.BankTransactions.Add(transaction);
            await db.SaveChangesAsync();
            transactionId = transaction.Id;
        }

        UseToken(client, owner.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/admin/bank-transactions/{transactionId}")).StatusCode);

        UseToken(client, admin.AccessToken);
        var response = await client.GetAsync($"/api/admin/bank-transactions/{transactionId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transactionDto = (await response.Content.ReadFromJsonAsync<AdminBankTransactionDto>())!;
        Assert.Equal(owner.User.Id, transactionDto.UserId);
        Assert.Equal("detail-external-id", transactionDto.ExternalTransactionId);
        Assert.Equal("****3333", transactionDto.MaskedAccountNumber);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/admin/bank-transactions/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Admin_event_audit_exposes_duplicate_metadata_and_reconciliation_sums_deliveries()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var adminCandidate = await RegisterAsync(client, "Integration Audit Admin");
        await PromoteToAdminAsync(factory, adminCandidate.User.Id);
        var admin = await LoginAsync(client, adminCandidate.User.Email);
        var duplicateAt = DateTime.UtcNow;
        using (var setupScope = factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            db.BankIntegrationEvents.AddRange(
                new BankIntegrationEvent
                {
                    Provider = BankProvider.SePayBankHub,
                    ExternalEventId = "audit-processed",
                    EventType = "BANK_TRANSACTION_IPN",
                    ProcessingStatus = BankIntegrationEventStatus.Processed,
                    DuplicateDeliveryCount = 2,
                    LastDuplicateReceivedAt = duplicateAt,
                    ReceivedAt = duplicateAt.AddMinutes(-2),
                    ProcessedAt = duplicateAt.AddMinutes(-2)
                },
                new BankIntegrationEvent
                {
                    Provider = BankProvider.SePayBankHub,
                    ExternalEventId = "audit-unmatched",
                    EventType = "BANK_ACCOUNT_LINKED",
                    ProcessingStatus = BankIntegrationEventStatus.Unmatched,
                    DuplicateDeliveryCount = 1,
                    LastDuplicateReceivedAt = duplicateAt,
                    ReceivedAt = duplicateAt.AddMinutes(-1),
                    ProcessedAt = duplicateAt.AddMinutes(-1)
                });
            await db.SaveChangesAsync();
        }

        UseToken(client, admin.AccessToken);
        var events = (await client.GetFromJsonAsync<IReadOnlyList<AdminBankIntegrationEventDto>>(
            "/api/admin/bank-integration/events"))!;
        var processed = Assert.Single(events, item => item.ExternalEventId == "audit-processed");
        Assert.Equal("Processed", processed.Status);
        Assert.Equal(2, processed.DuplicateDeliveryCount);
        Assert.NotNull(processed.LastDuplicateReceivedAt);

        var reconciliation = (await client.GetFromJsonAsync<BankReconciliationDto>(
            "/api/admin/bank-integration/reconciliation"))!;
        Assert.Equal(1, reconciliation.ProcessedEvents);
        Assert.Equal(1, reconciliation.UnmatchedEvents);
        Assert.Equal(3, reconciliation.DuplicateDeliveries);
    }

    private static async Task<PagedResult<AdminBankTransactionDto>> GetPageAsync(
        HttpClient client,
        string uri)
    {
        var response = await client.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<AdminBankTransactionDto>>())!;
    }

    private static BankConnection Connection(
        Guid userId,
        string externalAccountId,
        string bank,
        string maskedAccountNumber) => new()
    {
        UserId = userId,
        Provider = BankProvider.SePayBankHub,
        ExternalAccountId = externalAccountId,
        BankBrandName = bank,
        MaskedAccountNumber = maskedAccountNumber,
        Status = BankConnectionStatus.Active,
        LinkedAt = DateTime.UtcNow
    };

    private static BankTransaction Transaction(
        Guid userId,
        Guid connectionId,
        string externalTransactionId,
        BankTransactionDirection direction,
        DateTime transactionDate,
        string content,
        string referenceCode) => new()
    {
        UserId = userId,
        BankConnectionId = connectionId,
        Provider = BankProvider.SePayBankHub,
        ExternalTransactionId = externalTransactionId,
        Direction = direction,
        Amount = 125_000,
        Currency = "VND",
        TransactionDate = transactionDate,
        Content = content,
        ReferenceCode = referenceCode,
        PaymentCode = "SAFE-PAYMENT-CODE",
        Gateway = "TestBank",
        Source = BankTransactionSource.Webhook,
        ImportedAt = transactionDate.AddMinutes(1)
    };

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string name)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return await IdentityTestData.RegisterAndSetupPinAsync(client, name);
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return await IdentityTestData.LoginAndVerifyPinAsync(client, email);
    }

    private static async Task PromoteToAdminAsync(SpendWiseApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == userId);
        user.SystemRole = SystemRole.Admin;
        await db.SaveChangesAsync();
    }

    private static void UseToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
