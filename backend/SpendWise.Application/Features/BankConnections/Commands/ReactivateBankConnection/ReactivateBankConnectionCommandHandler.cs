using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankConnections.Commands.ReactivateBankConnection;

public sealed class ReactivateBankConnectionCommandHandler(
    ISpendWiseDbContext db,
    IBankHubGateway gateway,
    IBankHubConfiguration configuration) : IRequestHandler<ReactivateBankConnectionCommand, BankLinkSessionDto>
{
    public async Task<BankLinkSessionDto> Handle(
        ReactivateBankConnectionCommand command,
        CancellationToken cancellationToken)
    {
        var connection = await db.BankConnections.SingleOrDefaultAsync(
            item => item.Id == command.BankConnectionId && item.UserId == command.UserId,
            cancellationToken) ?? throw BankingErrors.ConnectionNotFound();
        // Connection đang Active hoặc đã Unlinked không được dùng nhầm luồng reactivate.
        if (connection.Status != BankConnectionStatus.Inactive)
        {
            throw new ConflictException("bank_connection_not_inactive", "Only an inactive bank connection can be reactivated.");
        }
        if (connection.AccountEnvironment == SePayAccountEnvironment.TestMode)
            throw new ConflictException("bank_reactivation_not_supported", "Start a new Test Mode link challenge to reactivate this account.");

        configuration.EnsureAvailable();
        var language = InputBoundary.RequiredText(command.Request.Language, 10, "Language").ToLowerInvariant();
        if (language is not ("vi" or "en"))
        {
            throw new AppException("invalid_input", "Language must be vi or en.");
        }
        var providerResult = await gateway.CreateLinkAsync(
            new BankProviderLinkRequest(
                BankLinkPurpose.ReactivateBankAccount,
                ResolveRedirectUri(configuration, command.Request),
                language,
                connection.ExternalAccountId,
                command.Request.ClientPlatform),
            cancellationToken);

        var session = new BankLinkSession
        {
            UserId = command.UserId,
            Provider = BankProvider.SePayBankHub,
            Purpose = BankLinkPurpose.ReactivateBankAccount,
            ExternalLinkTokenXid = providerResult.ExternalLinkTokenXid,
            BankConnectionId = connection.Id,
            ExternalBankAccountId = connection.ExternalAccountId,
            Status = BankLinkSessionStatus.Pending,
            ExpiresAt = providerResult.ExpiresAt
        };
        db.BankLinkSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return new BankLinkSessionDto(session.Id, providerResult.HostedLinkUrl, session.ExpiresAt, "REACTIVATE_BANK_ACCOUNT");
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
