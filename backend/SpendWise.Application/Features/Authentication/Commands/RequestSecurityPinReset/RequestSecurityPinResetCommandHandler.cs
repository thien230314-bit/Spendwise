using MediatR;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

using SpendWise.Application.Common;

using SpendWise.Application.Common.Interfaces;

using SpendWise.Application.Identity;

using SpendWise.Domain.Identity;



namespace SpendWise.Application.Features.Authentication.Commands.RequestSecurityPinReset;



public sealed class RequestSecurityPinResetCommandHandler(

    ISpendWiseDbContext db,

    SecurityPinResetSupport resets,

    IEmailSender emailSender,

    ILogger<RequestSecurityPinResetCommandHandler> logger,

    IEmailVerificationPolicy? policy = null,

    IClock? clock = null)

    : IRequestHandler<RequestSecurityPinResetCommand>

{

    public async Task Handle(

        RequestSecurityPinResetCommand command,

        CancellationToken cancellationToken)

    {

        // Mọi identifier sai hoặc không tồn tại đều nhận response trung lập.

        var identifier = command.Request.Identifier?.Trim();

        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 256)

        {

            return;

        }



        User? user = null;

        if (InputBoundary.IsValidEmail(identifier))

        {

            var normalizedEmail = identifier.ToUpperInvariant();

            user = await db.Users.AsNoTracking().SingleOrDefaultAsync(

                item => item.NormalizedEmail == normalizedEmail

                    && (item.Status == UserStatus.Active || item.Status == UserStatus.Restricted) && item.EmailVerifiedAt != null,

                cancellationToken);

        }

        else if (InputBoundary.TryNormalizePhone(identifier, out var normalizedPhone))

        {

            user = await db.Users.AsNoTracking().SingleOrDefaultAsync(

                item => item.Phone == normalizedPhone

                    && (item.Status == UserStatus.Active || item.Status == UserStatus.Restricted) && item.EmailVerifiedAt != null,

                cancellationToken);

        }



        if (user is null)

        {

            return;

        }



        try

        {

            await db.ExecuteInTransactionAsync(async ct =>

            {

                if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, null, ct)) return false;

                var current = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == user.Id

                    && (x.Status == UserStatus.Active || x.Status == UserStatus.Restricted) && x.EmailVerifiedAt != null, ct);

                if (current is null) return false;

                var now = clock?.UtcNow ?? DateTime.UtcNow;

                var hourStart = now.AddHours(-1);

                var recent = await db.SecurityPinResetTokens.AsNoTracking()

                    .Where(x => x.UserId == user.Id && x.Purpose == SecurityPinResetPurpose.SelfService && x.CreatedAt >= hourStart)

                    .OrderByDescending(x => x.CreatedAt).Select(x => x.CreatedAt).ToListAsync(ct);

                if (recent.Count >= (policy?.MaximumSendsPerHourPerAccount ?? 5)

                    || (recent.Count > 0 && recent[0].Add(policy?.ResendCooldown ?? TimeSpan.FromSeconds(60)) > now)) return false;

                var (rawToken, resetToken) = await resets.CreateAsync(current, SecurityPinResetPurpose.SelfService, null, ct);

                // Scoped queued sender shares this transaction; SMTP happens only after commit.

                await emailSender.SendSecurityPinResetAsync(current.Email, rawToken, resetToken.ExpiresAt, ct);

                return true;

            }, cancellationToken);

        }

        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }

        catch (Exception)

        {

            logger.LogWarning("Security PIN recovery queue operation failed; no sensitive exception details logged.");

        }

    }

}

