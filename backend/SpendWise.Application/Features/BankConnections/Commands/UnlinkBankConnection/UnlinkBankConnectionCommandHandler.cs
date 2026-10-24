using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankConnections.Commands.UnlinkBankConnection;

public sealed class UnlinkBankConnectionCommandHandler(
    ISpendWiseDbContext db,
    IBankHubGateway gateway,
    IBankHubConfiguration configuration,
    IClock clock,
    IBankUnlinkSafetyValidator unlinkSafety) : IRequestHandler<UnlinkBankConnectionCommand, BankLinkSessionDto>
{
    public async Task<BankLinkSessionDto> Handle(
        UnlinkBankConnectionCommand command,
        CancellationToken cancellationToken)
    {
        // Lọc theo UserId ngay trong query để không cho hủy liên kết của người khác.
        var connection = await db.BankConnections.SingleOrDefaultAsync(
            item => item.Id == command.BankConnectionId && item.UserId == command.UserId,
            cancellationToken) ?? throw BankingErrors.ConnectionNotFound();
        if (connection.Status is not (BankConnectionStatus.Active or BankConnectionStatus.Inactive))
        {
            throw new ConflictException("bank_connection_not_active", "Bank connection cannot be unlinked in its current status.");
        }

        await unlinkSafety.ValidateCanUnlinkAsync(command.UserId, cancellationToken);

        if (connection.AccountEnvironment == SePayAccountEnvironment.TestMode)
        {
            if (!command.IsDevelopment)
                throw new NotFoundException("sepay_test_mode_unavailable", "SePay Test Mode flow is unavailable.");
            connection.Status = BankConnectionStatus.Unlinked;
            connection.UnlinkedAt = clock.UtcNow;
            var activeWallets = await db.Wallets.Where(w => w.OwnerUserId == command.UserId && w.Status == SpendWise.Domain.Finance.WalletStatus.Active).ToListAsync(cancellationToken);
            foreach (var aw in activeWallets)
            {
                aw.Status = SpendWise.Domain.Finance.WalletStatus.Restricted;
                aw.RestrictionReason = SpendWise.Domain.Finance.WalletRestrictionReason.NoActiveBank;
            }
            var completed = new BankLinkSession
            {
                UserId = command.UserId,
                Provider = BankProvider.SePayBankHub,
                AccountEnvironment = SePayAccountEnvironment.TestMode,
                Purpose = BankLinkPurpose.UnlinkBankAccount,
                ExternalLinkTokenXid = "SWUNLINK" + Guid.NewGuid().ToString("N"),
                BankConnectionId = connection.Id,
                ExternalBankAccountId = connection.ExternalAccountId,
                Status = BankLinkSessionStatus.Completed,
                ExpiresAt = clock.UtcNow,
                CompletedAt = clock.UtcNow
            };
            db.BankLinkSessions.Add(completed);
            await db.SaveChangesAsync(cancellationToken);
            return new BankLinkSessionDto(completed.Id, string.Empty, completed.ExpiresAt, "UNLINK_BANK_ACCOUNT");
        }

        configuration.EnsureAvailable();
        var redirectUri = ResolveRedirectUri(configuration, command.Request);
        var language = InputBoundary.RequiredText(command.Request.Language, 10, "Language").ToLowerInvariant();
        if (language is not ("vi" or "en"))
        {
            throw new AppException("invalid_input", "Language must be vi or en.");
        }
        var providerResult = await gateway.CreateLinkAsync(
            new BankProviderLinkRequest(
                BankLinkPurpose.UnlinkBankAccount,
                redirectUri,
                language,
                connection.ExternalAccountId,
                command.Request.ClientPlatform),
            cancellationToken);

        var session = new BankLinkSession
        {
            UserId = command.UserId,
            Provider = BankProvider.SePayBankHub,
            Purpose = BankLinkPurpose.UnlinkBankAccount,
            ExternalLinkTokenXid = providerResult.ExternalLinkTokenXid,
            BankConnectionId = connection.Id,
            ExternalBankAccountId = connection.ExternalAccountId,
            Status = BankLinkSessionStatus.Pending,
            ExpiresAt = providerResult.ExpiresAt
        };
        db.BankLinkSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return new BankLinkSessionDto(session.Id, providerResult.HostedLinkUrl, session.ExpiresAt, "UNLINK_BANK_ACCOUNT");
    }

    private static string ResolveRedirectUri(
        IBankHubConfiguration configuration,
        CreateBankLinkRequest request) => request.ClientPlatform switch
    {
        BankLinkClientPlatform.Web =>
            configuration.ResolveCompletionRedirectUri(request.CompletionRedirectUri),
        BankLinkClientPlatform.Mobile =>
            configuration.ResolveMobileCompletionRedirectUri(request.CompletionRedirectUri),
        _ => throw new AppException("invalid_input", "Client platform must be Web or Mobile.")
    };
}
