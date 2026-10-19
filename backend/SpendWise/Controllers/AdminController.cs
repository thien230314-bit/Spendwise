using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using SpendWise.Application.Admin;
using SpendWise.Application.AccountRecovery;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin.Audit.Queries.GetAuditLogs;
using SpendWise.Application.Features.Admin.Cases.Commands.CreateDispute;
using SpendWise.Application.Features.Admin.Cases.Commands.CreateSupportCase;
using SpendWise.API.Contracts;
using SpendWise.Application.Features.Admin.Cases.Commands.UpdateAdminCase;
using SpendWise.Application.Features.Admin.Cases.Queries.GetAdminCases;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Features.Admin.Configuration.Commands.UpdateConfiguration;
using SpendWise.Application.Features.Admin.Configuration.Queries.GetConfigurations;
using SpendWise.Application.Features.Admin.Dashboard.Queries.GetAdminDashboard;
using SpendWise.Application.Features.Admin.FeatureFlags.Commands.UpdateFeatureFlag;
using SpendWise.Application.Features.Admin.FeatureFlags.Queries.GetFeatureFlags;
using SpendWise.Application.Features.Admin.Health.Queries.GetAdminHealth;
using SpendWise.Application.Features.Admin.Ledger.Commands.SearchAdminLedger;
using SpendWise.Application.Features.Admin.Transactions.Commands.SearchAdminTransactions;
using SpendWise.Application.Features.Admin.Users.Commands.ChangeUserStatus;
using SpendWise.Application.Features.Admin.Users.Commands.RevokeUserSessions;
using SpendWise.Application.Features.Admin.Users.Queries.SearchAdminUsers;
using SpendWise.Application.Features.Admin.Users.Commands.ResetUserPassword;
using SpendWise.Application.Features.Admin.Users.Commands.ResetUserSecurityPin;
using SpendWise.Application.Features.AccountRecovery.Commands.ReviewAccountRecovery;
using SpendWise.Application.Features.AccountRecovery.Queries.GetAccountRecoveryRequestById;
using SpendWise.Application.Features.AccountRecovery.Queries.GetAccountRecoveryRequests;
using SpendWise.Application.Features.Admin.Wallets.Commands.FreezeWallet;
using Microsoft.AspNetCore.RateLimiting;

namespace SpendWise.API.Controllers;

// Cung cấp API giám sát và workflow quản trị, không biến dữ liệu tài chính thành CRUD.

