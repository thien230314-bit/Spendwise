using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.AI;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Queries.GetSessions;
using SpendWise.Application.Finance;
using SpendWise.Application.Identity;
using SpendWise.Application.Notifications;
using SpendWise.Application.Planning;
using SpendWise.Domain.AI;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class FocusedParityVerificationTests
{
    // =========================================================================
    // 1. PIN reset validation với token hợp lệ và không hợp lệ
    // =========================================================================
    [Fact]
    public async Task Pin_reset_validation_with_valid_and_invalid_tokens()
    {
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?> { ["Email:WebsiteUrl"] = "http://localhost:3000" });
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "Pin Validation Test User");
        var email = auth.User.Email;

        // Yêu cầu quên PIN
        var forgotRes = await client.PostAsJsonAsync("/api/auth/pin/forgot", new ForgotSecurityPinRequest(email));
        Assert.Equal(HttpStatusCode.Accepted, forgotRes.StatusCode);

        // Lấy rawToken hợp lệ từ database
        string validToken;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var emailMsg = await db.EmailOutboxMessages.FirstAsync(m => m.RecipientEmail == email && m.Subject.Contains("PIN"));
            var plain = scope.ServiceProvider.GetRequiredService<IEmailContentProtector>().Unprotect(emailMsg.TextBody);
            var match = System.Text.RegularExpressions.Regex.Match(plain, @"http://localhost:3000/reset-pin\?token=([^\s]+)");
            Assert.True(match.Success);
            validToken = Uri.UnescapeDataString(match.Groups[1].Value);
        }

        // Test 1.1: Token hợp lệ -> 200 OK với valid: true
        var validRes = await client.PostAsJsonAsync("/api/auth/pin/reset/validate",
            new ValidateSecurityPinResetTokenRequest(validToken));
        validRes.EnsureSuccessStatusCode();
        var validStatus = (await validRes.Content.ReadFromJsonAsync<SecurityPinResetTokenStatusDto>())!;
        Assert.True(validStatus.Valid);
        Assert.True(validStatus.ExpiresAt > DateTime.UtcNow);

        // Test 1.2: Token không hợp lệ -> 200 OK với valid: false
        var invalidRes = await client.PostAsJsonAsync("/api/auth/pin/reset/validate",
            new ValidateSecurityPinResetTokenRequest("invalid-token-arbitrary-12345"));
        invalidRes.EnsureSuccessStatusCode();
        var invalidStatus = (await invalidRes.Content.ReadFromJsonAsync<SecurityPinResetTokenStatusDto>())!;
        Assert.False(invalidStatus.Valid);
    }

    // =========================================================================
    // 2. AI Action Execute: kiểm tra persistence và hành vi execute lặp lại
    // =========================================================================
    [Fact]
    public async Task Ai_action_execute_persists_executed_state_and_rejects_repeated_execution()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "AI Action Test User");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        Guid proposalId = Guid.NewGuid();
        Guid budgetId = Guid.NewGuid();

        // Seed an existing budget and a pending budget_delete proposal
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == auth.User.Email);
            var category = await db.Categories.FirstAsync(c => c.Kind == SpendWise.Domain.Finance.CategoryKind.Expense);

            var budget = new SpendWise.Domain.Planning.Budget
            {
                Id = budgetId,
                OwnerUserId = user.Id,
                CategoryId = category.Id,
                Year = 2026,
                Month = 12,
                LimitAmount = 5_000_000m,
                AlertsEnabled = false,
                Rollover = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Budgets.Add(budget);
            
            var proposal = new AiActionProposal
            {
                Id = proposalId,
                OwnerUserId = user.Id,
                ActionType = "budget_delete",
                Status = AiActionProposalStatus.Pending,
                SafeSummary = "Delete obsolete test budget",
                PayloadJson = JsonSerializer.Serialize(new { budget_id = budgetId.ToString() }),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.AiActionProposals.Add(proposal);
            await db.SaveChangesAsync();
        }

        // Lần 1: Execute thành công
        var executeRes = await client.PostAsJsonAsync($"/api/ai/actions/{proposalId}/execute",
            new ExecuteAiActionRequest("123456", ExplicitConfirmation: true));
        executeRes.EnsureSuccessStatusCode();
        var actionDto = (await executeRes.Content.ReadFromJsonAsync<AiActionDto>())!;
        Assert.Equal("Executed", actionDto.Status);

        // Kiểm tra Persistence trong cơ sở dữ liệu
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var savedInDb = await db.AiActionProposals.AsNoTracking().SingleAsync(x => x.Id == proposalId);
            Assert.Equal(AiActionProposalStatus.Executed, savedInDb.Status);
            Assert.NotNull(savedInDb.ExecutedAt);
        }

        // Lần 2: Execute lặp lại -> ném 409 Conflict (ai_action_not_pending)
        var repeatRes = await client.PostAsJsonAsync($"/api/ai/actions/{proposalId}/execute",
            new ExecuteAiActionRequest("123456", ExplicitConfirmation: true));
        Assert.Equal(HttpStatusCode.Conflict, repeatRes.StatusCode);
    }

    // =========================================================================
    // 3. Security PIN Change: PIN mới hợp lệ, PIN cũ bị từ chối
    // =========================================================================
    [Fact]
    public async Task Security_pin_change_validates_new_pin_and_rejects_old_pin()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "PIN Change User");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Ban đầu PIN là 123456 -> Verify OK (204)
        var initVerify = await client.PostAsJsonAsync("/api/security-pin/verify", new VerifySecurityPinRequest("123456"));
        Assert.Equal(HttpStatusCode.NoContent, initVerify.StatusCode);

        // Đổi PIN sang 888888
        var changeRes = await client.PutAsJsonAsync("/api/security-pin",
            new ChangeSecurityPinRequest("123456", "888888", "888888"));
        Assert.Equal(HttpStatusCode.NoContent, changeRes.StatusCode);

        // PIN mới 888888 hợp lệ -> Verify OK (204)
        var verifyNew = await client.PostAsJsonAsync("/api/security-pin/verify", new VerifySecurityPinRequest("888888"));
        Assert.Equal(HttpStatusCode.NoContent, verifyNew.StatusCode);

        // PIN cũ 123456 bị từ chối -> 401 Unauthorized
        var verifyOld = await client.PostAsJsonAsync("/api/security-pin/verify", new VerifySecurityPinRequest("123456"));
        Assert.Equal(HttpStatusCode.Unauthorized, verifyOld.StatusCode);
    }

    // =========================================================================
    // 4. Session Revoke: phiên đã thu hồi không sử dụng lại được
    // =========================================================================
    [Fact]
    public async Task Session_revoke_invalidates_session_and_refresh_fails()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "Session Revoke User");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Lấy danh sách session hiện tại
        var sessionsRes = await client.GetFromJsonAsync<IReadOnlyList<SessionDto>>("/api/auth/sessions");
        Assert.NotNull(sessionsRes);
        Assert.NotEmpty(sessionsRes);

        // Thu hồi toàn bộ session qua DELETE /api/users/me/sessions
        var revokeRes = await client.DeleteAsync("/api/users/me/sessions");
        Assert.Equal(HttpStatusCode.NoContent, revokeRes.StatusCode);

        // Thử refresh token với refresh token cũ -> Bị từ chối (401 Unauthorized)
        var refreshRes = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken, "Test Device"));
        Assert.True(refreshRes.StatusCode == HttpStatusCode.Unauthorized || refreshRes.StatusCode == HttpStatusCode.Forbidden);
    }

    // =========================================================================
    // 5. Các mutation HTTP 204 quan trọng: kiểm tra hậu điều kiện (Post-Condition)
    // =========================================================================
    [Fact]
    public async Task Http_204_mutations_enforce_verifiable_post_conditions()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "204 PostCondition User");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // 5.1. Xóa mục tiêu tiết kiệm (DELETE /api/goals/{id} -> 204)
        var createGoalRes = await client.PostAsJsonAsync("/api/goals",
            new CreateGoalRequest("Test Goal", 5000000, DateTime.UtcNow.AddMonths(3), SecurityPin: "123456"));
        createGoalRes.EnsureSuccessStatusCode();
        var createdGoal = (await createGoalRes.Content.ReadFromJsonAsync<GoalDto>())!;

        var deleteGoalRes = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/goals/{createdGoal.Id}")
        {
            Content = JsonContent.Create(new DeleteGoalRequest("123456"))
        });
        Assert.Equal(HttpStatusCode.NoContent, deleteGoalRes.StatusCode);

        // Hậu điều kiện: GET /api/goals/{id} trả về 404 NotFound
        var getGoalAfterDelete = await client.GetAsync($"/api/goals/{createdGoal.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getGoalAfterDelete.StatusCode);

        // 5.2. Đánh dấu tất cả thông báo đã đọc (POST /api/notifications/read-all -> 204)
        var markReadRes = await client.PostAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.NoContent, markReadRes.StatusCode);

        // Hậu điều kiện: GET /api/notifications?unreadOnly=true trả về mảng rỗng
        var unreadList = await client.GetFromJsonAsync<IReadOnlyList<NotificationDto>>("/api/notifications?unreadOnly=true");
        Assert.NotNull(unreadList);
        Assert.Empty(unreadList);
    }

    // =========================================================================
    // 6. Nghiệp vụ tiền và Ledger: bảo toàn số dư thực tế
    // =========================================================================
    [Fact]
    public async Task Financial_internal_transfer_conserves_ledger_balances()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "Ledger User");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Lấy danh sách ví ban đầu
        var wallets = await client.GetFromJsonAsync<IReadOnlyList<WalletDto>>("/api/wallets");
        Assert.NotNull(wallets);
        Assert.NotEmpty(wallets);
        var mainWallet = wallets.First();

        // Nạp tiền ban đầu vào ví chính trong Db
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var w = await db.Wallets.Include(x => x.LedgerAccount).SingleAsync(x => x.Id == mainWallet.Id);
            w.AvailableBalance = 1_000_000m;
            w.LedgerAccount.Balance = 1_000_000m;
            await db.SaveChangesAsync();
        }

        // Tạo ví phụ thứ 2
        var createW2Res = await client.PostAsJsonAsync("/api/wallets",
            new CreateWalletRequest("Savings Wallet", "savings", false));
        createW2Res.EnsureSuccessStatusCode();
        var wallet2 = (await createW2Res.Content.ReadFromJsonAsync<WalletDto>())!;

        // Chuyển 300,000 VND từ ví 1 sang ví 2
        var transferRes = await client.PostAsJsonAsync("/api/transfers/internal",
            new InternalTransferRequest(
                mainWallet.Id, wallet2.Id, 300_000m, "Transfer 300k", Guid.NewGuid().ToString("N"), "123456"));
        transferRes.EnsureSuccessStatusCode();

        // Kiểm tra số dư ví thực tế sau chuyển tiền:
        // Ví 1: 1,000,000 - 300,000 = 700,000
        // Ví 2: 0 + 300,000 = 300,000
        // Tổng số dư bảo toàn: 700,000 + 300,000 = 1,000,000
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var w1After = await db.Wallets.AsNoTracking().SingleAsync(x => x.Id == mainWallet.Id);
            var w2After = await db.Wallets.AsNoTracking().SingleAsync(x => x.Id == wallet2.Id);

            Assert.Equal(700_000m, w1After.AvailableBalance);
            Assert.Equal(300_000m, w2After.AvailableBalance);
            Assert.Equal(1_000_000m, w1After.AvailableBalance + w2After.AvailableBalance);
        }
    }
}
