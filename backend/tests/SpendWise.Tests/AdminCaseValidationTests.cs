using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Cases.Commands.UpdateAdminCase;
using SpendWise.Application.Features.Admin.Cases.Queries.GetAdminCases;
using SpendWise.Domain.Admin;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ allow-list loại case và enum trạng thái của từng workflow admin.
public sealed class AdminCaseValidationTests
{
    [Fact]
    public async Task Risk_query_reads_only_risk_cases()
    {
        // Arrange: tạo đồng thời đủ ba loại case để phát hiện chọn nhầm collection.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var cases = await AddCasesAsync(fixture);
        var handler = new GetAdminCasesQueryHandler(fixture.Db);

        // Act: yêu cầu loại risk bằng đúng route contract hiện tại.
        var result = await handler.Handle(new GetAdminCasesQuery("risk"), default);

        // Assert: chỉ RiskCase được trả về.
        var item = Assert.Single(result);
        Assert.Equal(cases.Risk.Id, item.Id);
        Assert.Equal("Risk", item.Type);
    }

    [Fact]
    public async Task Disputes_query_reads_only_disputes()
    {
        // Arrange: dữ liệu có cả risk, dispute và support.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var cases = await AddCasesAsync(fixture);
        var handler = new GetAdminCasesQueryHandler(fixture.Db);

        // Act: lấy collection disputes.
        var result = await handler.Handle(new GetAdminCasesQuery("disputes"), default);

        // Assert: handler không fallback sang RiskCase hoặc SupportCase.
        var item = Assert.Single(result);
        Assert.Equal(cases.Dispute.Id, item.Id);
        Assert.Equal("Dispute", item.Type);
    }

    [Fact]
    public async Task Support_query_reads_only_support_cases()
    {
        // Arrange: dữ liệu có đủ ba collection admin case.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var cases = await AddCasesAsync(fixture);
        var handler = new GetAdminCasesQueryHandler(fixture.Db);

        // Act: lấy collection support.
        var result = await handler.Handle(new GetAdminCasesQuery("support"), default);

        // Assert: chỉ SupportCase được map sang DTO.
        var item = Assert.Single(result);
        Assert.Equal(cases.Support.Id, item.Id);
        Assert.Equal("Support", item.Type);
    }

    [Fact]
    public async Task Unknown_case_type_returns_invalid_case_type()
    {
        // Arrange: handler không cần dữ liệu để từ chối type ngoài allow-list.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var handler = new GetAdminCasesQueryHandler(fixture.Db);

        // Act: truyền type không được API hỗ trợ.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new GetAdminCasesQuery("unknown"),
            default));

        // Assert: lỗi 400 rõ ràng thay vì ngầm fallback sang support.
        Assert.Equal("invalid_case_type", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Invalid_risk_status_returns_invalid_case_status()
    {
        // Arrange: RiskCase chỉ dùng ReviewStatus.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin");
        var handler = CreateUpdateHandler(fixture);

        // Act: gửi CaseStatus không thuộc enum ReviewStatus.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new UpdateAdminCaseCommand(
                admin.Id,
                "risk",
                Guid.NewGuid(),
                new UpdateCaseRequest("Resolved", null, "Valid review reason")),
            default));

        // Assert: validation dừng ở lỗi 400 trước khi tìm case.
        Assert.Equal("invalid_case_status", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Invalid_dispute_status_returns_invalid_case_status()
    {
        // Arrange: dispute chỉ chấp nhận giá trị CaseStatus đã định nghĩa.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin");
        var handler = CreateUpdateHandler(fixture);

        // Act: chuỗi số parse được enum nhưng không phải giá trị đã định nghĩa.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new UpdateAdminCaseCommand(
                admin.Id,
                "disputes",
                Guid.NewGuid(),
                new UpdateCaseRequest("999", null, "Valid review reason")),
            default));

        // Assert: Enum.IsDefined ngăn trạng thái ngoài workflow.
        Assert.Equal("invalid_case_status", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Invalid_support_status_returns_invalid_case_status()
    {
        // Arrange: support dùng cùng CaseStatus nhưng vẫn phải validate riêng.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin");
        var handler = CreateUpdateHandler(fixture);

        // Act: gửi trạng thái không tồn tại.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new UpdateAdminCaseCommand(
                admin.Id,
                "support",
                Guid.NewGuid(),
                new UpdateCaseRequest("Escalated", null, "Valid review reason")),
            default));

        // Assert: mã lỗi và HTTP status thống nhất.
        Assert.Equal("invalid_case_status", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Valid_statuses_update_all_three_case_workflows()
    {
        // Arrange: tạo admin và một bản ghi cho mỗi loại case.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin");
        var cases = await AddCasesAsync(fixture);
        var handler = CreateUpdateHandler(fixture);

        // Act: cập nhật RiskCase bằng ReviewStatus và hai case còn lại bằng CaseStatus.
        var risk = await handler.Handle(
            new UpdateAdminCaseCommand(
                admin.Id,
                "risk",
                cases.Risk.Id,
                new UpdateCaseRequest("Approved", null, "Risk was reviewed")),
            default);
        var dispute = await handler.Handle(
            new UpdateAdminCaseCommand(
                admin.Id,
                "disputes",
                cases.Dispute.Id,
                new UpdateCaseRequest("Resolved", "Resolved safely", "Dispute was reviewed")),
            default);
        var support = await handler.Handle(
            new UpdateAdminCaseCommand(
                admin.Id,
                "support",
                cases.Support.Id,
                new UpdateCaseRequest("Closed", "Answer delivered", "Support was reviewed")),
            default);

        // Assert: từng entity nhận đúng enum và audit được ghi cho cả ba thao tác.
        Assert.Equal("Approved", risk.Status);
        Assert.Equal("Resolved", dispute.Status);
        Assert.Equal("Closed", support.Status);
        Assert.Equal(ReviewStatus.Approved, cases.Risk.ReviewStatus);
        Assert.Equal(CaseStatus.Resolved, cases.Dispute.Status);
        await fixture.Db.Entry(cases.Support).ReloadAsync();
        Assert.Equal(CaseStatus.Closed, cases.Support.Status);
        Assert.Equal(3, await fixture.Db.AuditLogs.CountAsync());
    }

    private static UpdateAdminCaseCommandHandler CreateUpdateHandler(
        TargetedCorrectnessFixture fixture) =>
        new(
            fixture.Db,
            new AdminAuditWriter(fixture.Db),
            fixture.Clock,
            SupportEmailTestFactory.Create(fixture.Db),
            new SpendWise.Infrastructure.Notifications.NotificationService(fixture.Db));

    private static async Task<(RiskCase Risk, Dispute Dispute, SupportCase Support)> AddCasesAsync(
        TargetedCorrectnessFixture fixture)
    {
        // Dùng một user thật để dữ liệu test gần với workflow API hiện tại.
        var user = await fixture.AddUserAsync("Case owner");
        var risk = new RiskCase
        {
            UserId = user.Id,
            RiskScore = 75,
            RiskLevel = RiskLevel.High,
            Decision = RiskDecision.Review,
            Signals = "test-signal"
        };
        var dispute = new Dispute
        {
            UserId = user.Id,
            LinkedTransactionId = Guid.NewGuid(),
            Reason = "Test dispute"
        };
        var support = new SupportCase
        {
            LinkedUserId = user.Id,
            Category = "Account",
            Priority = "Normal"
        };
        fixture.Db.AddRange(risk, dispute, support);
        await fixture.Db.SaveChangesAsync();
        return (risk, dispute, support);
    }
}
