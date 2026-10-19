using System.Net;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace SpendWise.Tests;

// Bảo vệ tài liệu API khỏi thay đổi route ngoài ý muốn.
public sealed class OpenApiContractTests(ITestOutputHelper output)
{
    [Fact]
    public async Task OpenApi_contains_bank_hub_routes_and_removes_sw_bank_routes()
    {
        // Arrange: test host bật OpenAPI nhưng vẫn dùng database SQLite cô lập.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        // Act: đọc tài liệu do endpoint routing thật tạo ra.
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var paths = document.RootElement.GetProperty("paths");
        var operationCount = paths.EnumerateObject()
            .Sum(path => path.Value.EnumerateObject().Count(operation =>
                operation.Name is "get" or "post" or "put" or "patch" or "delete"));

        // Assert: Bank Hub contract mới xuất hiện và toàn bộ SW Bank route cũ biến mất.
        Assert.True(paths.TryGetProperty("/api/auth/forgot-password", out _));
        Assert.True(paths.TryGetProperty("/api/auth/reset-password", out _));
        Assert.True(paths.TryGetProperty("/api/auth/pin/setup", out _));
        Assert.True(paths.TryGetProperty("/api/auth/pin/verify", out _));
        Assert.True(paths.TryGetProperty("/api/security-pin/status", out _));
        Assert.True(paths.TryGetProperty("/api/security-pin/verify", out _));
        Assert.True(paths.TryGetProperty("/api/security-pin", out _));
        Assert.True(paths.TryGetProperty("/api/evidence", out _));
        Assert.True(paths.TryGetProperty("/api/evidence/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/bank-transactions/{id}/category", out _));
        Assert.True(paths.TryGetProperty("/api/recurring-transactions/{id}/pause", out _));
        Assert.True(paths.TryGetProperty("/api/recurring-transactions/{id}/resume", out _));
        Assert.True(paths.TryGetProperty("/api/recurring-transactions/{id}/occurrences/{occurrenceId}/record", out _));
        foreach (var route in new[]
        {
            "/api/bank-connections/link",
            "/api/bank-connections",
            "/api/bank-connections/{id}",
            "/api/bank-connections/{id}/unlink",
            "/api/bank-connections/{id}/reactivate",
            "/api/bank-connections/{id}/sync",
            "/api/bank-connections/test-mode/link-challenges",
            "/api/bank-connections/test-mode/link-challenges/{challengeId}/confirm",
            "/api/bank-connections/test-mode/linked-accounts",
            "/api/bank-connections/test-mode/wallet-top-ups",
            "/api/bank-connections/test-mode/wallet-top-ups/{topUpId}/confirm",
            "/api/bank-transactions",
            "/api/bank-transactions/{id}",
            "/api/admin/bank-connections",
            "/api/admin/bank-connections/{id}",
            "/api/admin/bank-transactions",
            "/api/admin/bank-transactions/{id}",
            "/api/admin/bank-integration/events",
            "/api/admin/bank-integration/reconciliation",
            "/api/admin/integrations/sepay/status",
            "/api/admin/integrations/sepay/sandbox/transactions",
            "/api/integrations/sepay/bankhub/events",
            "/api/integrations/sepay/bankhub/ipn"
        })
        {
            Assert.True(paths.TryGetProperty(route, out _), $"Missing Bank Hub route: {route}");
        }
        foreach (var route in new[]
        {
            "/api/friends/search",
            "/api/friends",
            "/api/friends/requests/incoming",
            "/api/friends/requests/outgoing",
            "/api/transfers/{paymentId}/confirm",
            "/api/groups/{groupId}/receiving-bank",
            "/api/groups/{groupId}/fund/transactions",
            "/api/groups/{groupId}/fund-requests",
            "/api/groups/{groupId}/fund-rounds",
            "/api/groups/{groupId}/activities",
            "/api/groups/{groupId}/deletion-requests"
        })
        {
            Assert.True(paths.TryGetProperty(route, out _), $"Missing social/group route: {route}");
        }
        foreach (var route in new[]
        {
            "/api/sandbox-bank/accounts",
            "/api/sandbox-bank/history",
            "/api/sandbox-bank/payments/{id}/confirm",
            "/api/payments/deposits",
            "/api/payments/deposits/{id}/qr",
            "/api/payments/withdrawals",
            "/api/payments/webhooks/sw-bank",
            "/api/admin/reconciliation/run"
        })
        {
            Assert.False(paths.TryGetProperty(route, out _), $"Obsolete SW Bank route remains: {route}");
        }
        Assert.False(paths.TryGetProperty("/api/money-requests", out _));
        Assert.False(paths.TryGetProperty("/api/groups/{groupId}/debts", out _));
        Assert.False(paths.TryGetProperty("/api/groups/{groupId}/debts/{debtId}/settlements", out _));
        // AI attachment/action endpoints are part of the final backend contract.
        Assert.True(paths.TryGetProperty("/api/transactions/expense/multi-wallet", out _));
        Assert.True(paths.TryGetProperty("/api/admin/ai/conversations", out _));
        Assert.True(paths.TryGetProperty("/api/admin/ai/conversations/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/admin/ai/chat", out _));
        Assert.True(paths.TryGetProperty("/api/admin/ai/reports/export", out _));
        Assert.True(paths.TryGetProperty(
            "/api/mobile/push/installations/{installationId}",
            out var mobilePushInstallation));
        Assert.True(mobilePushInstallation.TryGetProperty("put", out _));
        Assert.True(mobilePushInstallation.TryGetProperty("delete", out _));
        Assert.True(paths.TryGetProperty("/api/auth/sessions", out var authSessions));
        Assert.True(authSessions.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/auth/sessions/{sessionId}", out var authSession));
        Assert.True(authSession.TryGetProperty("delete", out _));
        Assert.True(paths.TryGetProperty("/api/cases/support/{id}/messages/multipart", out var supportMultipart));
        Assert.True(supportMultipart.TryGetProperty("post", out var supportMultipartPost));
        Assert.True(supportMultipartPost.GetProperty("requestBody").GetProperty("content")
            .TryGetProperty("multipart/form-data", out _));
        Assert.True(paths.TryGetProperty("/api/cases/disputes/{id}/messages/multipart", out var disputeMultipart));
        Assert.True(disputeMultipart.TryGetProperty("post", out var disputeMultipartPost));
        Assert.True(disputeMultipartPost.GetProperty("requestBody").GetProperty("content")
            .TryGetProperty("multipart/form-data", out _));
        // SePay Test Mode admin read is part of the current backend contract.
        var v1Routes = new Dictionary<string, string[]>
        {
            ["/api/v1/admin/support/{caseId}/funds/search"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/detail"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/members"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/activity"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/finance/{source}"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/treasury"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/postings/{postingId}"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/governance/{source}"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/cases"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/invitations"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/dissolution"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/reports/{source}/{reportId}/history"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds/{fundId}/expenses/{expenseId}/postings"] = ["post"],
            ["/api/v1/admin/users/search"] = ["post"],
            ["/api/v1/admin/support/{caseId}/users/{userId}/profile"] = ["post"],
            ["/api/v1/admin/support/{caseId}/users/{userId}/sessions"] = ["post"],
            ["/api/v1/admin/support/{caseId}/users/{userId}/restrictions"] = ["post"],
            ["/api/v1/admin/support/{caseId}/users/{userId}/revoke-sessions"] = ["post"],
            ["/api/v1/admin/support/{caseId}/users/{userId}/groups"] = ["post"],
            ["/api/v1/admin/support/{caseId}/users/{userId}/friends"] = ["post"],
            ["/api/v1/admin/support/{caseId}/users/{userId}/history"] = ["post"],
            ["/api/v1/admin/overview"] = ["get"],
            ["/api/v1/admin/overview/sources/{metric}"] = ["get"],
            ["/api/v1/admin/overview/tasks"] = ["get"],
            ["/api/v1/admin/overview/alerts"] = ["get"],
            ["/api/v1/admin/overview/activity"] = ["get"],
            ["/api/v1/activity"] = ["post"],
            ["/api/v1/admin/support/{caseId}/detail"] = ["post"],
            ["/api/v1/admin/support/{caseId}/accounts"] = ["post"],
            ["/api/v1/admin/support/{caseId}/actions"] = ["post"],
            ["/api/v1/funds/{id}/owner-response"] = ["get"],
            ["/api/v1/funds/{id}/owner-response/escalate"] = ["post"],
            ["/api/v1/funds/{id}/activity/close"] = ["post"],
            ["/api/v1/funds/{id}/activity-proposals"] = ["post"],
            ["/api/v1/funds/activity-proposals/{id}"] = ["get"],
            ["/api/v1/funds/activity-proposals/{id}/vote"] = ["post"],
            ["/api/v1/funds/{id}/requests"] = ["get", "post"],
            ["/api/v1/funds/requests/{id}/review"] = ["post"],
            ["/api/v1/funds/requests/{id}/cancel"] = ["post"],
            ["/api/v1/system/maintenance/emergency"] = ["post"],
            ["/api/v1/system/maintenance/{id}"] = ["put"],
            ["/api/v1/system/maintenance/{id}/reminders"] = ["post"],
            ["/api/v1/system/maintenance/{id}/extend"] = ["post"],
            ["/api/v1/system/maintenance/{id}/review"] = ["post"],
            ["/api/v1/system/broadcasts"] = ["post"],
            ["/api/v1/system/broadcasts/{id}/publish"] = ["post"],
            ["/api/v1/system/broadcasts/{id}/cancel"] = ["post"],
            ["/api/v1/system/broadcasts/{id}/status"] = ["post"],
            ["/api/v1/admin/support/{caseId}/user-profile"] = ["post"],
            ["/api/v1/admin/operators/{userId}"] = ["put"],
            ["/api/v1/funds"] = ["get", "post"],
            ["/api/v1/funds/{id}"] = ["get"],
            ["/api/v1/funds/{id}/members"] = ["get"],
            ["/api/v1/funds/{id}/activities"] = ["get"],
            ["/api/v1/funds/{id}/contribution-allocation"] = ["get"],
            ["/api/v1/funds/{id}/finance"] = ["get"],
            ["/api/v1/funds/{id}/governance"] = ["get"],
            ["/api/v1/funds/{id}/elections"] = ["post"],
            ["/api/v1/funds/elections/{id}"] = ["get"],
            ["/api/v1/funds/elections/{id}/candidates"] = ["post"],
            ["/api/v1/funds/elections/{id}/ballots"] = ["post"],
            ["/api/v1/funds/elections/{id}/advance"] = ["post"],
            ["/api/v1/funds/elections/{id}/accept"] = ["post"],
            ["/api/v1/system/maintenance"] = ["get", "post"],
            ["/api/v1/system/maintenance/{id}/cancel"] = ["post"],
            ["/api/v1/system/maintenance/{id}/close"] = ["post"],
            ["/api/v1/admin/support/{caseId}/funds"] = ["post"],
            ["/api/v1/admin/operators/search"] = ["post"],
            ["/api/v1/admin/support/search"] = ["post"],
            ["/api/v1/admin/support/{caseId}/assignment"] = ["put"],
            ["/api/v1/funds/{id}/obligations/confirm"] = ["post"],
            ["/api/v1/funds/{id}/contributions"] = ["get", "post"],
            ["/api/v1/funds/{id}/invitation-proposals"] = ["get"],
            ["/api/v1/funds/invitations/{id}/review"] = ["post"],
            ["/api/v1/funds/invitations/{id}/decline"] = ["post"],
            ["/api/v1/funds/invitations/{id}"] = ["delete"],
            ["/api/v1/admin/support/{caseId}/transaction"] = ["post"],
            ["/api/v1/funds/{id}/invitations"] = ["post"],
            ["/api/v1/funds/invitations"] = ["get"],
            ["/api/v1/funds/invitations/{id}/accept"] = ["post"],
            ["/api/v1/funds/{id}/members/{userId}/spending-permission"] = ["put"],
            ["/api/v1/funds/{id}/ownership-offers"] = ["post"],
            ["/api/v1/funds/ownership-offers"] = ["get"],
            ["/api/v1/funds/ownership-offers/{id}/accept"] = ["post"]
        };
        foreach (var route in v1Routes)
        {
            Assert.True(paths.TryGetProperty(route.Key, out var path));
            foreach (var method in route.Value) Assert.True(path.TryGetProperty(method, out _));
        }
        Assert.Equal(179 + v1Routes.Count, paths.EnumerateObject().Count());
        Assert.Equal(217 + v1Routes.Sum(p => p.Value.Length), operationCount);
        output.WriteLine("OpenAPI paths={0}; operations={1}", paths.EnumerateObject().Count(), operationCount);
    }

    [Fact]
    public async Task Admin_audit_and_transaction_operations_reference_named_schemas()
    {
        // Arrange: lấy tài liệu OpenAPI từ routing và chữ ký Controller thật.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        using var document = JsonDocument.Parse(await client.GetStreamAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        // Act: đọc riêng schema response của hai endpoint quản trị từng trả object.
        var auditOperation = paths
            .GetProperty("/api/admin/audit-logs")
            .GetProperty("get")
            .GetRawText();
        var transactionOperation = paths
            .GetProperty("/api/admin/transactions/search")
            .GetProperty("post")
            .GetRawText();

        // Assert: OpenAPI liên kết DTO có tên để client generator tạo model mạnh kiểu.
        Assert.Contains("#/components/schemas/AuditLogDto", auditOperation, StringComparison.Ordinal);
        Assert.Contains("#/components/schemas/AdminTransactionDto", transactionOperation, StringComparison.Ordinal);
        Assert.True(document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .TryGetProperty("AuditLogDto", out _));
        Assert.True(document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .TryGetProperty("AdminTransactionDto", out _));
    }
}
