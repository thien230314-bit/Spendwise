using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication.Commands.ChangePassword;
using SpendWise.Application.Features.Authentication.Commands.ForgotPassword;
using SpendWise.Application.Features.Authentication.Commands.Login;
using SpendWise.Application.Features.Authentication.Commands.Logout;
using SpendWise.Application.Features.Authentication.Commands.RefreshToken;
using SpendWise.Application.Features.Authentication.Commands.RevokeSession;
using SpendWise.Application.Features.Authentication.Commands.Register;
using SpendWise.Application.Features.Authentication.Commands.ResetPassword;
using SpendWise.Application.Features.Authentication.Commands.RequestSecurityPinReset;
using SpendWise.Application.Features.Authentication.Commands.ResetSecurityPin;
using SpendWise.Application.Features.Authentication.Commands.SetupSecurityPin;
using SpendWise.Application.Features.Authentication.Commands.CompleteSecurityPinLogin;
using SpendWise.Application.Features.Authentication.Commands.VerifyRegistrationEmail;
using SpendWise.Application.Features.Authentication.Commands.ResendRegistrationEmail;
using SpendWise.Application.Features.Authentication.Queries.ValidateSecurityPinResetToken;
using SpendWise.Application.Features.Authentication.Queries.GetSessions;
using SpendWise.Application.Identity;
using SpendWise.API.Infrastructure;

namespace SpendWise.API.Controllers;

// Nhận thông tin xác thực và để Application xử lý mật khẩu, token cùng phiên đăng nhập.

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<EmailVerificationPendingResponse> Register(
        RegisterRequest request,
        CancellationToken ct)
    {
        // Gửi thông tin đăng ký cùng IP và User-Agent để tạo tài khoản an toàn.
        var command = new RegisterCommand(
            request,
            Ip(),
            Request.Headers.UserAgent.ToString());

        return await mediator.Send(command, ct);
    }

    [HttpPost("email-verification/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("emailVerification")]
    public Task<LoginChallengeResponse> VerifyRegistrationEmail(
        VerifyRegistrationEmailRequest request,
        CancellationToken ct) =>
        mediator.Send(new VerifyRegistrationEmailCommand(
            request,
            Ip(),
            Request.Headers.UserAgent.ToString()), ct);

    [HttpPost("email-verification/resend")]
    [AllowAnonymous]
    [EnableRateLimiting("emailVerification")]
    public Task<EmailVerificationPendingResponse> ResendRegistrationEmail(
        ResendRegistrationEmailRequest request,
        CancellationToken ct) =>
        mediator.Send(new ResendRegistrationEmailCommand(request, Ip()), ct);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<LoginChallengeResponse> Login(
        LoginRequest request,
        CancellationToken ct)
    {
        // Gửi thông tin đăng nhập cùng metadata thiết bị để phát hành token.
        var command = new LoginCommand(
            request,
            Ip(),
            Request.Headers.UserAgent.ToString());

        return await mediator.Send(command, ct);
    }

    [HttpPost("pin/setup")]
    [AllowAnonymous]
    public async Task<AuthResponse> SetupPin(
        SetupSecurityPinRequest request,
        CancellationToken ct)
    {
        // Challenge thiết lập PIN chỉ được dùng một lần trước khi cấp session đầu tiên.
        return await mediator.Send(new SetupSecurityPinCommand(request), ct);
    }

    [HttpPost("pin/verify")]
    [AllowAnonymous]
    public async Task<AuthResponse> VerifyPinForLogin(
        CompleteSecurityPinLoginRequest request,
        CancellationToken ct)
    {
        // Password challenge phải vượt qua PIN trước khi nhận access/refresh token.
        return await mediator.Send(new CompleteSecurityPinLoginCommand(request), ct);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<AuthResponse> Refresh(
        RefreshRequest request,
        CancellationToken ct)
    {
        // Gửi refresh token cùng metadata thiết bị để thực hiện token rotation.
        var command = new RefreshTokenCommand(
            request,
            Ip(),
            Request.Headers.UserAgent.ToString());

        return await mediator.Send(command, ct);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        RefreshRequest? request,
        CancellationToken ct)
    {
        // Lấy UserId từ access token và gửi yêu cầu thu hồi refresh token.
        var userId = RequiredUser();
        var sessionId = RequiredSession();
        var command = new LogoutCommand(userId, sessionId, request?.RefreshToken);
        await mediator.Send(command, ct);

        return NoContent();
    }

    [HttpGet("sessions")]
    [Authorize(Policy = "User")]
    public Task<IReadOnlyList<SessionDto>> GetSessions(CancellationToken ct) =>
        mediator.Send(new GetSessionsQuery(RequiredUser(), RequiredSession()), ct);

    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> RevokeSession(
        Guid sessionId,
        CancellationToken ct)
    {
        await mediator.Send(new RevokeSessionCommand(RequiredUser(), sessionId), ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken ct)
    {
        // Lấy UserId từ access token rồi gửi Command đổi mật khẩu.
        var userId = RequiredUser();
        var command = new ChangePasswordCommand(userId, request);
        await mediator.Send(command, ct);

        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("emailVerification")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken ct)
    {
        // Handler tạo token khi phù hợp; response luôn trung lập chống account enumeration.
        try
        {
            await mediator.Send(new ForgotPasswordCommand(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString()), ct);
        }
        catch (SpendWise.Application.Common.Exceptions.AppException exception)
            when (exception.Code is "password_reset_cooldown" or "password_reset_rate_limited")
        {
            // Quota nội bộ không tiết lộ account; HTTP limiter theo IP vẫn trả 429 trước action.
        }
        return Accepted(new
        {
            message = ApiErrorLocalizer.Localize(
                "password_reset_instruction",
                "If an account matches the information provided, password reset instructions will be sent to the registered email.")
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("emailVerification")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken ct)
    {
        // OTP RESET_PASSWORD chỉ được dùng một lần để đổi password và thu hồi credential cũ.
        await mediator.Send(new ResetPasswordCommand(request), ct);
        return NoContent();
    }

    [HttpPost("pin/forgot")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotSecurityPin(
        ForgotSecurityPinRequest request,
        CancellationToken ct)
    {
        await mediator.Send(new RequestSecurityPinResetCommand(request), ct);
        return Accepted(new
        {
            message = ApiErrorLocalizer.Localize(
                "pin_reset_instruction",
                "If an account matches the information provided, PIN reset instructions will be sent to the registered email.")
        });
    }

    [HttpPost("pin/reset/validate")]
    [AllowAnonymous]
    public Task<SecurityPinResetTokenStatusDto> ValidateSecurityPinReset(
        ValidateSecurityPinResetTokenRequest request,
        CancellationToken ct) =>
        mediator.Send(new ValidateSecurityPinResetTokenQuery(request), ct);

    [HttpPost("pin/reset")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetSecurityPin(
        ResetSecurityPinRequest request,
        CancellationToken ct)
    {
        await mediator.Send(new ResetSecurityPinCommand(request), ct);
        return NoContent();
    }

    private Guid RequiredUser()
    {
        // Endpoint yêu cầu đăng nhập nên thiếu UserId trong access token phải trả Unauthorized.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }

    private string? Ip()
    {
        // Lấy địa chỉ IP của client để ghi nhận vào phiên và token.
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private Guid RequiredSession()
    {
        // sid đã được JWT middleware xác thực trước khi endpoint Logout chạy.
        return Guid.TryParse(currentUser.SessionId, out var sessionId)
            ? sessionId
            : throw new UnauthorizedAccessException();
    }
}
