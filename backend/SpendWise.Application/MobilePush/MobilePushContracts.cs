using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.MobilePush;

namespace SpendWise.Application.MobilePush;

public sealed record RegisterMobilePushInstallationRequest(
    string Platform,
    string PushToken,
    string? DeviceName);

public sealed record RegisterMobilePushInstallationCommand(
    Guid UserId,
    Guid InstallationId,
    RegisterMobilePushInstallationRequest Request) : IRequest;

public sealed record UnregisterMobilePushInstallationCommand(
    Guid UserId,
    Guid InstallationId) : IRequest;

public sealed record ExpoPushMessage(
    string PushToken,
    Guid NotificationId,
    string NotificationType);

public enum ExpoPushSendStatus { Submitted, DeviceNotRegistered, PermanentFailure }

public sealed record ExpoPushSendResult(
    ExpoPushSendStatus Status,
    string? ReceiptId = null);

public enum ExpoPushReceiptStatus { Succeeded, DeviceNotRegistered, PermanentFailure }

public sealed record ExpoPushReceiptResult(ExpoPushReceiptStatus Status);

public interface IExpoPushSender
{
    Task<ExpoPushSendResult> SendAsync(
        ExpoPushMessage message,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, ExpoPushReceiptResult>> GetReceiptsAsync(
        IReadOnlyCollection<string> receiptIds,
        CancellationToken cancellationToken);
}

public sealed class ExpoPushTransientException : Exception;
public sealed class ExpoPushReceiptPermanentException : Exception;

public sealed class RegisterMobilePushInstallationCommandHandler(ISpendWiseDbContext db)
    : IRequestHandler<RegisterMobilePushInstallationCommand>
{
    public async Task Handle(
        RegisterMobilePushInstallationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.InstallationId == Guid.Empty)
            throw new AppException("invalid_input", "Installation ID is invalid.");

        var platformText = InputBoundary.RequiredText(command.Request.Platform, 20, "Platform");
        if (!Enum.TryParse<MobilePlatform>(platformText, true, out var platform)
            || !Enum.IsDefined(platform))
        {
            throw new AppException("invalid_input", "Platform must be Android or Ios.");
        }

        var pushToken = InputBoundary.RequiredText(command.Request.PushToken, 512, "Push token");
        if (!IsExpoPushToken(pushToken))
            throw new AppException("invalid_input", "Push token format is invalid.");
        var deviceName = InputBoundary.OptionalText(command.Request.DeviceName, 120, "Device name");

        var tokenInUse = await db.MobilePushInstallations.AsNoTracking().AnyAsync(
            item => item.PushToken == pushToken && item.Id != command.InstallationId,
            cancellationToken);
        if (tokenInUse)
            throw new ConflictException("mobile_push_token_in_use", "Push token is already registered.");

        var installation = await db.MobilePushInstallations.SingleOrDefaultAsync(
            item => item.Id == command.InstallationId,
            cancellationToken);
        if (installation is null)
        {
            installation = new MobilePushInstallation { Id = command.InstallationId };
            db.MobilePushInstallations.Add(installation);
        }
        else if (installation.UserId != command.UserId && installation.IsActive)
        {
            throw new ConflictException(
                "mobile_push_installation_in_use",
                "Mobile installation is already registered.");
        }

        installation.UserId = command.UserId;
        installation.Platform = platform;
        installation.PushToken = pushToken;
        installation.DeviceName = string.IsNullOrEmpty(deviceName) ? null : deviceName;
        installation.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsExpoPushToken(string token)
    {
        var prefixLength = token.StartsWith("ExpoPushToken[", StringComparison.Ordinal)
            ? "ExpoPushToken[".Length
            : token.StartsWith("ExponentPushToken[", StringComparison.Ordinal)
                ? "ExponentPushToken[".Length
                : 0;
        if (prefixLength == 0 || token[^1] != ']' || token.Length <= prefixLength + 1)
            return false;

        return token.AsSpan(prefixLength, token.Length - prefixLength - 1)
            .IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_".AsSpan()) < 0;
    }
}

public sealed class UnregisterMobilePushInstallationCommandHandler(ISpendWiseDbContext db)
    : IRequestHandler<UnregisterMobilePushInstallationCommand>
{
    public async Task Handle(
        UnregisterMobilePushInstallationCommand command,
        CancellationToken cancellationToken)
    {
        var installation = await db.MobilePushInstallations.SingleOrDefaultAsync(
            item => item.Id == command.InstallationId && item.UserId == command.UserId,
            cancellationToken);
        if (installation is null || !installation.IsActive)
            return;

        installation.IsActive = false;
        installation.PushToken = null;
        installation.DeviceName = null;
        await db.MobilePushDeliveries
            .Where(item => item.InstallationId == installation.Id
                && item.Status == MobilePushDeliveryStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, MobilePushDeliveryStatus.Failed)
                    .SetProperty(item => item.LastError, "InstallationUnregistered")
                    .SetProperty(item => item.NextAttemptAt, (DateTime?)null),
                cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
