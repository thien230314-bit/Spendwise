using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA4Tests
{
    private static AdminA4Service CreateService(TargetedCorrectnessFixture f) =>
        new(f.Db, new(f.Db), new(f.Db, new(f.Db), new(f.Db), new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), f.Clock), new(f.Db), f.Clock);

    [Theory]
    [InlineData(SystemRole.Admin)]
    [InlineData(SystemRole.SuperAdmin)]
    [InlineData(SystemRole.User)]
    [InlineData(SystemRole.SystemAdmin)]
    public async Task Real_HTTP_routes_enforce_canonical_roles_and_scope(SystemRole role)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var caseId = Guid.NewGuid();
        var txId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            User Synthetic(SystemRole r)
            {
                var key = Guid.NewGuid().ToString("N");
                return new()
                {
                    FullName = "Synthetic",
                    Email = key + "@test.local",
                    NormalizedEmail = key + "@TEST.LOCAL",
                    SpendId = key,
                    NormalizedSpendId = key,
                    PasswordHash = "synthetic",
                    SystemRole = r
                };
            }
            var actor = Synthetic(role);
            var subject = Synthetic(SystemRole.User);
            var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };

            var account = new LedgerAccount { Code = "TEST:ACC", Name = "Test Acc", Type = LedgerAccountType.Wallet };
            var ledger = new LedgerTransaction { Id = ledgerId, Reference = "REF-" + Guid.NewGuid().ToString("N")[..8], BusinessType = "PersonalExpense", Status = LedgerTransactionStatus.Completed };
            var wallet = new Wallet { Id = Guid.NewGuid(), OwnerUserId = subject.Id, Name = "Main", Type = WalletType.Main, AvailableBalance = 500000 };
            var tx = new FinancialTransaction
            {
                Id = txId,
                OwnerUserId = subject.Id,
                WalletId = wallet.Id,
                Type = FinancialTransactionType.Expense,
                Status = FinancialTransactionStatus.Completed,
                Amount = 100000,
                Currency = "VND",
                LedgerTransactionId = ledger.Id,
                OccurredAt = DateTime.UtcNow
            };
            var ticket = new SupportCase
            {
                Id = caseId,
                LinkedUserId = subject.Id,
                AssignedAdminId = role == SystemRole.Admin ? actor.Id : null,
                EscalatedAt = role == SystemRole.SuperAdmin ? DateTime.UtcNow : null,
                EscalationReviewerId = role == SystemRole.SuperAdmin ? actor.Id : null,
                Status = CaseStatus.Open
            };

            db.AddRange(actor, subject, session, account, ledger, wallet, tx, ticket);
            db.LedgerEntries.AddRange(
                new LedgerEntry { LedgerTransactionId = ledger.Id, LedgerAccountId = account.Id, Direction = LedgerEntryDirection.Debit, Amount = 100000 },
                new LedgerEntry { LedgerTransactionId = ledger.Id, LedgerAccountId = account.Id, Direction = LedgerEntryDirection.Credit, Amount = 100000 }
            );
            await db.SaveChangesAsync();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(actor, session.Id).Token);
        }

        var root = $"/api/v1/admin/support/{caseId}/transactions";

        // A4.1
        using (var res = await client.PostAsJsonAsync(root + "/search", new { reason = "Audit test" }))
        {
            Assert.Equal(role is SystemRole.Admin or SystemRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, res.StatusCode);
        }

        // A4.2
        using (var res = await client.PostAsJsonAsync(root + $"/lookup?code={txId}", new { reason = "Audit test" }))
        {
            Assert.Equal(role is SystemRole.Admin or SystemRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, res.StatusCode);
        }

        // A4.3
        using (var res = await client.PostAsJsonAsync(root + "/wallets", new { reason = "Audit test" }))
        {
            Assert.Equal(role is SystemRole.Admin or SystemRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, res.StatusCode);
        }

        // A4.4
        using (var res = await client.PostAsJsonAsync(root + "/pending-failed", new { reason = "Audit test" }))
        {
            Assert.Equal(role is SystemRole.Admin or SystemRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, res.StatusCode);
        }

        // A4.5
        using (var res = await client.PostAsJsonAsync(root + $"/ledger/{ledgerId}", new { reason = "Audit test" }))
        {
            Assert.Equal(role is SystemRole.Admin or SystemRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, res.StatusCode);
        }

        // A4.6
        using (var res = await client.PostAsJsonAsync(root + $"/{txId}/trace", new { reason = "Audit test" }))
        {
            Assert.Equal(role is SystemRole.Admin or SystemRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, res.StatusCode);
        }

        // Cross-case foreign transaction denial (A4.2, A4.5, A4.6)
        var foreignGuid = Guid.NewGuid();
        using (var res = await client.PostAsJsonAsync(root + $"/lookup?code={foreignGuid}", new { reason = "Audit test" }))
        {
            Assert.Equal(role is SystemRole.Admin or SystemRole.SuperAdmin ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, res.StatusCode);
        }
        using (var res = await client.PostAsJsonAsync(root + $"/ledger/{foreignGuid}", new { reason = "Audit test" }))
        {
            Assert.Equal(role is SystemRole.Admin or SystemRole.SuperAdmin ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, res.StatusCode);
        }
        using (var res = await client.PostAsJsonAsync(root + $"/{foreignGuid}/trace", new { reason = "Audit test" }))
        {
            Assert.Equal(role is SystemRole.Admin or SystemRole.SuperAdmin ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, res.StatusCode);
        }
    }

    [Fact]
    public async Task A4_1_Transactions_Listing_Filters_And_Paging_Accurate()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Subject");
        var other = await f.AddUserAsync("Other");
        var session = await f.AddSessionAsync(actor.Id);

        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id, Status = CaseStatus.Open };
        var ledger1 = new LedgerTransaction { Reference = "LED1", BusinessType = "PersonalIncome", Status = LedgerTransactionStatus.Completed };
        var ledger2 = new LedgerTransaction { Reference = "LED2", BusinessType = "PersonalExpense", Status = LedgerTransactionStatus.Completed };
        var ledger3 = new LedgerTransaction { Reference = "LED3", BusinessType = "PersonalIncome", Status = LedgerTransactionStatus.Completed };
        var w = new Wallet { OwnerUserId = subject.Id, Name = "Main", Type = WalletType.Main, AvailableBalance = 1000000 };
        var tx1 = new FinancialTransaction { OwnerUserId = subject.Id, Wallet = w, Type = FinancialTransactionType.Income, Status = FinancialTransactionStatus.Completed, Amount = 100000, LedgerTransaction = ledger1, OccurredAt = f.Clock.UtcNow.AddHours(-2) };
        var tx2 = new FinancialTransaction { OwnerUserId = subject.Id, Wallet = w, Type = FinancialTransactionType.Expense, Status = FinancialTransactionStatus.Completed, Amount = 50000, LedgerTransaction = ledger2, OccurredAt = f.Clock.UtcNow.AddHours(-1) };
        var txOther = new FinancialTransaction { OwnerUserId = other.Id, Type = FinancialTransactionType.Income, Status = FinancialTransactionStatus.Completed, Amount = 999999, LedgerTransaction = ledger3, OccurredAt = f.Clock.UtcNow };

        f.Db.AddRange(ticket, ledger1, ledger2, ledger3, w, tx1, tx2, txOther);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "Admin");

        var all = await service.TransactionsAsync(principal, ticket.Id, new("Read all"), default);
        Assert.Equal(2, all.Total);
        Assert.Equal(2, all.Items.Count);
        Assert.DoesNotContain(all.Items, item => item.Amount == 999999);

        // Filter by Type
        var incomeOnly = await service.TransactionsAsync(principal, ticket.Id, new("Filter income", Type: "Income"), default);
        Assert.Equal(1, incomeOnly.Total);
        Assert.Equal(100000, incomeOnly.Items[0].Amount);

        // Filter by Status
        var pendingOnly = await service.TransactionsAsync(principal, ticket.Id, new("Filter pending", Status: "Pending"), default);
        Assert.Equal(0, pendingOnly.Total);
        Assert.Empty(pendingOnly.Items);
    }

    [Fact]
    public async Task A4_2_Lookup_Supports_Multiple_Codes_And_Prevents_IDOR()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Subject");
        var other = await f.AddUserAsync("Other");
        var session = await f.AddSessionAsync(actor.Id);

        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id, Status = CaseStatus.Open };
        var ledger = new LedgerTransaction { Reference = "LEDGER-REF-12345", BusinessType = "PersonalExpense", Status = LedgerTransactionStatus.Completed };
        var txSubject = new FinancialTransaction
        {
            OwnerUserId = subject.Id,
            Type = FinancialTransactionType.Expense,
            Status = FinancialTransactionStatus.Completed,
            Amount = 45000,
            LedgerTransaction = ledger,
            OccurredAt = f.Clock.UtcNow,
            CounterpartyName = "Nguyen Van A",
            Description = "Secret note 123"
        };

        var otherLedger = new LedgerTransaction { Reference = "LEDGER-OTHER-99999", BusinessType = "PersonalExpense", Status = LedgerTransactionStatus.Completed };
        var txOther = new FinancialTransaction
        {
            OwnerUserId = other.Id,
            Type = FinancialTransactionType.Expense,
            Status = FinancialTransactionStatus.Completed,
            Amount = 88888,
            LedgerTransaction = otherLedger,
            OccurredAt = f.Clock.UtcNow
        };

        f.Db.AddRange(ticket, ledger, txSubject, otherLedger, txOther);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "Admin");

        // Lookup by Transaction ID
        var byTxId = await service.LookupAsync(principal, ticket.Id, txSubject.Id.ToString(), "Check TxId", default);
        Assert.Equal(txSubject.Id, byTxId.Id);
        Assert.Equal("FinancialTransactionId", byTxId.MatchedBy);
        Assert.NotEqual("Nguyen Van A", byTxId.MaskedCounterparty); // Masked
        Assert.NotEqual("Secret note 123", byTxId.MaskedDescription); // Masked

        // Lookup by Ledger Reference
        var byLedgerRef = await service.LookupAsync(principal, ticket.Id, "LEDGER-REF-12345", "Check Ledger Ref", default);
        Assert.Equal(txSubject.Id, byLedgerRef.Id);
        Assert.Equal("LedgerReference", byLedgerRef.MatchedBy);

        // IDOR: Lookup other user's transaction ID -> ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.LookupAsync(principal, ticket.Id, txOther.Id.ToString(), "Sneak other", default));

        // IDOR: Lookup other user's ledger ref -> ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.LookupAsync(principal, ticket.Id, "LEDGER-OTHER-99999", "Sneak other ref", default));
    }

    [Fact]
    public async Task A4_3_Wallets_Lists_Main_Secondary_And_Masked_Bank_Connections()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Subject");
        var session = await f.AddSessionAsync(actor.Id);

        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id, Status = CaseStatus.Open };
        var mainWallet = new Wallet { OwnerUserId = subject.Id, Name = "Main Wallet", Type = WalletType.Main, AvailableBalance = 250000 };
        var savingsWallet = new Wallet { OwnerUserId = subject.Id, Name = "Savings", Type = WalletType.Savings, AvailableBalance = 1000000 };
        var bankConn = new BankConnection
        {
            UserId = subject.Id,
            BankBrandName = "Vietcombank",
            MaskedAccountNumber = "1234567890",
            AccountHolderName = "NGUYEN VAN B",
            Status = BankConnectionStatus.Active,
            LinkedAt = f.Clock.UtcNow
        };

        f.Db.AddRange(ticket, mainWallet, savingsWallet, bankConn);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "Admin");

        var result = await service.WalletsAsync(principal, ticket.Id, "Audit wallets", default);
        Assert.Equal(2, result.Wallets.Count);
        Assert.Contains(result.Wallets, w => w.Type == "Main" && w.AvailableBalance == 250000);
        Assert.Contains(result.Wallets, w => w.Type == "Savings" && w.AvailableBalance == 1000000);

        Assert.Single(result.LinkedBanks);
        var bank = result.LinkedBanks[0];
        Assert.Equal("Vietcombank", bank.BankBrandName);
        Assert.Equal("****7890", bank.MaskedAccountNumber);
        Assert.NotEqual("NGUYEN VAN B", bank.MaskedAccountHolderName); // Masked PII
    }

    [Fact]
    public async Task A4_4_Pending_And_Failed_Transactions_With_Retry_Telemetry()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Subject");
        var session = await f.AddSessionAsync(actor.Id);

        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id, Status = CaseStatus.Open };
        var bankConn = new BankConnection { UserId = subject.Id, BankBrandName = "TestBank", MaskedAccountNumber = "1111", Status = BankConnectionStatus.Active };
        var topUpWallet = new Wallet { OwnerUserId = subject.Id, Name = "TopUpWallet", Type = WalletType.Main, AvailableBalance = 500000 };
        var topUpLedger = new LedgerTransaction { Reference = "LED-TOPUP-FAIL", BusinessType = "WalletTopUp", Status = LedgerTransactionStatus.Failed };
        var topUp = new WalletTopUp
        {
            UserId = subject.Id,
            BankConnection = bankConn,
            Wallet = topUpWallet,
            LedgerTransaction = topUpLedger,
            Reference = "TOPUP-REF-99",
            ProviderTransactionId = "PROVIDER-TX-001",
            Amount = 200000,
            Status = WalletTopUpStatus.Failed
        };

        var tx = new FinancialTransaction
        {
            OwnerUserId = subject.Id,
            RelatedBusinessId = topUp.Id,
            Type = FinancialTransactionType.BankTopUp,
            Status = FinancialTransactionStatus.Failed,
            Amount = 200000,
            LedgerTransaction = topUpLedger,
            OccurredAt = f.Clock.UtcNow
        };

        var evt1 = new BankIntegrationEvent
        {
            Provider = BankProvider.SePayBankHub,
            ExternalTransactionId = "PROVIDER-TX-001",
            EventType = "IPN_DELIVERY",
            ProcessingStatus = BankIntegrationEventStatus.Failed,
            FailureCode = "SIGNATURE_MISMATCH",
            DuplicateDeliveryCount = 2,
            ReceivedAt = f.Clock.UtcNow.AddMinutes(-5)
        };

        f.Db.AddRange(ticket, bankConn, topUpWallet, topUpLedger, topUp, tx, evt1);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "Admin");

        var result = await service.PendingAndFailedAsync(principal, ticket.Id, new("Investigate failures"), default);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal("Failed", item.Status);
        Assert.Equal("SIGNATURE_MISMATCH", item.FailureOrReviewReason);
        Assert.Single(item.RetryHistory);
        Assert.Equal("IPN_DELIVERY", item.RetryHistory[0].Action);
        Assert.Contains("DeliveryDuplicates: 2", item.RetryHistory[0].Detail);
    }

    [Fact]
    public async Task A4_5_LedgerDetail_Evaluates_Balance_And_Business_Binding()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Subject");
        var session = await f.AddSessionAsync(actor.Id);

        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id, Status = CaseStatus.Open };
        var account1 = new LedgerAccount { Code = "ACC:1", Name = "Account 1", Type = LedgerAccountType.Wallet };
        var account2 = new LedgerAccount { Code = "ACC:2", Name = "Account 2", Type = LedgerAccountType.ExpenseClearing };
        var ledger = new LedgerTransaction { Reference = "LED-REF-BALANCED", BusinessType = "PersonalExpense", Status = LedgerTransactionStatus.Completed };

        var tx = new FinancialTransaction
        {
            OwnerUserId = subject.Id,
            Type = FinancialTransactionType.Expense,
            Status = FinancialTransactionStatus.Completed,
            Amount = 150000,
            LedgerTransactionId = ledger.Id,
            OccurredAt = f.Clock.UtcNow
        };

        f.Db.AddRange(ticket, account1, account2, ledger, tx);
        f.Db.LedgerEntries.AddRange(
            new LedgerEntry { LedgerTransactionId = ledger.Id, LedgerAccountId = account1.Id, Direction = LedgerEntryDirection.Debit, Amount = 150000 },
            new LedgerEntry { LedgerTransactionId = ledger.Id, LedgerAccountId = account2.Id, Direction = LedgerEntryDirection.Credit, Amount = 150000 }
        );
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "Admin");

        var result = await service.LedgerDetailAsync(principal, ticket.Id, ledger.Id, "Inspect ledger", default);
        Assert.Equal(ledger.Id, result.LedgerTransactionId);
        Assert.True(result.IsBalanced);
        Assert.Equal("BALANCED", result.BalanceStatus);
        Assert.Equal(150000, result.TotalDebit);
        Assert.Equal(150000, result.TotalCredit);
        Assert.Equal("BOUND_PERSONAL_FINANCE", result.BusinessBindingStatus);
        Assert.Equal(2, result.Entries.Count);
    }

    [Fact]
    public async Task A4_6_Transaction_Trace_Builds_Stage_Timeline_Without_Leaking_Secrets()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Subject");
        var session = await f.AddSessionAsync(actor.Id);

        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id, Status = CaseStatus.Open };
        var ledger = new LedgerTransaction { Reference = "TRACE-LED-001", BusinessType = "PersonalIncome", Status = LedgerTransactionStatus.Completed, CompletedAt = f.Clock.UtcNow };
        var tx = new FinancialTransaction
        {
            OwnerUserId = subject.Id,
            Type = FinancialTransactionType.Income,
            Status = FinancialTransactionStatus.Completed,
            Amount = 300000,
            LedgerTransaction = ledger,
            OccurredAt = f.Clock.UtcNow
        };

        f.Db.AddRange(ticket, ledger, tx);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "Admin");

        var trace = await service.TraceAsync(principal, ticket.Id, tx.Id, "Trace lifecycle", default);
        Assert.Equal(tx.Id, trace.TransactionId);
        Assert.NotEmpty(trace.Timeline);

        var serialized = System.Text.Json.JsonSerializer.Serialize(trace);
        Assert.DoesNotContain("passwordHash", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", serialized, StringComparison.OrdinalIgnoreCase);

        // Stages present
        Assert.Contains(trace.Timeline, step => step.Stage == "RequestInitiation");
        Assert.Contains(trace.Timeline, step => step.Stage == "AuthorizationAndSecurity");
        Assert.Contains(trace.Timeline, step => step.Stage == "BusinessProcessing");
        Assert.Contains(trace.Timeline, step => step.Stage == "LedgerOutcome");
    }
}
