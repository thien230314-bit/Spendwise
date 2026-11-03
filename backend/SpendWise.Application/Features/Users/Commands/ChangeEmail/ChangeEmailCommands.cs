using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Users.Commands.ChangeEmail;

// Giai đoạn A: Yêu cầu OTP gửi về email hiện tại
public sealed record RequestCurrentEmailChangeOtpCommand(
    Guid UserId,
    string? IpAddress) : IRequest<EmailVerificationPendingResponse>;

public sealed class RequestCurrentEmailChangeOtpCommandHandler(
    ISpendWiseDbContext db,
    EmailVerificationSupport emailVerification) : IRequestHandler<RequestCurrentEmailChangeOtpCommand, EmailVerificationPendingResponse>
{
    public async Task<EmailVerificationPendingResponse> Handle(
        RequestCurrentEmailChangeOtpCommand command,
        CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new NotFoundException("user_not_found", "User not found.");

        return await emailVerification.IssueCurrentEmailChangeOtpAsync(user, command.IpAddress, ct);
    }
}

// Giai đoạn A: Xác thực OTP email hiện tại và cấp ticket
public sealed record VerifyCurrentEmailOtpRequest(string Otp);

public sealed record VerifyCurrentEmailOtpResponse(string Ticket, DateTime ExpiresAt);

public sealed record VerifyCurrentEmailOtpCommand(
    Guid UserId,
    Guid? SessionId,
    VerifyCurrentEmailOtpRequest Request,
    string? IpAddress) : IRequest<VerifyCurrentEmailOtpResponse>;

public sealed class VerifyCurrentEmailOtpCommandHandler(
    ISpendWiseDbContext db,
    EmailVerificationSupport emailVerification) : IRequestHandler<VerifyCurrentEmailOtpCommand, VerifyCurrentEmailOtpResponse>
{
    public async Task<VerifyCurrentEmailOtpResponse> Handle(
        VerifyCurrentEmailOtpCommand command,
        CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new NotFoundException("user_not_found", "User not found.");

        var (ticket, expiresAt) = await emailVerification.VerifyCurrentEmailChangeOtpAsync(
            user,
            command.SessionId,
            command.Request.Otp,
            command.IpAddress,
            ct);

        return new VerifyCurrentEmailOtpResponse(ticket, expiresAt);
    }
}

// Giai đoạn B: Yêu cầu OTP gửi về email mới (bắt buộc kèm ticket hợp lệ)
public sealed record RequestNewEmailOtpRequest(string Ticket, string NewEmail);

public sealed record RequestNewEmailOtpCommand(
    Guid UserId,
    Guid? SessionId,
    RequestNewEmailOtpRequest Request,
    string? IpAddress) : IRequest<EmailVerificationPendingResponse>;

public sealed class RequestNewEmailOtpCommandHandler(
    ISpendWiseDbContext db,
    EmailVerificationSupport emailVerification) : IRequestHandler<RequestNewEmailOtpCommand, EmailVerificationPendingResponse>
{
    public async Task<EmailVerificationPendingResponse> Handle(
        RequestNewEmailOtpCommand command,
        CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new NotFoundException("user_not_found", "User not found.");

        return await emailVerification.IssueNewEmailChangeOtpAsync(
            user,
            command.SessionId,
            command.Request.Ticket,
            command.Request.NewEmail,
            command.IpAddress,
            ct);
    }
}

// Giai đoạn C: Xác thực OTP email mới và commit cập nhật email (bắt buộc kèm ticket hợp lệ)
public sealed record ConfirmNewEmailOtpRequest(string Ticket, string Otp);

public sealed record ConfirmNewEmailOtpCommand(
    Guid UserId,
    Guid? SessionId,
    ConfirmNewEmailOtpRequest Request,
    string? IpAddress) : IRequest<UserProfile>;

public sealed class ConfirmNewEmailOtpCommandHandler(
    ISpendWiseDbContext db,
    EmailVerificationSupport emailVerification) : IRequestHandler<ConfirmNewEmailOtpCommand, UserProfile>
{
    public async Task<UserProfile> Handle(
        ConfirmNewEmailOtpCommand command,
        CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new NotFoundException("user_not_found", "User not found.");

        return await emailVerification.ConfirmNewEmailChangeOtpAsync(
            user,
            command.SessionId,
            command.Request.Ticket,
            command.Request.Otp,
            command.IpAddress,
            ct);
    }
}

// Hủy yêu cầu đổi email đang thực hiện
public sealed record CancelEmailChangeRequest(string? Ticket);

public sealed record CancelEmailChangeCommand(
    Guid UserId,
    Guid? SessionId,
    string? Ticket,
    string? IpAddress) : IRequest;

public sealed class CancelEmailChangeCommandHandler(
    ISpendWiseDbContext db,
    EmailVerificationSupport emailVerification) : IRequestHandler<CancelEmailChangeCommand>
{
    public async Task Handle(
        CancelEmailChangeCommand command,
        CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new NotFoundException("user_not_found", "User not found.");

        await emailVerification.CancelEmailChangeAsync(
            user,
            command.SessionId,
            command.Ticket,
            ct);
    }
}
