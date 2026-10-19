using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Transactions;
using SpendWise.Application.Features.Transactions.Commands;
using SpendWise.Application.Finance;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.RecordRecurringOccurrence;

public sealed class RecordRecurringOccurrenceCommandHandler(
    ISpendWiseDbContext db,
    ManualTransactionCreator creator,
    FinancialSecurityGuard security,
    IClock clock) : IRequestHandler<RecordRecurringOccurrenceCommand, TransactionDto>
{
    public async Task<TransactionDto> Handle(RecordRecurringOccurrenceCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        if (string.IsNullOrWhiteSpace(command.Request.IdempotencyKey)
            || command.Request.IdempotencyKey.Length > 100)
        {
            throw new AppException("invalid_idempotency_key", "Idempotency key is required and limited to 100 characters.");
        }

        return await db.ExecuteInTransactionAsync(async token =>
        {
            // Serialize the business occurrence before reading Due/Recorded, not only its wallet balance.
            // InMemory is a single-threaded test provider; relational writes use the existing transaction.
            if (db is not DbContext context || context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            {
                var locked = await db.RecurringOccurrences
                    .Where(item => item.Id == command.OccurrenceId
                        && item.RecurringTransactionId == command.RecurringTransactionId
                        && item.RecurringTransaction.OwnerUserId == command.UserId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UpdatedAt, item => item.UpdatedAt), token);
                if (locked != 1)
                    throw new NotFoundException("recurring_occurrence_not_found", "Recurring occurrence not found.");

                // ExecuteUpdate does not refresh an entity already tracked by an earlier caller.
                if (db is DbContext trackedContext)
                {
                    var tracked = trackedContext.ChangeTracker.Entries<RecurringOccurrence>()
                        .SingleOrDefault(entry => entry.Entity.Id == command.OccurrenceId);
                    if (tracked is not null)
                        await tracked.ReloadAsync(token);
                }
            }

            var occurrence = await db.RecurringOccurrences
                .Include(item => item.RecurringTransaction)
                .SingleOrDefaultAsync(item => item.Id == command.OccurrenceId
                    && item.RecurringTransactionId == command.RecurringTransactionId
                    && item.RecurringTransaction.OwnerUserId == command.UserId,
                    token)
                ?? throw new NotFoundException("recurring_occurrence_not_found", "Recurring occurrence not found.");

            if (occurrence.Status == RecurringOccurrenceStatus.Recorded)
            {
                var existing = await db.FinancialTransactions
                    .AsNoTracking()
                    .Include(item => item.Wallet)
                    .Include(item => item.Category)
                    .Include(item => item.Evidence)
                    .Include(item => item.Corrections).ThenInclude(item => item.Evidence)
                    .SingleAsync(item => item.Id == occurrence.RecordedTransactionId, token);
                var ledgerTransaction = await db.LedgerTransactions.AsNoTracking()
                    .SingleAsync(item => item.Id == existing.LedgerTransactionId, token);
                var suppliedEvidenceIds = (command.Request.EvidenceIds ?? [])
                    .Distinct()
                    .OrderBy(id => id);
                var sameEvidence = existing.Evidence.Select(item => item.Id).OrderBy(id => id)
                    .SequenceEqual(suppliedEvidenceIds);
                if (ledgerTransaction.IdempotencyKey != command.Request.IdempotencyKey.Trim() || !sameEvidence)
                {
                    throw new ConflictException("idempotency_conflict", "Occurrence was recorded with different request details.");
                }
                return TransactionMappings.ToDto(existing);
            }

            var recurring = occurrence.RecurringTransaction;
            var request = new CreateManualTransactionRequest(
                recurring.WalletId,
                recurring.CategoryId,
                recurring.Amount,
                recurring.Description,
                occurrence.ScheduledFor,
                command.Request.IdempotencyKey.Trim(),
                command.Request.SecurityPin,
                command.Request.EvidenceIds);
            var transaction = await creator.CreateAsync(
                command.UserId,
                request,
                recurring.Kind,
                token,
                relatedBusinessId: recurring.Id);

            occurrence.Status = RecurringOccurrenceStatus.Recorded;
            occurrence.RecordedTransactionId = transaction.Id;
            occurrence.RecordedAt = clock.UtcNow;
            recurring.LastRunAt = occurrence.ScheduledFor;
            db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = command.UserId,
                Action = "RECORD_RECURRING_OCCURRENCE",
                TargetType = "RecurringOccurrence",
                TargetId = occurrence.Id.ToString(),
                Reason = "User explicitly recorded recurring payment.",
                MetadataJson = JsonSerializer.Serialize(new
                {
                    transactionId = transaction.Id,
                    recurringTransactionId = recurring.Id
                })
            });
            await db.SaveChangesAsync(token);
            return transaction;
        }, cancellationToken);
    }
}
