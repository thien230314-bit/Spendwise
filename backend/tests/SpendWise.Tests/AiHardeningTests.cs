using System.Text.Json;
using System.Text;
using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SpendWise.Application.AI;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.AiAssistant.Commands.SendAiMessage;
using SpendWise.Domain.Admin;
using SpendWise.Domain.AI;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Planning;
using SpendWise.Application.Common;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.AI;
using SpendWise.Infrastructure.Admin;
using SpendWise.Application.Features.Transactions.Commands.CreateIncome;
using SpendWise.Application.Features.Budgets.Commands.UpdateBudget;
using SpendWise.Application.Features.Goals.Commands.CreateGoal;
using SpendWise.Application.Features.RecurringTransactions.Commands.RecordRecurringOccurrence;
using SpendWise.Application.Features.Transactions.Commands.UpdateTransaction;
using SpendWise.Application.Features.Groups;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class AiHardeningTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_key_is_unavailable_regardless_of_fallback_flag(bool allowFallback)
    {
        // Arrange: không cấu hình key và tuyệt đối không thực hiện network call.
        var provider = new GeminiCompletionProvider(new HttpClient(), new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Gemini:ApiKey"] = "", ["AI:AllowDevelopmentFallback"] = allowFallback.ToString()
            }).Build());

        // Act/Assert: Production và Staging không bao giờ giả phản hồi AI.
        var error = await Assert.ThrowsAsync<AppException>(() => provider.CompleteAsync(EmptyProviderRequest(), default));
        Assert.Equal("AI_PROVIDER_UNAVAILABLE", error.Code);
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public async Task Development_fallback_flags_cannot_fabricate_success()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "",
            ["Gemini:AllowDevelopmentFallback"] = "true",
            ["AI:AllowDevelopmentFallback"] = "true"
        }).Build();
        var provider = new GeminiCompletionProvider(new HttpClient(), configuration);
        var error = await Assert.ThrowsAsync<AppException>(() => provider.CompleteAsync(EmptyProviderRequest(), default));
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public void Configured_real_provider_path_is_reported_without_external_call()
    {
        // Arrange: key giả chỉ dùng để kiểm tra nhánh cấu hình, không gọi Gemini.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-placeholder-not-a-real-secret",
            ["Gemini:Model"] = "test-model"
        }).Build();

        // Act/Assert: health status nhận đúng đường provider thật và không gắn nhãn fallback.
        var status = new GeminiProviderStatus(configuration);
        Assert.True(status.IsOnlineConfigured);
        Assert.Equal("Configured", status.Status);
    }

    [Fact]
    public void Registered_ai_action_tools_use_closed_schemas_without_secret_fields()
    {
        // Arrange: lấy registry thật để kiểm tra đúng contract mà Gemini nhận được.
        using var factory = new SpendWiseApiFactory();
        using var scope = factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IAiToolRegistry>();

        // Act: chọn các tool có thể tạo proposal hoặc thay đổi dữ liệu.
        var actionTools = registry.Definitions.Where(tool => tool.Name.Contains("prepare_", StringComparison.Ordinal)
            || tool.Name is "send_friend_request" or "accept_friend_request" or "reject_friend_request" or "create_support_case"
                or "reply_support_case" or "create_recurring" or "update_recurring" or "pause_recurring" or "resume_recurring");

        // Assert: model không được gửi thêm thuộc tính hoặc đưa bí mật vào payload.
        Assert.NotEmpty(actionTools);
        foreach (var tool in actionTools)
        {
            Assert.Contains("\"additionalProperties\":false", tool.JsonSchema, StringComparison.Ordinal);
            Assert.DoesNotContain("SecurityPin", tool.JsonSchema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password", tool.JsonSchema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RefreshToken", tool.JsonSchema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ApiKey", tool.JsonSchema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Secret", tool.JsonSchema, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Optional_ai_action_fields_are_not_falsely_required()
    {
        // Arrange: đọc schema thật mà provider nhận cho các action có trường tùy chọn.
        using var factory = new SpendWiseApiFactory();
        using var scope = factory.Services.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IAiToolRegistry>().Definitions.ToDictionary(item => item.Name);

        // Act: lấy riêng mảng required thay vì chỉ tìm chuỗi toàn schema.
        static string[] Required(AiToolDefinition tool)
        {
            using var json = JsonDocument.Parse(tool.JsonSchema);
            return json.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()!).ToArray();
        }

        // Assert: backend phân biệt allowed và required đúng theo contract hiện hành.
        Assert.DoesNotContain("alerts_enabled", Required(tools["prepare_budget_update"]));
        Assert.DoesNotContain("rollover", Required(tools["prepare_budget_update"]));
        Assert.DoesNotContain("target_date", Required(tools["prepare_goal_create"]));
        Assert.DoesNotContain("counterparty", Required(tools["prepare_transaction_correction"]));
        Assert.DoesNotContain("paid_by_user_id", Required(tools["prepare_group_expense_create"]));
        Assert.DoesNotContain("description", Required(tools["prepare_group_expense_create"]));
    }

    [Fact]
    public async Task Proposal_sanitizer_rejects_secret_fields_and_keeps_evidence_at_execute_boundary()
    {
        // Arrange: tạo User thật để proposal tuân thủ foreign key.
        using var factory = new SpendWiseApiFactory();
        _ = factory.CreateHttpsClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await AddUserAsync(db, "AI sanitizer");
        var registry = scope.ServiceProvider.GetRequiredService<IAiToolRegistry>();
        var transactionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var walletId = Guid.NewGuid();

        // Act/Assert: thuộc tính bí mật không thuộc allowlist bị từ chối hoàn toàn.
        await Assert.ThrowsAsync<AppException>(() => registry.ExecuteAsync(user.Id, null,
            new AiToolCall("prepare_budget_create",
                $$"""{"category_id":"{{categoryId}}","year":2026,"month":9,"limit_amount":1000,"Password":"never-store"}"""), default));

        // Act: correction không nhận evidence từ model và counterparty thực sự tùy chọn.
        var resultJson = await registry.ExecuteAsync(user.Id, null,
            new AiToolCall("prepare_transaction_correction",
                $$"""{"transaction_id":"{{transactionId}}","category_id":"{{categoryId}}","wallet_id":"{{walletId}}","amount":1000,"description":"Correct","reason":"Wrong metadata","occurred_at":"2026-09-18T00:00:00Z"}"""), default);
        using var result = JsonDocument.Parse(resultJson);
        var proposal = await db.AiActionProposals.SingleAsync();

        // Assert: payload chỉ có dữ liệu đã kiểm tra và evidence/PIN vẫn ở execute request riêng.
        Assert.False(result.RootElement.GetProperty("RequiresEvidence").GetBoolean());
        Assert.DoesNotContain("evidence", proposal.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pin", proposal.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", proposal.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ai_action_dates_accept_iso_instants_normalize_offsets_and_reject_ambiguous_text()
    {
        // Arrange: dùng registry thật để kiểm tra cả validation lẫn payload đã sanitize.
        using var factory = new SpendWiseApiFactory();
        _ = factory.CreateHttpsClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await AddUserAsync(db, "AI ISO dates");
        var registry = scope.ServiceProvider.GetRequiredService<IAiToolRegistry>();
        var transactionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var walletId = Guid.NewGuid();

        // Act: Z và offset đều là ISO rõ ràng; offset +07 được chuẩn hóa về UTC trước khi lưu proposal.
        await registry.ExecuteAsync(user.Id, null, new("prepare_transaction_correction",
            $$"""{"transaction_id":"{{transactionId}}","category_id":"{{categoryId}}","wallet_id":"{{walletId}}","amount":1000,"description":"Fix","reason":"Metadata","occurred_at":"2026-10-01T00:30:00+07:00"}"""), default);
        await registry.ExecuteAsync(user.Id, null, new("prepare_goal_create",
            """{"name":"Laptop","target_amount":1000000,"target_date":"2027-01-01T00:00:00Z"}"""), default);
        await registry.ExecuteAsync(user.Id, null, new("prepare_group_expense_create",
            $$"""{"group_id":"{{Guid.NewGuid()}}","title":"Lunch","amount":1000,"category_id":"{{categoryId}}","paid_by_type":"fund","date":"2026-10-01T00:30:00+07:00"}"""), default);

        var proposals = await db.AiActionProposals.AsNoTracking().ToDictionaryAsync(item => item.ActionType);
        using var correction = JsonDocument.Parse(proposals["transaction_correction"].PayloadJson);
        using var goal = JsonDocument.Parse(proposals["goal_create"].PayloadJson);
        using var group = JsonDocument.Parse(proposals["group_expense_create"].PayloadJson);

        // Assert: mọi instant trong payload có một cách hiểu UTC duy nhất.
        Assert.Equal(new DateTime(2026, 9, 30, 17, 30, 0, DateTimeKind.Utc),
            correction.RootElement.GetProperty("occurred_at").GetDateTime());
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            goal.RootElement.GetProperty("target_date").GetDateTime());
        Assert.Equal(new DateTime(2026, 9, 30, 17, 30, 0, DateTimeKind.Utc),
            group.RootElement.GetProperty("date").GetDateTime());

        var error = await Assert.ThrowsAsync<AppException>(() => registry.ExecuteAsync(user.Id, null,
            new("prepare_goal_create",
                """{"name":"Ambiguous","target_amount":1000000,"target_date":"01/02/2027"}"""), default));
        Assert.Equal("ai_tool_arguments_invalid", error.Code);
    }

    [Fact]
    public async Task Fund_paid_group_expense_does_not_require_paid_by_user_id()
    {
        // Arrange: proposal chỉ kiểm tra/sanitize payload; quyền nhóm vẫn do command thật kiểm tra lúc Execute.
        using var factory = new SpendWiseApiFactory();
        _ = factory.CreateHttpsClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await AddUserAsync(db, "Fund proposal");
        var registry = scope.ServiceProvider.GetRequiredService<IAiToolRegistry>();

        // Act: paid_by_type=fund không cần ID người trả.
        await registry.ExecuteAsync(user.Id, null, new AiToolCall("prepare_group_expense_create",
            $$"""{"group_id":"{{Guid.NewGuid()}}","title":"Team lunch","amount":250000,"category_id":"{{Guid.NewGuid()}}","paid_by_type":"fund","date":"2026-09-18T00:00:00Z"}"""), default);
        var proposal = await db.AiActionProposals.SingleAsync();

        // Assert: payload sạch không tự chế paid_by_user_id hoặc description.
        Assert.DoesNotContain("paid_by_user_id", proposal.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("description", proposal.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ai_attachments_enforce_type_size_owner_count_parse_and_text_bounds()
    {
        // Arrange: dùng handler/reader thật trên SQLite có ownership FK.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Attachment owner");
        var stranger = await fixture.AddUserAsync("Attachment stranger");
        var upload = new SpendWise.Application.Features.AiAssistant.UploadAiAttachmentCommandHandler(fixture.Db);
        var supported = new (string Name, string Type, byte[] Content)[]
        {
            ("photo.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            ("photo.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF, 0xD9]),
            ("photo.webp", "image/webp", Encoding.ASCII.GetBytes("RIFF0000WEBP")),
            ("note.txt", "text/plain", Encoding.UTF8.GetBytes("safe text")),
            ("rows.csv", "text/csv", Encoding.UTF8.GetBytes("a,b\n1,2")),
            ("data.json", "application/json", Encoding.UTF8.GetBytes("{\"ok\":true}"))
        };

        // Act/Assert: các format allowlist được lưu, executable/oversize bị chặn.
        foreach (var item in supported)
            await upload.Handle(new(owner.Id, new(item.Name, item.Type, item.Content)), default);
        await Assert.ThrowsAsync<AppException>(() => upload.Handle(new(owner.Id,
            new("malware.txt", "text/plain", Encoding.ASCII.GetBytes("MZ executable"))), default));
        await Assert.ThrowsAsync<AppException>(() => upload.Handle(new(owner.Id,
            new("large.txt", "text/plain", new byte[10 * 1024 * 1024 + 1])), default));

        var text = Enumerable.Repeat((byte)'a', 300 * 1024).ToArray();
        var textDto = await upload.Handle(new(owner.Id, new("bounded.txt", "text/plain", text)), default);
        var malformedPdf = await upload.Handle(new(owner.Id,
            new("broken.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-not-a-real-document"))), default);
        var reader = new AiAttachmentReader(fixture.Db);
        var bounded = Assert.Single(await reader.ReadAsync(owner.Id, [textDto.Id], default));
        Assert.True(Encoding.UTF8.GetByteCount(bounded.ExtractedText!) <= 200 * 1024);
        var parseError = await Assert.ThrowsAsync<AppException>(() => reader.ReadAsync(owner.Id, [malformedPdf.Id], default));
        Assert.Equal("ai_attachment_parse_failed", parseError.Code);
        await Assert.ThrowsAsync<AppException>(() => reader.ReadAsync(owner.Id,
            Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray(), default));

        // Assert: user khác không thể tải hoặc dùng attachment của owner.
        await Assert.ThrowsAsync<NotFoundException>(() => new SpendWise.Application.Features.AiAssistant.GetAiAttachmentQueryHandler(fixture.Db)
            .Handle(new(stranger.Id, textDto.Id), default));
        await Assert.ThrowsAsync<NotFoundException>(() => reader.ReadAsync(stranger.Id, [textDto.Id], default));

        // Arrange/Act: attachment đã gắn vào message cũ không được tái sử dụng cho chat mới.
        var oldConversation = new AiConversation { UserId = owner.Id, Title = "Old attachment" };
        var oldMessage = new AiMessage
        {
            AiConversationId = oldConversation.Id,
            Conversation = oldConversation,
            Role = AiMessageRole.User,
            Content = "Old"
        };
        var linked = await fixture.Db.AiAttachments.SingleAsync(item => item.Id == textDto.Id);
        linked.AiConversationId = oldConversation.Id;
        linked.Conversation = oldConversation;
        linked.AiMessageId = oldMessage.Id;
        linked.Message = oldMessage;
        fixture.Db.AiConversations.Add(oldConversation);
        fixture.Db.AiMessages.Add(oldMessage);
        fixture.Db.FeatureFlags.Add(new FeatureFlag { Name = "AIAssistant", IsEnabled = true, Description = "Current" });
        await fixture.Db.SaveChangesAsync();
        var provider = new NeverCalledProvider();
        var send = new SendAiMessageCommandHandler(
            fixture.Db,
            new SpendWise.Application.Features.AiAssistant.AiFinancialContextBuilder(
                fixture.Db, fixture.Clock, new SpendWise.Application.Features.Budgets.BudgetReader(fixture.Db)),
            new ProfanityFilter(),
            provider,
            reader,
            new SpendWise.Application.Features.AiAssistant.AiToolRegistry(fixture.Db, new RecordingMediator(), fixture.Clock),
            new OperationPolicy(fixture.Db),
            fixture.Clock);
        await Assert.ThrowsAsync<ConflictException>(() => send.Handle(new(owner.Id, "User", "en-US",
            new AiChatRequest(null, "Use this file", AttachmentIds: [linked.Id])), default));
        Assert.False(provider.WasCalled);
    }

    [Fact]
    public async Task One_chat_turn_creates_at_most_one_sensitive_proposal()
    {
        // Arrange: provider test double cố gọi hai prepare tool trong cùng một lượt model.
        using var factory = new SpendWiseApiFactory();
        _ = factory.CreateHttpsClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await AddUserAsync(db, "One proposal");
        db.FeatureFlags.Add(new FeatureFlag { Name = "AIAssistant", IsEnabled = true, Description = "Current AI gate" });
        await db.SaveChangesAsync();
        var provider = new DoubleSensitiveToolProvider();
        var handler = new SendAiMessageCommandHandler(
            db,
            scope.ServiceProvider.GetRequiredService<IAiDataTools>(),
            scope.ServiceProvider.GetRequiredService<IProfanityFilter>(),
            provider,
            scope.ServiceProvider.GetRequiredService<IAiAttachmentReader>(),
            scope.ServiceProvider.GetRequiredService<IAiToolRegistry>(),
            scope.ServiceProvider.GetRequiredService<IOperationPolicy>(),
            scope.ServiceProvider.GetRequiredService<IClock>());

        // Act: handler cho provider chạy bounded tool callback như production.
        await handler.Handle(new SendAiMessageCommand(user.Id, "User", "vi-VN",
            new AiChatRequest(null, "Xóa một mục tiêu")), default);

        // Assert: lần prepare thứ hai nhận lỗi tool và không tạo hàng Pending thứ hai.
        Assert.Equal(1, await db.AiActionProposals.CountAsync());
        Assert.Contains("one_sensitive_proposal_per_turn", provider.SecondResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrent_cancel_and_execute_have_exactly_one_pending_state_winner()
    {
        // Arrange: database file cho hai DbContext/connection tranh chấp cùng một proposal thật.
        using var factory = new SpendWiseApiFactory(useIndependentDatabaseConnections: true);
        using var client = factory.CreateHttpsClient();
        Guid userId;
        Guid proposalId;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = await AddUserAsync(db, "AI concurrency");
            user.SecurityPinHash = new SecurityPinService().Hash("123456");
            var category = await db.Categories.SingleAsync(item => item.Key == "categories.food");
            var budget = new Budget
            {
                OwnerUserId = user.Id,
                CategoryId = category.Id,
                Year = 2026,
                Month = 9,
                LimitAmount = 500_000
            };
            var proposal = new AiActionProposal
            {
                OwnerUserId = user.Id,
                ActionType = "budget_delete",
                PayloadJson = $$"""{"budget_id":"{{budget.Id}}"}""",
                SafeSummary = "Delete budget configuration.",
                Status = AiActionProposalStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                OperationKey = $"test-{Guid.NewGuid():N}"
            };
            db.AddRange(budget, proposal);
            await db.SaveChangesAsync();
            userId = user.Id;
            proposalId = proposal.Id;
        }

        await using var executeScope = factory.Services.CreateAsyncScope();
        await using var cancelScope = factory.Services.CreateAsyncScope();
        var executeDb = executeScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var cancelDb = cancelScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var execute = new SpendWise.Application.Features.AiAssistant.ExecuteAiActionCommandHandler(
            executeDb,
            executeScope.ServiceProvider.GetRequiredService<IMediator>(),
            executeScope.ServiceProvider.GetRequiredService<IClock>());
        var cancel = new SpendWise.Application.Features.AiAssistant.CancelAiActionCommandHandler(cancelDb);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Exception?> RunExecuteAsync()
        {
            await gate.Task;
            try
            {
                await execute.Handle(new SpendWise.Application.Features.AiAssistant.ExecuteAiActionCommand(
                    userId, proposalId, new ExecuteAiActionRequest("123456", ExplicitConfirmation: true)), default);
                return null;
            }
            catch (Exception error) { return error; }
        }

        async Task<Exception?> RunCancelAsync()
        {
            await gate.Task;
            try
            {
                await cancel.Handle(new SpendWise.Application.Features.AiAssistant.CancelAiActionCommand(userId, proposalId), default);
                return null;
            }
            catch (Exception error) { return error; }
        }

        // Act: hai thao tác bắt đầu cùng lúc trên hai connection.
        var executeTask = RunExecuteAsync();
        var cancelTask = RunCancelAsync();
        gate.SetResult();
        var outcomes = await Task.WhenAll(executeTask, cancelTask);

        // Assert: chỉ một compare-and-set từ Pending thành công; bên còn lại bị Conflict.
        Assert.Single(outcomes, item => item is null);
        Assert.Single(outcomes, item => item is ConflictException);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var finalStatus = await verifyDb.AiActionProposals.Where(item => item.Id == proposalId)
            .Select(item => item.Status).SingleAsync();
        Assert.Contains(finalStatus, new[] { AiActionProposalStatus.Cancelled, AiActionProposalStatus.Executed });
    }

    [Fact]
    public async Task Sensitive_executor_dispatches_current_commands_and_keeps_pin_evidence_out_of_payload()
    {
        // Arrange: recording mediator xác minh executor tái sử dụng Command hiện hành, không nhân đôi business logic.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("AI action dispatch");
        var category = await fixture.Db.Categories.SingleAsync(item => item.Key == "categories.food");
        var budget = new Budget
        {
            OwnerUserId = user.Id,
            CategoryId = category.Id,
            Year = 2026,
            Month = 9,
            LimitAmount = 500_000,
            AlertsEnabled = false,
            Rollover = true
        };
        fixture.Db.Budgets.Add(budget);
        await fixture.Db.SaveChangesAsync();
        var evidenceId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var recurringId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var mediator = new RecordingMediator();
        var handler = new SpendWise.Application.Features.AiAssistant.ExecuteAiActionCommandHandler(fixture.Db, mediator, fixture.Clock);

        var actions = new (string Type, string Payload)[]
        {
            ("manual_income", $$"""{"wallet_id":"{{walletId}}","category_id":"{{category.Id}}","amount":120000,"description":"Salary"}"""),
            ("budget_create", $$"""{"category_id":"{{category.Id}}","year":2026,"month":10,"limit_amount":600000,"alerts_enabled":false,"rollover":true}"""),
            ("budget_update", $$"""{"budget_id":"{{budget.Id}}","limit_amount":700000}"""),
            ("budget_delete", $$"""{"budget_id":"{{budget.Id}}"}"""),
            ("goal_create", $$"""{"name":"Laptop","target_amount":20000000,"target_date":"2027-01-01T00:00:00Z","category_id":"{{category.Id}}","icon_name":"Laptop","color_hex":"#123456","initial_amount":100000,"wallet_id":"{{walletId}}"}"""),
            ("goal_withdrawal", $$"""{"goal_id":"{{goalId}}","wallet_id":"{{walletId}}","amount":50000}"""),
            ("recurring_occurrence_record", $$"""{"recurring_transaction_id":"{{recurringId}}","occurrence_id":"{{occurrenceId}}","idempotency_key":"rec-test"}"""),
            ("transaction_correction", $$"""{"transaction_id":"{{transactionId}}","category_id":"{{category.Id}}","wallet_id":"{{walletId}}","amount":1000,"description":"Correct","reason":"Metadata","occurred_at":"2026-09-18T00:00:00Z"}"""),
            ("bank_direct_sender_confirmation", $$"""{"payment_id":"{{paymentId}}"}"""),
            ("group_expense_create", $$"""{"group_id":"{{groupId}}","title":"Lunch","amount":200000,"category_id":"{{category.Id}}","paid_by_type":"fund","date":"2026-09-18T00:00:00Z"}"""),
            ("group_deletion_finalize", $$"""{"group_id":"{{groupId}}","request_id":"{{requestId}}"}""")
        };

        // Act: mỗi proposal được claim và dispatch đúng một lần.
        foreach (var action in actions)
        {
            var proposal = await AddProposalAsync(fixture.Db, user.Id, action.Type, action.Payload);
            await handler.Handle(new SpendWise.Application.Features.AiAssistant.ExecuteAiActionCommand(
                user.Id, proposal.Id, new ExecuteAiActionRequest("908172", [evidenceId], true)), default);
        }

        // Assert: đủ các current command được gọi; PIN/evidence chỉ đi qua request execute vào Command.
        var names = mediator.Requests.Select(item => item.GetType().Name).ToArray();
        Assert.Contains("CreateIncomeCommand", names);
        Assert.Contains("CreateBudgetCommand", names);
        Assert.Contains("UpdateBudgetCommand", names);
        Assert.Contains("DeleteBudgetCommand", names);
        Assert.Contains("CreateGoalCommand", names);
        Assert.Contains("WithdrawGoalCommand", names);
        Assert.Contains("RecordRecurringOccurrenceCommand", names);
        Assert.Contains("UpdateTransactionCommand", names);
        Assert.Contains("ConfirmBankDirectPaymentCommand", names);
        Assert.Contains("CreateCurrentGroupExpenseCommand", names);
        Assert.Contains("FinalizeGroupDeletionCommand", names);

        var income = Assert.IsType<CreateIncomeCommand>(mediator.Requests.Single(item => item is CreateIncomeCommand));
        Assert.Equal("908172", income.Request.SecurityPin);
        Assert.Equal([evidenceId], income.Request.EvidenceIds);
        var budgetUpdate = Assert.IsType<UpdateBudgetCommand>(mediator.Requests.Single(item => item is UpdateBudgetCommand));
        Assert.False(budgetUpdate.Request.AlertsEnabled);
        Assert.True(budgetUpdate.Request.Rollover);
        var goalCreate = Assert.IsType<CreateGoalCommand>(mediator.Requests.Single(item => item is CreateGoalCommand));
        Assert.Equal(category.Id, goalCreate.Request.CategoryId);
        Assert.Equal(100_000, goalCreate.Request.InitialAmount);
        var recurring = Assert.IsType<RecordRecurringOccurrenceCommand>(mediator.Requests.Single(item => item is RecordRecurringOccurrenceCommand));
        Assert.Equal([evidenceId], recurring.Request.EvidenceIds);
        var correction = Assert.IsType<UpdateTransactionCommand>(mediator.Requests.Single(item => item is UpdateTransactionCommand));
        Assert.Equal([evidenceId], correction.Request.EvidenceIds);
        var groupExpense = Assert.IsType<CreateCurrentGroupExpenseCommand>(mediator.Requests.Single(item => item is CreateCurrentGroupExpenseCommand));
        Assert.Equal(category.Id, groupExpense.Request.CategoryId);
        Assert.Null(groupExpense.Request.PaidByUserId);
        Assert.All(await fixture.Db.AiActionProposals.ToListAsync(), item => Assert.Equal(AiActionProposalStatus.Executed, item.Status));
        Assert.All(await fixture.Db.AiActionProposals.ToListAsync(), item =>
        {
            Assert.DoesNotContain("908172", item.PayloadJson, StringComparison.Ordinal);
            Assert.DoesNotContain("evidence", item.PayloadJson, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Direct_recurring_and_support_tools_dispatch_only_current_non_pin_commands()
    {
        // Arrange: registry thật + mediator ghi nhận command, không thực hiện financial posting.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("AI direct tools");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", true);
        var category = await fixture.Db.Categories.SingleAsync(item => item.Key == "categories.food");
        var mediator = new RecordingMediator();
        var registry = new SpendWise.Application.Features.AiAssistant.AiToolRegistry(fixture.Db, mediator, fixture.Clock);
        var recurringId = Guid.NewGuid();
        var recurringPayload = $$"""{"title":"Internet","description":"Monthly bill","wallet_id":"{{wallet.Id}}","category_id":"{{category.Id}}","kind":"Expense","amount":100000,"frequency":"Monthly","next_run_at":"2026-10-18T00:00:00Z","is_subscription":true}""";
        var recurringUpdatePayload = $$"""{"recurring_transaction_id":"{{recurringId}}","title":"Internet updated","description":"Monthly bill","wallet_id":"{{wallet.Id}}","category_id":"{{category.Id}}","kind":"Expense","amount":120000,"frequency":"Monthly","next_run_at":"2026-11-18T00:00:00Z"}""";

        // Act: bốn lifecycle action và reply Support đi thẳng qua Command hiện có.
        await registry.ExecuteAsync(user.Id, null, new("create_recurring", recurringPayload), default);
        await registry.ExecuteAsync(user.Id, null, new("update_recurring", recurringUpdatePayload), default);
        await registry.ExecuteAsync(user.Id, null, new("pause_recurring", $$"""{"recurring_transaction_id":"{{recurringId}}"}"""), default);
        await registry.ExecuteAsync(user.Id, null, new("resume_recurring", $$"""{"recurring_transaction_id":"{{recurringId}}"}"""), default);
        await registry.ExecuteAsync(user.Id, null, new("reply_support_case",
            $$"""{"case_id":"{{Guid.NewGuid()}}","message":"Requested information"}"""), default);

        // Assert: không tool nào biến recurring definition thà giao dịch tiền và không có PIN trong schema.
        var names = mediator.Requests.Select(item => item.GetType().Name).ToArray();
        Assert.Contains("CreateRecurringTransactionCommand", names);
        Assert.Contains("UpdateRecurringTransactionCommand", names);
        Assert.Contains("PauseRecurringTransactionCommand", names);
        Assert.Contains("ResumeRecurringTransactionCommand", names);
        Assert.Contains("AddSupportMessageCommand", names);
        Assert.DoesNotContain(names, name => name == "RecordRecurringOccurrenceCommand");
        foreach (var name in new[] { "create_recurring", "update_recurring", "pause_recurring", "resume_recurring", "reply_support_case" })
            Assert.DoesNotContain("pin", registry.Definitions.Single(item => item.Name == name).JsonSchema, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false, "AI_ACTION_EXECUTION_FAILED")]
    [InlineData(true, "AI_ACTION_CANCELLED")]
    public async Task Execution_failure_never_leaves_proposal_executing(bool cancellation, string expectedCode)
    {
        // Arrange: mediator test double mô phỏng lỗi ngoài dự kiến hoặc request bị hủy sau khi claim.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("AI failed execution");
        var proposal = await AddProposalAsync(fixture.Db, user.Id, "budget_delete",
            $$"""{"budget_id":"{{Guid.NewGuid()}}"}""");
        var mediator = new RecordingMediator
        {
            Failure = cancellation ? new OperationCanceledException() : new InvalidOperationException("controlled failure")
        };
        var handler = new SpendWise.Application.Features.AiAssistant.ExecuteAiActionCommandHandler(fixture.Db, mediator, fixture.Clock);

        // Act: exception vẫn được trả cho caller, nhưng status phải được chốt an toàn.
        if (cancellation)
            await Assert.ThrowsAsync<OperationCanceledException>(() => handler.Handle(new(
                user.Id, proposal.Id, new ExecuteAiActionRequest("123456", ExplicitConfirmation: true)), default));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new(
                user.Id, proposal.Id, new ExecuteAiActionRequest("123456", ExplicitConfirmation: true)), default));

        // Assert: không báo Executed và không kẹt Executing; FailureCode không chứa chi tiết exception.
        var failed = await fixture.Db.AiActionProposals.AsNoTracking().SingleAsync(item => item.Id == proposal.Id);
        Assert.Equal(AiActionProposalStatus.Failed, failed.Status);
        Assert.Equal(expectedCode, failed.FailureCode);
        Assert.DoesNotContain("controlled", failed.FailureCode, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Current_model_maps_ai_attachments_and_single_use_proposals()
    {
        // Arrange: test host dùng SQLite nhưng vẫn dựng model EF thật.
        await using var factory = new SpendWiseApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();

        // Act: đọc metadata model thay vì phụ thuộc dữ liệu giả.
        var attachment = db.Model.FindEntityType("SpendWise.Domain.AI.AiAttachment");
        var proposal = db.Model.FindEntityType("SpendWise.Domain.AI.AiActionProposal");

        // Assert: hai bảng cuối cùng và trạng thái Executing đều thuộc model hiện hành.
        Assert.NotNull(attachment);
        Assert.NotNull(proposal);
        Assert.NotNull(proposal!.FindProperty("Status"));
        Assert.NotNull(proposal.FindProperty("OperationKey"));
        Assert.NotNull(attachment!.FindProperty("ContentHash"));
    }

    private static async Task<User> AddUserAsync(SpendWiseDbContext db, string name)
    {
        var unique = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = name,
            Email = $"{unique}@test.local",
            NormalizedEmail = $"{unique}@TEST.LOCAL",
            SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(),
            PasswordHash = "test-only-hash"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<AiActionProposal> AddProposalAsync(
        SpendWiseDbContext db,
        Guid userId,
        string actionType,
        string payload)
    {
        var proposal = new AiActionProposal
        {
            OwnerUserId = userId,
            ActionType = actionType,
            PayloadJson = payload,
            SafeSummary = $"Prepare {actionType}.",
            Status = AiActionProposalStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            OperationKey = $"test-{Guid.NewGuid():N}"
        };
        db.AiActionProposals.Add(proposal);
        await db.SaveChangesAsync();
        return proposal;
    }

    private static AiProviderRequest EmptyProviderRequest() => new(
        "User", "en-US", new AiFinancialContext(0, 0, new Dictionary<string, decimal>(), [], [], null),
        [], "Hello", [], [], (_, _) => Task.FromResult("{}"));

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "SpendWise.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class DoubleSensitiveToolProvider : IAiCompletionProvider
    {
        public string SecondResult { get; private set; } = string.Empty;

        public async Task<AiCompletionResult> CompleteAsync(AiProviderRequest request, CancellationToken cancellationToken)
        {
            var firstId = Guid.NewGuid();
            await request.ExecuteToolAsync(new AiToolCall("prepare_goal_delete", $$"""{"goal_id":"{{firstId}}"}"""), cancellationToken);
            SecondResult = await request.ExecuteToolAsync(new AiToolCall("prepare_goal_delete", $$"""{"goal_id":"{{Guid.NewGuid()}}"}"""), cancellationToken);
            return new AiCompletionResult("Prepared one action.", "ControlledTestProvider", false);
        }
    }

    private sealed class NeverCalledProvider : IAiCompletionProvider
    {
        public bool WasCalled { get; private set; }
        public Task<AiCompletionResult> CompleteAsync(AiProviderRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("Provider must not be called for a reused attachment.");
        }
    }

    private sealed class RecordingMediator : IMediator
    {
        public List<object> Requests { get; } = [];
        public Exception? Failure { get; init; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Failure is not null) return Task.FromException<TResponse>(Failure);
            var response = (TResponse)RuntimeHelpers.GetUninitializedObject(typeof(TResponse));
            return Task.FromResult(response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            Requests.Add(request);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Failure is null ? Task.FromResult<object?>(null) : Task.FromException<object?>(Failure);
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;

        public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<object?> CreateStream(
            object request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