[ApiController]
[Authorize(Policy = "SystemAdmin")]
[Route("api/admin")]
public sealed class AdminController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    // Legacy A2 routes are closed by the legacy + V1 policy conjunction; use the case-scoped AdminA2Controller instead.
    [HttpGet("users")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    public async Task<IReadOnlyList<AdminUserDto>> Users(
        [FromQuery] string? search,
        CancellationToken ct)
    {
        // Gửi bộ lọc tìm người dùng sang Application để phân trang.
        return await mediator.Send(new SearchAdminUsersQuery(search), ct);
    }

    [HttpPut("users/{id:guid}/status")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    [EnableRateLimiting("auth")]
    public async Task<AdminUserDto> UserStatus(
        Guid id,
        ChangeUserStatusRequest request,
        CancellationToken ct)
    {
        // Lấy AdminId từ token rồi gửi Command đổi trạng thái tài khoản.
        var adminId = AdminId();

        return await mediator.Send(
            new ChangeUserStatusCommand(adminId, id, request),
            ct);
    }

    [HttpPost("users/{id:guid}/reset-password")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    [EnableRateLimiting("auth")]
    public Task<AdminTemporaryPasswordResponse> ResetPassword(
        Guid id,
        AdminCredentialResetRequest request,
        CancellationToken ct) =>
        mediator.Send(new ResetUserPasswordCommand(AdminId(), id, request), ct);

    [HttpPost("users/{id:guid}/reset-pin")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    [EnableRateLimiting("auth")]
    public Task<AdminSecurityPinResetResponse> ResetPin(
        Guid id,
        AdminCredentialResetRequest request,
        CancellationToken ct) =>
        mediator.Send(new ResetUserSecurityPinCommand(AdminId(), id, request), ct);

    [HttpGet("account-recovery")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    [EnableRateLimiting("auth")]
    public Task<IReadOnlyList<AccountRecoveryDto>> AccountRecovery(
        [FromQuery] AdminAccountRecoveryFilter filter,
        CancellationToken ct) =>
        mediator.Send(new GetAccountRecoveryRequestsQuery(filter), ct);

    [HttpGet("account-recovery/{id:guid}")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    [EnableRateLimiting("auth")]
    public Task<AccountRecoveryDto> AccountRecovery(
        Guid id,
        CancellationToken ct) =>
        mediator.Send(new GetAccountRecoveryRequestByIdQuery(id), ct);

    [HttpPost("account-recovery/{id:guid}/more-info")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    [EnableRateLimiting("auth")]
    public Task<AccountRecoveryDto> RequestRecoveryInformation(
        Guid id,
        AccountRecoveryAdminActionRequest request,
        CancellationToken ct) =>
        mediator.Send(new RequestMoreAccountRecoveryInfoCommand(AdminId(), id, request), ct);

    [HttpPost("account-recovery/{id:guid}/approve")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    [EnableRateLimiting("auth")]
    public Task<AccountRecoveryApprovalResponse> ApproveRecovery(
        Guid id,
        ApproveAccountRecoveryRequest request,
        CancellationToken ct) =>
        mediator.Send(new ApproveAccountRecoveryCommand(AdminId(), id, request), ct);

    [HttpPost("account-recovery/{id:guid}/reject")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    [EnableRateLimiting("auth")]
    public Task<AccountRecoveryDto> RejectRecovery(
        Guid id,
        AccountRecoveryAdminActionRequest request,
        CancellationToken ct) =>
        mediator.Send(new RejectAccountRecoveryCommand(AdminId(), id, request), ct);

    [HttpPost("users/{id:guid}/revoke-sessions")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    public async Task<IActionResult> Revoke(
        Guid id,
        SensitiveAccessRequest request,
        CancellationToken ct)
    {
        // Gửi Command thu hồi session người dùng và ghi audit quản trị.
        var adminId = AdminId();
        await mediator.Send(
            new RevokeUserSessionsCommand(adminId, id, request.Reason),
            ct);

        return NoContent();
    }

    [HttpPut("wallets/{id:guid}/freeze")]
    [Authorize(Policy = SpendWise.API.Infrastructure.V1Authorization.OperationsRead)]
    public async Task<IActionResult> Freeze(
        Guid id,
        FreezeWalletRequest request,
        CancellationToken ct)
    {
        // Gửi Command khóa hoặc mở ví; Controller không sửa số dư.
        var adminId = AdminId();
        await mediator.Send(new FreezeWalletCommand(adminId, id, request), ct);

        return NoContent();
    }

    [HttpPost("transactions/search")]
    public async Task<IReadOnlyList<AdminTransactionDto>> Transactions(
        SensitiveAccessRequest request,
        CancellationToken ct)
    {
        // Gửi yêu cầu tra cứu giao dịch nhạy cảm có ghi audit.
        var adminId = AdminId();

        return await mediator.Send(
            new SearchAdminTransactionsCommand(adminId, request),
            ct);
    }

    [HttpPost("ledger/search")]
    public async Task<IReadOnlyList<LedgerTransactionDto>> Ledger(
        SensitiveAccessRequest request,
        CancellationToken ct)
    {
        // Gửi yêu cầu tra cứu ledger chỉ đọc có ghi audit.
        var adminId = AdminId();

        return await mediator.Send(new SearchAdminLedgerCommand(adminId, request), ct);
    }

    [HttpGet("cases/{type}")]
    public async Task<IReadOnlyList<CaseDto>> Cases(
        string type,
        CancellationToken ct)
    {
        // Chuyển bộ lọc case sang Query quản trị tương ứng.
        return await mediator.Send(new GetAdminCasesQuery(type), ct);
    }

    [HttpPut("cases/{type}/{id:guid}")]
    public async Task<CaseDto> UpdateCase(
        string type,
        Guid id,
        UpdateCaseRequest request,
        CancellationToken ct)
    {
        // Lấy AdminId rồi gửi Command chuyển trạng thái case.
        var adminId = AdminId();

        return await mediator.Send(
            new UpdateAdminCaseCommand(adminId, type, id, request),
            ct);
    }

    [HttpGet("configuration")]
    public async Task<IReadOnlyList<ConfigurationDto>> Configuration(
        CancellationToken ct)
    {
        // Điều phối đọc hoặc cập nhật cấu hình hệ thống qua Handler tương ứng.
        return await mediator.Send(new GetConfigurationsQuery(), ct);
    }

    [HttpPut("configuration/{key}")]
    public async Task<ConfigurationDto> Configuration(
        string key,
        UpdateConfigurationRequest request,
        CancellationToken ct)
    {
        // Điều phối đọc hoặc cập nhật cấu hình hệ thống qua Handler tương ứng.
        var adminId = AdminId();

        return await mediator.Send(
            new UpdateConfigurationCommand(adminId, key, request),
            ct);
    }

    [HttpGet("feature-flags")]
    public async Task<IReadOnlyList<FeatureFlagDto>> Flags(CancellationToken ct)
    {
        // Đọc danh sách feature flag qua Query chỉ đọc.
        return await mediator.Send(new GetFeatureFlagsQuery(), ct);
    }

    [HttpPut("feature-flags/{name}")]
    public async Task<FeatureFlagDto> Flag(
        string name,
        UpdateFeatureFlagRequest request,
        CancellationToken ct)
    {
        // Lấy AdminId rồi gửi Command cập nhật một feature flag.
        var adminId = AdminId();

        return await mediator.Send(
            new UpdateFeatureFlagCommand(adminId, name, request),
            ct);
    }

    [HttpGet("audit-logs")]
    public async Task<IReadOnlyList<AuditLogDto>> Audit(CancellationToken ct)
    {
        // Đọc audit log qua Query, không cho sửa lịch sử audit.
        return await mediator.Send(new GetAuditLogsQuery(), ct);
    }

    [HttpGet("health")]
    public async Task<object> Health(CancellationToken ct)
    {
        // Đọc trạng thái database và dịch vụ cho màn hình admin.
        return await mediator.Send(new GetAdminHealthQuery(), ct);
    }

    private Guid AdminId()
    {
        // Lấy định danh từ phiên đã xác thực, không tin UserId do client gửi.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}

[ApiController]
[Authorize(Roles = "User")]
[Route("api/cases")]
public sealed class CasesController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("disputes")]
    public async Task<CaseDto> Dispute(
        CreateDisputeRequest request,
        CancellationToken ct)
    {
        // Gửi Command tạo tranh chấp cho giao dịch của người dùng hiện tại.
        var userId = UserId();

        return await mediator.Send(new CreateDisputeCommand(userId, request), ct);
    }

    [HttpPost("support")]
    public async Task<CaseDto> Support(
        CreateSupportCaseRequest request,
        CancellationToken ct)
    {
        // Gửi Command mở yêu cầu hỗ trợ cho người dùng hiện tại.
        var userId = UserId();

        return await mediator.Send(new CreateSupportCaseCommand(userId, request), ct);
    }

    [HttpGet("support")]
    public Task<IReadOnlyList<SupportCaseDetailDto>> SupportCases(CancellationToken ct) =>
        mediator.Send(new GetSupportCasesQuery(UserId()), ct);

    [HttpGet("support/{id:guid}")]
    public Task<SupportCaseDetailDto> SupportCase(Guid id, CancellationToken ct) =>
        mediator.Send(new GetSupportCaseQuery(UserId(), id), ct);

    [HttpPost("support/{id:guid}/messages")]
    public Task<SupportCaseDetailDto> ReplySupport(Guid id, CaseMessageRequest request, CancellationToken ct) =>
        mediator.Send(new AddSupportMessageCommand(UserId(), false, id, request), ct);

    [HttpPost("support/{id:guid}/messages/multipart")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = CaseMessageMultipartRequest.MaximumBodyLength)]
    public async Task<SupportCaseDetailDto> ReplySupportMultipart(
        Guid id,
        [FromForm] CaseMessageMultipartRequest request,
        CancellationToken ct) =>
        await mediator.Send(new AddSupportMessageCommand(
            UserId(),
            false,
            id,
            await request.ToApplicationRequestAsync(ct)), ct);

    [HttpGet("disputes")]
    public Task<IReadOnlyList<DisputeDetailDto>> Disputes(CancellationToken ct) =>
        mediator.Send(new GetDisputesQuery(UserId()), ct);

    [HttpGet("disputes/{id:guid}")]
    public Task<DisputeDetailDto> Dispute(Guid id, CancellationToken ct) =>
        mediator.Send(new GetDisputeQuery(UserId(), id), ct);

    [HttpPost("disputes/{id:guid}/messages")]
    public Task<DisputeDetailDto> ReplyDispute(Guid id, CaseMessageRequest request, CancellationToken ct) =>
        mediator.Send(new AddDisputeMessageCommand(UserId(), false, id, request), ct);

    [HttpPost("disputes/{id:guid}/messages/multipart")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = CaseMessageMultipartRequest.MaximumBodyLength)]
    public async Task<DisputeDetailDto> ReplyDisputeMultipart(
        Guid id,
        [FromForm] CaseMessageMultipartRequest request,
        CancellationToken ct) =>
        await mediator.Send(new AddDisputeMessageCommand(
            UserId(),
            false,
            id,
            await request.ToApplicationRequestAsync(ct)), ct);

    [HttpGet("attachments/{id:guid}")]
    public async Task<IActionResult> Attachment(Guid id, CancellationToken ct)
    {
        var file = await mediator.Send(new GetCaseAttachmentQuery(UserId(), false, id), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    private Guid UserId()
    {
        // Lấy định danh từ phiên đã xác thực, không tin UserId do client gửi.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}

[ApiController]
[Authorize(Policy = "SystemAdmin")]
[Route("api/admin/cases")]
public sealed class AdminCasesV2Controller(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    private Guid AdminId() => currentUser.UserId ?? throw new UnauthorizedAccessException();
    [HttpGet("support")]
    public Task<IReadOnlyList<SupportCaseDetailDto>> Support(CancellationToken ct) => mediator.Send(new GetSupportCasesQuery(null), ct);
    [HttpGet("support/{id:guid}")]
    public Task<SupportCaseDetailDto> Support(Guid id, CancellationToken ct) => mediator.Send(new GetSupportCaseQuery(null, id), ct);
    [HttpPost("support/{id:guid}/messages")]
    public Task<SupportCaseDetailDto> ReplySupport(Guid id, CaseMessageRequest request, CancellationToken ct) => mediator.Send(new AddSupportMessageCommand(AdminId(), true, id, request), ct);
    [HttpPost("support/{id:guid}/status")]
    public Task<SupportCaseDetailDto> SupportStatus(Guid id, CaseStatusRequest request, CancellationToken ct) => mediator.Send(new UpdateSupportStatusCommand(AdminId(), id, request), ct);
    [HttpPost("support/{id:guid}/correction-grant")]
    public Task<SupportCaseDetailDto> CorrectionGrant(Guid id, CorrectionGrantRequest request, CancellationToken ct) => mediator.Send(new GrantSupportCorrectionCommand(AdminId(), id, request), ct);
    [HttpGet("disputes")]
    public Task<IReadOnlyList<DisputeDetailDto>> Disputes(CancellationToken ct) => mediator.Send(new GetDisputesQuery(null), ct);
    [HttpGet("disputes/{id:guid}")]
    public Task<DisputeDetailDto> Dispute(Guid id, CancellationToken ct) => mediator.Send(new GetDisputeQuery(null, id), ct);
    [HttpPost("disputes/{id:guid}/messages")]
    public Task<DisputeDetailDto> ReplyDispute(Guid id, CaseMessageRequest request, CancellationToken ct) => mediator.Send(new AddDisputeMessageCommand(AdminId(), true, id, request), ct);
    [HttpPost("disputes/{id:guid}/status")]
    public Task<DisputeDetailDto> DisputeStatus(Guid id, CaseStatusRequest request, CancellationToken ct) => mediator.Send(new UpdateDisputeStatusCommand(AdminId(), id, request), ct);
    [HttpGet("attachments/{id:guid}")]
    public async Task<IActionResult> Attachment(Guid id, CancellationToken ct) { var file = await mediator.Send(new GetCaseAttachmentQuery(AdminId(), true, id), ct); return File(file.Content, file.ContentType, file.FileName); }
}
