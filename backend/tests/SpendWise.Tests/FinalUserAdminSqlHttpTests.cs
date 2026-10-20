using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Finance;
using SpendWise.Application.Planning;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Notifications;
using SpendWise.Domain.Social;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

[Trait("Category", "SqlServer")]
public sealed class FinalUserAdminSqlHttpTests
{
    private const string Pin = IdentityTestData.DefaultPin;

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Free_refund_HTTP_SQL02_same_clock_rejoin_attributes_refunds_to_membership_episode(bool oldWithdrawal)
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); CoreFundSeed s;
        await using (var db = f.Context()) s = await CoreAcceptanceData.SeedAsync(db, false, ledgerOpening: true);
        await f.ActorAsync(s.Member);
        if (oldWithdrawal)
        {
            using var request = await Send(f, HttpMethod.Post, $"/api/v1/funds/{s.FundId}/requests", new V1FundRequestInput("Withdrawal", 100_000, s.Member.Id, "Synthetic prior withdrawal", "hardening-old-withdrawal", Pin));
            var requestId = (await request.Content.ReadFromJsonAsync<V1FundRequestDto>())!.Id;
            await f.ActorAsync(s.Owner); await Send(f, HttpMethod.Post, $"/api/v1/funds/requests/{requestId}/review", new { Approved = true, Reason = "Synthetic review", SecurityPin = Pin });
            await f.ActorAsync(s.Member);
        }
        await Send(f, HttpMethod.Post, $"/api/v1/funds/{s.FundId}/leave", new V1LeaveFundRequest(Pin, "hardening-leave", s.MemberWallet));
        Guid invitation;
        await using (var db = f.Context())
        {
            var invite = new V1FundInvitation { FundId = s.FundId, InvitedUserId = s.Member.Id, InvitedByUserId = s.Owner.Id, ExpiresAt = f.Clock.UtcNow.AddDays(1), Status = V1InvitationStatus.Pending };
            db.V1FundInvitations.Add(invite); await db.SaveChangesAsync(); invitation = invite.Id;
        }
        await Send(f, HttpMethod.Post, $"/api/v1/funds/invitations/{invitation}/accept");
        await Send(f, HttpMethod.Post, $"/api/v1/funds/{s.FundId}/contributions", new V1ContributeRequest(s.MemberWallet, 100_000, "hardening-fresh", Pin));
        using var preview = await Send(f, HttpMethod.Get, $"/api/v1/funds/{s.FundId}/refund-preview");
        Assert.Equal(100_000, (await preview.Content.ReadFromJsonAsync<V1RefundPreviewDto>())!.EligibleRefundAmountVnd);
        using var leave = await Send(f, HttpMethod.Post, $"/api/v1/funds/{s.FundId}/leave", new V1LeaveFundRequest(Pin, "hardening-second-leave", s.MemberWallet));
        Assert.Equal(100_000, (await leave.Content.ReadFromJsonAsync<V1RefundResultDto>())!.RefundAmountVnd);
        await using var verify = f.Context();
        Assert.Equal(2_000_000, await verify.Wallets.Where(w => w.Id == s.MemberWallet).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Equal(2, await verify.V1FundMemberships.CountAsync(m => m.UserId == s.Member.Id && m.LeftAt != null));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Free_pending_withdrawal_HTTP_SQL02_cannot_debit_a_rejoined_membership(bool legacyRequest)
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); CoreFundSeed s;
        await using (var db = f.Context()) s = await CoreAcceptanceData.SeedAsync(db, false, ledgerOpening: true);
        await f.ActorAsync(s.Member);
        using var pending = await Send(f, HttpMethod.Post, $"/api/v1/funds/{s.FundId}/requests", new V1FundRequestInput("Withdrawal", 100_000, s.Member.Id, "Synthetic pending old episode", "hardening-pending", Pin));
        var id = (await pending.Content.ReadFromJsonAsync<V1FundRequestDto>())!.Id;
        if (legacyRequest) { await using var old = f.Context(); await old.V1FundRequests.Where(r => r.Id == id).ExecuteUpdateAsync(u => u.SetProperty(r => r.RequestedByMembershipId, (Guid?)null)); }
        await Send(f, HttpMethod.Post, $"/api/v1/funds/{s.FundId}/leave", new V1LeaveFundRequest(Pin, "hardening-old-leave", s.MemberWallet));
        Guid invitation;
        await using (var db = f.Context())
        {
            var invite = new V1FundInvitation { FundId = s.FundId, InvitedUserId = s.Member.Id, InvitedByUserId = s.Owner.Id, ExpiresAt = f.Clock.UtcNow.AddDays(1), Status = V1InvitationStatus.Pending };
            db.V1FundInvitations.Add(invite); await db.SaveChangesAsync(); invitation = invite.Id;
        }
        await Send(f, HttpMethod.Post, $"/api/v1/funds/invitations/{invitation}/accept");
        await Send(f, HttpMethod.Post, $"/api/v1/funds/{s.FundId}/contributions", new V1ContributeRequest(s.MemberWallet, 100_000, "hardening-new-episode", Pin));
        await f.ActorAsync(s.Owner); await Send(f, HttpMethod.Post, $"/api/v1/funds/requests/{id}/review", new { Approved = true, Reason = "Synthetic stale review", SecurityPin = Pin }, HttpStatusCode.Conflict);
        await using var verify = f.Context(); Assert.Equal(1_900_000, await verify.Wallets.Where(w => w.Id == s.MemberWallet).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Equal("AwaitingOwnerReview", (await verify.V1FundRequests.SingleAsync()).State); Assert.Equal(6, await verify.LedgerTransactions.CountAsync());
    }

    [Fact]
    public async Task Budgets_HTTP_SQL02_crud_progress_and_foreign_owner_are_consistent()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); var s = await Seed(f); var category = await Category(f);
        await f.ActorAsync(s.Owner);
        using var create = await Send(f, HttpMethod.Post, "/api/budgets", new CreateBudgetRequest(category, f.Clock.UtcNow.Year, f.Clock.UtcNow.Month, 500_000, SecurityPin: Pin), HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<BudgetDto>())!.Id;
        await f.ActorAsync(s.Member);
        await Send(f, HttpMethod.Get, $"/api/budgets/{id}", expected: HttpStatusCode.NotFound);
        await Send(f, HttpMethod.Put, $"/api/budgets/{id}", new UpdateBudgetRequest(700_000, SecurityPin: Pin), HttpStatusCode.NotFound);
        await Send(f, HttpMethod.Delete, $"/api/budgets/{id}", new DeleteBudgetRequest(Pin), HttpStatusCode.NotFound);
        await f.ActorAsync(s.Owner);
        var evidence = await IdentityTestData.UploadEvidenceAsync(f.Client);
        await Send(f, HttpMethod.Post, "/api/transactions/expense", new CreateManualTransactionRequest(s.OwnerWallet, category, 100_000, "Synthetic budget expense", IdempotencyKey: "hardening-budget", SecurityPin: Pin, EvidenceIds: [evidence.Id]), HttpStatusCode.Created);
        using var read = await Send(f, HttpMethod.Get, $"/api/budgets/{id}"); var budget = (await read.Content.ReadFromJsonAsync<BudgetDto>())!;
        Assert.Equal(100_000, budget.SpentAmount); Assert.Equal(400_000, budget.RemainingAmount);
        await Send(f, HttpMethod.Put, $"/api/budgets/{id}", new UpdateBudgetRequest(700_000, SecurityPin: Pin));
        await Send(f, HttpMethod.Delete, $"/api/budgets/{id}", new DeleteBudgetRequest(Pin), HttpStatusCode.NoContent);
        await Send(f, HttpMethod.Get, $"/api/budgets/{id}", expected: HttpStatusCode.NotFound);
        await using var db = f.Context(); Assert.Equal(1_400_000, await db.Wallets.Where(w => w.Id == s.OwnerWallet).Select(w => w.AvailableBalance).SingleAsync());
    }

    [Fact]
    public async Task Goals_HTTP_SQL02_contribute_withdraw_replay_and_cross_account_denial()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); var s = await Seed(f); await f.ActorAsync(s.Owner);
        using var create = await Send(f, HttpMethod.Post, "/api/goals", new CreateGoalRequest("Synthetic savings", 500_000, SecurityPin: Pin));
        var id = (await create.Content.ReadFromJsonAsync<GoalDto>())!.Id;
        var contribution = new GoalMovementRequest(s.OwnerWallet, 100_000, "hardening-goal", Pin);
        for (var i = 0; i < 2; i++) await Send(f, HttpMethod.Post, $"/api/goals/{id}/contributions", contribution);
        await f.ActorAsync(s.Member);
        await Send(f, HttpMethod.Get, $"/api/goals/{id}", expected: HttpStatusCode.NotFound);
        await Send(f, HttpMethod.Post, $"/api/goals/{id}/contributions", contribution, HttpStatusCode.NotFound);
        await Send(f, HttpMethod.Post, $"/api/goals/{id}/withdrawals", new GoalMovementRequest(s.MemberWallet, 50_000, "foreign", Pin), HttpStatusCode.NotFound);
        await f.ActorAsync(s.Owner);
        for (var i = 0; i < 2; i++) await Send(f, HttpMethod.Post, $"/api/goals/{id}/withdrawals", new GoalMovementRequest(s.OwnerWallet, 50_000, "hardening-withdraw", Pin));
        using var read = await Send(f, HttpMethod.Get, $"/api/goals/{id}"); Assert.Equal(50_000, (await read.Content.ReadFromJsonAsync<GoalDto>())!.CurrentAllocatedAmount);
        await using var db = f.Context(); Assert.Equal(1_450_000, await db.Wallets.Where(w => w.Id == s.OwnerWallet).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Equal(6, await db.LedgerTransactions.CountAsync());
    }

    [Fact]
    public async Task Recurring_HTTP_SQL02_due_record_retry_pause_resume_and_ownership()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); var s = await Seed(f); var category = await Category(f); await f.ActorAsync(s.Owner);
        using var create = await Send(f, HttpMethod.Post, "/api/recurring-transactions", new CreateRecurringRequest(s.OwnerWallet, category, "Expense", 20_000, "Synthetic recurring", "Monthly", f.Clock.UtcNow.AddDays(1)));
        var id = (await create.Content.ReadFromJsonAsync<RecurringDto>())!.Id;
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2);
        using (var scope = f.Factory.Services.CreateScope()) { var worker = scope.ServiceProvider.GetRequiredService<IRecurringProcessor>(); await worker.ProcessDueAsync(default); await worker.ProcessDueAsync(default); }
        using var detail = await Send(f, HttpMethod.Get, $"/api/recurring-transactions/{id}"); var occurrence = Assert.Single((await detail.Content.ReadFromJsonAsync<RecurringDto>())!.Occurrences!);
        await using (var noMoney = f.Context()) Assert.Equal(4, await noMoney.LedgerTransactions.CountAsync());
        await f.ActorAsync(s.Member);
        await Send(f, HttpMethod.Get, $"/api/recurring-transactions/{id}", expected: HttpStatusCode.NotFound);
        await Send(f, HttpMethod.Post, $"/api/recurring-transactions/{id}/pause", expected: HttpStatusCode.NotFound);
        await f.ActorAsync(s.Owner); var evidence = await IdentityTestData.UploadEvidenceAsync(f.Client);
        for (var i = 0; i < 2; i++) await Send(f, HttpMethod.Post, $"/api/recurring-transactions/{id}/occurrences/{occurrence.Id}/record", new RecordRecurringOccurrenceRequest(Pin, [evidence.Id], "hardening-recurring"));
        await Send(f, HttpMethod.Post, $"/api/recurring-transactions/{id}/pause"); await Send(f, HttpMethod.Post, $"/api/recurring-transactions/{id}/resume");
        await using var db = f.Context(); Assert.Equal(5, await db.LedgerTransactions.CountAsync()); Assert.Equal(1_480_000, await db.Wallets.Where(w => w.Id == s.OwnerWallet).Select(w => w.AvailableBalance).SingleAsync());
    }

    [Fact]
    public async Task Notifications_HTTP_SQL02_reads_and_mutations_never_touch_foreign_notifications()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); var s = await Seed(f); Guid own, foreign;
        await using (var db = f.Context())
        {
            var a = new Notification { UserId = s.Owner.Id, Title = "Synthetic own", Message = "Safe synthetic content" }; var b = new Notification { UserId = s.Member.Id, Title = "Synthetic foreign", Message = "Foreign synthetic content" };
            db.Notifications.AddRange(a, b); await db.SaveChangesAsync(); own = a.Id; foreign = b.Id;
        }
        await f.ActorAsync(s.Owner);
        using var list = await Send(f, HttpMethod.Get, "/api/notifications"); Assert.DoesNotContain(foreign.ToString(), await list.Content.ReadAsStringAsync());
        await Send(f, HttpMethod.Post, $"/api/notifications/{foreign}/read", expected: HttpStatusCode.NotFound);
        await Send(f, HttpMethod.Delete, $"/api/notifications/{foreign}", expected: HttpStatusCode.NotFound);
        await Send(f, HttpMethod.Post, $"/api/notifications/{own}/read", expected: HttpStatusCode.NoContent);
        await Send(f, HttpMethod.Post, "/api/notifications/read-all", expected: HttpStatusCode.NoContent);
        await Send(f, HttpMethod.Delete, $"/api/notifications/{own}", expected: HttpStatusCode.NoContent);
        await using var verify = f.Context(); Assert.Null((await verify.Notifications.SingleAsync(n => n.Id == foreign)).ReadAt); Assert.False(await verify.Notifications.AnyAsync(n => n.Id == own));
    }

    [Fact]
    public async Task Friends_HTTP_SQL02_allows_hundredth_and_denies_101st_without_accepting_request()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); var s = await Seed(f);
        await using (var db = f.Context())
        {
            for (var i = 0; i < 99; i++)
            {
                var other = CoreAcceptanceHttpTests.SyntheticUser(); var pair = new[] { s.Owner.Id, other.Id }.Order().ToArray();
                var accepted = new FriendRequest { RequesterUserId = s.Owner.Id, AddresseeUserId = other.Id, LowerUserId = pair[0], HigherUserId = pair[1], Status = FriendRequestStatus.Accepted };
                db.AddRange(other, new Friendship { LowerUserId = pair[0], HigherUserId = pair[1], AcceptedRequest = accepted, AcceptedAt = f.Clock.UtcNow });
            }
            await db.SaveChangesAsync();
        }
        await f.ActorAsync(s.Member); using var invite = await Send(f, HttpMethod.Post, "/api/friends/requests", new { Contact = s.Owner.Email });
        var id = (await invite.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        await f.ActorAsync(s.Owner); await Send(f, HttpMethod.Post, $"/api/friends/requests/{id}/accept");
        var extra = CoreAcceptanceHttpTests.SyntheticUser(); await using (var db = f.Context()) { db.Users.Add(extra); await db.SaveChangesAsync(); }
        await f.ActorAsync(extra); using var next = await Send(f, HttpMethod.Post, "/api/friends/requests", new { Contact = s.Owner.Email });
        var nextId = (await next.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        await f.ActorAsync(s.Owner); await Send(f, HttpMethod.Post, $"/api/friends/requests/{nextId}/accept", expected: HttpStatusCode.Conflict);
        await using var verify = f.Context(); Assert.Equal(100, await verify.Friendships.CountAsync(x => x.EndedAt == null)); Assert.Equal(FriendRequestStatus.Pending, (await verify.FriendRequests.SingleAsync(x => x.Id == nextId)).Status);
    }

    [Fact]
    public async Task Transactions_HTTP_SQL02_correction_preserves_history_and_denies_foreign_mutation()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); var s = await Seed(f); var category = await Category(f); await f.ActorAsync(s.Owner);
        var evidence = await IdentityTestData.UploadEvidenceAsync(f.Client);
        using var created = await Send(f, HttpMethod.Post, "/api/transactions/expense", new CreateManualTransactionRequest(s.OwnerWallet, category, 50_000, "Synthetic original expense", IdempotencyKey: "hardening-correction", SecurityPin: Pin, EvidenceIds: [evidence.Id]), HttpStatusCode.Created);
        var original = (await created.Content.ReadFromJsonAsync<TransactionDto>())!;
        var correction = new UpdateTransactionRequest(category, "Synthetic corrected expense", Pin, [], "Synthetic receipt correction", Amount: 40_000);
        await f.ActorAsync(s.Member); await Send(f, HttpMethod.Get, $"/api/transactions/{original.Id}", expected: HttpStatusCode.NotFound);
        await Send(f, HttpMethod.Patch, $"/api/transactions/{original.Id}", correction, HttpStatusCode.NotFound);
        await f.ActorAsync(s.Owner); using var corrected = await Send(f, HttpMethod.Patch, $"/api/transactions/{original.Id}", correction);
        var final = (await corrected.Content.ReadFromJsonAsync<TransactionDto>())!; Assert.Equal(40_000, final.Amount); Assert.Equal(1, final.CorrectionCount); Assert.Single(final.Corrections!);
        await using var db = f.Context(); Assert.Equal(LedgerTransactionStatus.Reversed, (await db.LedgerTransactions.SingleAsync(t => t.Id == original.LedgerTransactionId)).Status);
        Assert.Equal(7, await db.LedgerTransactions.CountAsync()); Assert.Equal(1_460_000, await db.Wallets.Where(w => w.Id == s.OwnerWallet).Select(w => w.AvailableBalance).SingleAsync());
    }

    [Fact]
    public async Task Wallet_HTTP_SQL02_three_wallet_limit_default_ownership_and_internal_retry()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); var s = await Seed(f); await f.ActorAsync(s.Owner); Guid destination = default;
        foreach (var name in new[] { "Synthetic savings", "Synthetic emergency" })
        {
            using var created = await Send(f, HttpMethod.Post, "/api/wallets", new { Name = name, Type = "savings", IsDefault = false }, HttpStatusCode.Created);
            destination = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        }
        await Send(f, HttpMethod.Post, "/api/wallets", new { Name = "Synthetic fourth", Type = "savings", IsDefault = false }, HttpStatusCode.Conflict);
        await Send(f, HttpMethod.Get, $"/api/wallets/{s.MemberWallet}", expected: HttpStatusCode.NotFound);
        for (var i = 0; i < 2; i++) await Send(f, HttpMethod.Post, "/api/transfers/internal", new InternalTransferRequest(s.OwnerWallet, destination, 10_000, "Synthetic reserve", "hardening-internal", Pin), HttpStatusCode.Created);
        await using var db = f.Context(); Assert.Equal(3, await db.Wallets.CountAsync(w => w.OwnerUserId == s.Owner.Id)); Assert.Equal(1, await db.Wallets.CountAsync(w => w.OwnerUserId == s.Owner.Id && w.IsDefault));
        Assert.Equal(1_490_000, await db.Wallets.Where(w => w.Id == s.OwnerWallet).Select(w => w.AvailableBalance).SingleAsync()); Assert.Equal(10_000, await db.Wallets.Where(w => w.Id == destination).Select(w => w.AvailableBalance).SingleAsync()); Assert.Equal(5, await db.LedgerTransactions.CountAsync());
    }

    private static async Task<CoreFundSeed> Seed(Task17SqlHttpFixture f) { await using var db = f.Context(); return await CoreAcceptanceData.SeedAsync(db, ledgerOpening: true); }
    private static async Task<Guid> Category(Task17SqlHttpFixture f) { await using var db = f.Context(); var c = new Category { Name = "Synthetic expense", Key = Guid.NewGuid().ToString("N"), Kind = CategoryKind.Expense, IsSystem = true }; db.Categories.Add(c); await db.SaveChangesAsync(); return c.Id; }
    private static async Task<HttpResponseMessage> Send(Task17SqlHttpFixture f, HttpMethod method, string path, object? body = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        var response = await f.Client.SendAsync(request); Assert.True(response.StatusCode == expected, $"{method} {path}: expected {expected}, got {response.StatusCode}"); return response;
    }
}
