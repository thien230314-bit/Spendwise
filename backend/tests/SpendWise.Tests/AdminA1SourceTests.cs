using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA1SourceTests
{
    [Fact]
    public async Task Aggregate_source_links_resolve_only_case_scoped_records_not_global_customer_details()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Subject"); await f.AddUserAsync("Unrelated");
        var principal = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var service = new AdminA1Service(f.Db, new(f.Db), f.Clock, new(f.Db));
        var aggregate = await service.SummaryAsync(principal, null, default); Assert.Equal(2, aggregate.UsersCreated);
        Assert.Contains("/api/v1/admin/overview/sources/", aggregate.Sources[0].Endpoint);
        Assert.Empty((await service.SourceRecordsAsync(principal, "usersCreated", null, new(), default)).Items);
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id, TicketNumber = "SCOPED" }; f.Db.Add(ticket); await f.Db.SaveChangesAsync();
        var source = await service.SourceRecordsAsync(principal, "usersCreated", null, new(), default);
        Assert.Equal(subject.Id, Assert.Single(source.Items).Id); Assert.Equal(ticket.Id, source.Items[0].CaseId); Assert.Equal(1, source.Total);
    }

    [Fact]
    public async Task Failed_payment_and_bank_signals_require_exact_links_and_ambiguous_bank_ownership_is_not_exposed()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var user = await f.AddUserAsync("Subject"); var other = await f.AddUserAsync("Other");
        var ledger = new LedgerTransaction { Reference = "A1-ERROR-SOURCE", BusinessType = "Synthetic", Status = LedgerTransactionStatus.Failed };
        var linked = new FinancialTransaction { OwnerUserId = user.Id, LedgerTransactionId = ledger.Id, Status = FinancialTransactionStatus.Failed };
        var unlinked = new FinancialTransaction { OwnerUserId = user.Id, LedgerTransactionId = ledger.Id, Status = FinancialTransactionStatus.Failed };
        var bank = new BankIntegrationEvent { ExternalBankAccountId = "synthetic-bank", ProcessingStatus = BankIntegrationEventStatus.Failed, ReceivedAt = f.Clock.UtcNow };
        f.Db.AddRange(ledger, linked, unlinked, bank, new BankIntegrationEvent { ExternalBankAccountId = "ambiguous", ProcessingStatus = BankIntegrationEventStatus.Unmatched, ReceivedAt = f.Clock.UtcNow },
            new BankConnection { UserId = user.Id, ExternalAccountId = "synthetic-bank" }, new BankConnection { UserId = user.Id, ExternalAccountId = "ambiguous" },
            new BankConnection { UserId = other.Id, ExternalAccountId = "ambiguous", AccountEnvironment = SePayAccountEnvironment.TestMode },
            new SupportCase { LinkedUserId = user.Id, LinkedTransactionId = linked.Id, AssignedAdminId = actor.Id, TicketNumber = "CASE-A" },
            new SupportCase { LinkedUserId = user.Id, LinkedTransactionId = linked.Id, AssignedAdminId = actor.Id, TicketNumber = "CASE-B" });
        await f.Db.SaveChangesAsync(); var principal = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var service = new AdminA1Service(f.Db, new(f.Db), f.Clock, new(f.Db));
        var alerts = await service.AlertsAsync(principal, new(), default);
        Assert.Equal(linked.Id, Assert.Single(alerts.Items, a => a.Source == "FinancialTransaction").Id);
        Assert.Equal(bank.Id, Assert.Single(alerts.Items, a => a.Source == "BankIntegrationEvent").Id);
        var posting = await service.SourceRecordsAsync(principal, "postingStatuses", null, new(), default);
        Assert.Equal(ledger.Id, Assert.Single(posting.Items).Id);
        var page1 = await service.AlertsAsync(principal, new(1, 1), default); var page2 = await service.AlertsAsync(principal, new(2, 1), default);
        Assert.Equal(2, page1.Total); Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
    }
}
