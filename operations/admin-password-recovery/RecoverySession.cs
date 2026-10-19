using System.Data;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Authentication.Commands.ForgotPassword;
using SpendWise.Application.Features.Authentication.Commands.ResetPassword;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.AdminPasswordRecovery;

public sealed record RecoveryTicket(Guid UserId, Guid TokenId, Guid OutboxId, string DeduplicationKey);

public sealed class RecoverySession(
    SpendWiseDbContext db,
    ForgotPasswordCommandHandler forgot,
    ResetPasswordCommandHandler reset,
    EmailOutboxDispatcher dispatcher,
    IClock clock)
{
    // Local operator configuration; never publish an actual recovery account.
    public static string Email => Environment.GetEnvironmentVariable("SPENDWISE_RECOVERY_EMAIL") ?? "admin@fixture.invalid";

    public async Task<Guid> IdentifyAsync(CancellationToken ct)
    {
        var ids = await db.Users.AsNoTracking()
            .Where(x => x.Email == Email && x.NormalizedEmail == Email.ToUpperInvariant()
                && x.SystemRole == SystemRole.SystemAdmin && x.Status == UserStatus.Active
                && x.EmailVerifiedAt != null)
            .Select(x => x.Id).ToListAsync(ct);
        if (ids.Count != 1) throw new InvalidOperationException("Exact verified Active SystemAdmin not found.");
        return ids[0];
    }

    public async Task<RecoveryTicket> IssueAsync(Guid userId, CancellationToken ct)
    {
        // SERIALIZABLE keeps ownership/email/role stable while existing handlers execute.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.TryAcquireEmailVerificationLockAsync(userId, null, ct)
            || await IdentifyAsync(ct) != userId)
            throw new InvalidOperationException("Recovery account changed or is busy.");
        var before = db.ChangeTracker.Entries<EmailVerificationToken>().Select(x => x.Entity.Id).ToHashSet();
        await forgot.Handle(new ForgotPasswordCommand(new ForgotPasswordRequest(Email), "127.0.0.1"), ct);
        var created = db.ChangeTracker.Entries<EmailVerificationToken>()
            .Select(x => x.Entity).Where(x => !before.Contains(x.Id)
                && x.UserId == userId && x.Purpose == EmailVerificationPurpose.ResetPassword).ToArray();
        if (created.Length != 1)
            throw new InvalidOperationException("No unique OTP was created; nothing will be sent.");
        var token = created[0];
        var key = $"email:password-reset-otp:{token.Id:N}";
        var rows = await db.EmailOutboxMessages.AsNoTracking()
            .Where(x => x.DeduplicationKey == key).ToListAsync(ct);
        if (rows.Count != 1 || rows[0].RecipientEmail != Email
            || rows[0].Status != EmailDeliveryStatus.Pending || rows[0].AttemptCount != 0
            || token.RevokedAt != null || token.UsedAt != null || token.ExpiresAt <= clock.UtcNow)
            throw new InvalidOperationException("New OTP/outbox identity could not be verified.");
        var ticket = new RecoveryTicket(userId, token.Id, rows[0].Id, key);
        await tx.CommitAsync(ct);
        return ticket;
    }

    public async Task<bool> DeliverAsync(RecoveryTicket ticket, CancellationToken ct)
    {
        await VerifyTicketAsync(ticket, ct);
        return await dispatcher.DispatchByIdAsync(ticket.OutboxId, Email, ticket.DeduplicationKey, ct);
    }

    public async Task ResetAsync(RecoveryTicket ticket, string otp, string newPassword, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.TryAcquireEmailVerificationLockAsync(ticket.UserId, null, ct))
            throw new InvalidOperationException("Recovery account is busy.");
        await VerifyTicketAsync(ticket, ct);
        // The existing handler owns validation, OTP consumption, hashing and session revocation.
        try
        {
            await reset.Handle(new ResetPasswordCommand(new ResetPasswordRequest(Email, otp, newPassword)), ct);
        }
        catch (AppException error) when (error.Code == "invalid_password_reset_token")
        {
            // Preserve the handler's committed failed-attempt accounting inside our outer transaction.
            await tx.CommitAsync(ct);
            throw;
        }
        await tx.CommitAsync(ct);
    }

    private async Task VerifyTicketAsync(RecoveryTicket ticket, CancellationToken ct)
    {
        if (await IdentifyAsync(ct) != ticket.UserId
            || ticket.DeduplicationKey != $"email:password-reset-otp:{ticket.TokenId:N}")
            throw new InvalidOperationException("Recovery identity changed.");
        var latest = await db.EmailVerificationTokens.AsNoTracking()
            .Where(x => x.UserId == ticket.UserId && x.Purpose == EmailVerificationPurpose.ResetPassword)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (latest is null || latest.Id != ticket.TokenId || latest.RevokedAt != null
            || latest.UsedAt != null || latest.ExpiresAt <= clock.UtcNow)
            throw new InvalidOperationException("Recovery OTP is no longer current.");
        if (!await db.EmailOutboxMessages.AsNoTracking().AnyAsync(x => x.Id == ticket.OutboxId
                && x.DeduplicationKey == ticket.DeduplicationKey && x.RecipientEmail == Email, ct))
            throw new InvalidOperationException("Recovery outbox identity changed.");
    }
}
