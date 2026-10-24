using Microsoft.EntityFrameworkCore;
using MediatR;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankConnections.Commands.CreateBankLink;

public sealed class CreateBankLinkCommandHandler(
    ISpendWiseDbContext db,
    IBankHubGateway gateway,
    IBankHubConfiguration configuration) : IRequestHandler<CreateBankLinkCommand, BankLinkSessionDto>
{
    public async Task<BankLinkSessionDto> Handle(
        CreateBankLinkCommand command,
        CancellationToken cancellationToken)
    {
        // UserId đến từ JWT; request public không có trường UserId để giả mạo chủ sở hữu.
        var hasActiveBank = await db.BankConnections.AnyAsync(b => b.UserId == command.UserId && b.Status == BankConnectionStatus.Active, cancellationToken);
        if (hasActiveBank)
        {
            throw new ConflictException("user_already_has_active_bank", "User already has an active bank connection.");
        }

        var userExists = await db.Users.FindAsync([command.UserId], cancellationToken) is not null;
        if (!userExists)
        {
            throw new NotFoundException("user_not_found", "User not found.");
        }

        configuration.EnsureAvailable();
        var redirectUri = ResolveRedirectUri(configuration, command.Request);
        var language = InputBoundary.RequiredText(command.Request.Language, 10, "Language").ToLowerInvariant();
        if (language is not ("vi" or "en"))
        {
            throw new AppException("invalid_input", "Language must be vi or en.");
        }

        // Chỉ gateway Infrastructure biết URL, credentials và access token của SePay.
        var providerResult = await gateway.CreateLinkAsync(
            new BankProviderLinkRequest(
                BankLinkPurpose.LinkBankAccount,
                redirectUri,
                language,
                ClientPlatform: command.Request.ClientPlatform),
            cancellationToken);

        // Chỉ lưu xid dùng tương quan webhook, không lưu raw link_token.
        var session = new BankLinkSession
        {
            UserId = command.UserId,
            Provider = BankProvider.SePayBankHub,
            Purpose = BankLinkPurpose.LinkBankAccount,
            ExternalLinkTokenXid = providerResult.ExternalLinkTokenXid,
            Status = BankLinkSessionStatus.Pending,
            ExpiresAt = providerResult.ExpiresAt
        };
        db.BankLinkSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return new BankLinkSessionDto(
            session.Id,
            providerResult.HostedLinkUrl,
            session.ExpiresAt,
            "LINK_BANK_ACCOUNT");
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
