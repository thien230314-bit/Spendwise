using Microsoft.EntityFrameworkCore;
using SpendWise.Domain.Banking;
using Xunit;

namespace SpendWise.Tests;

// SQLite relational tests xác nhận unique index thật, không dựa vào EF InMemory.
public sealed class BankHubRelationalConstraintTests
{
    [Fact]
    public async Task Provider_external_account_is_unique()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var first = await fixture.AddUserAsync("First connection user");
        var second = await fixture.AddUserAsync("Second connection user");
        fixture.Db.BankConnections.AddRange(
            Connection(first.Id, "same-account"),
            Connection(second.Id, "same-account"));
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Provider_link_token_xid_is_unique()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Link session user");
        fixture.Db.BankLinkSessions.AddRange(
            Session(user.Id, "same-link"),
            Session(user.Id, "same-link"));
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Provider_transaction_id_is_unique()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Transaction user");
        var connection = Connection(user.Id, "transaction-account");
        fixture.Db.BankConnections.Add(connection);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.BankTransactions.AddRange(
            Transaction(user.Id, connection.Id, "same-transaction"),
            Transaction(user.Id, connection.Id, "same-transaction"));
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Provider_event_xid_is_unique_when_present()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        fixture.Db.BankIntegrationEvents.AddRange(
            Event("same-event"),
            Event("same-event"));
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    private static BankConnection Connection(Guid userId, string externalId) => new()
    {
        UserId = userId,
        Provider = BankProvider.SePayBankHub,
        ExternalAccountId = externalId,
        BankBrandName = "Test Bank",
        MaskedAccountNumber = "****1234",
        Status = BankConnectionStatus.Active
    };

    private static BankLinkSession Session(Guid userId, string xid) => new()
    {
        UserId = userId,
        Provider = BankProvider.SePayBankHub,
        Purpose = BankLinkPurpose.LinkBankAccount,
        ExternalLinkTokenXid = xid,
        Status = BankLinkSessionStatus.Pending,
        ExpiresAt = DateTime.UtcNow.AddMinutes(10)
    };

    private static BankTransaction Transaction(Guid userId, Guid connectionId, string externalId) => new()
    {
        UserId = userId,
        BankConnectionId = connectionId,
        Provider = BankProvider.SePayBankHub,
        ExternalTransactionId = externalId,
        Direction = BankTransactionDirection.Credit,
        Amount = 1,
        Currency = "VND",
        TransactionDate = DateTime.UtcNow,
        Content = string.Empty,
        Source = BankTransactionSource.Webhook,
        ImportedAt = DateTime.UtcNow
    };

    private static BankIntegrationEvent Event(string xid) => new()
    {
        Provider = BankProvider.SePayBankHub,
        ExternalEventId = xid,
        EventType = "TEST",
        ProcessingStatus = BankIntegrationEventStatus.Processed,
        ReceivedAt = DateTime.UtcNow
    };
}
