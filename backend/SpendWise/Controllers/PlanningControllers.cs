using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Budgets.Commands.CreateBudget;
using SpendWise.Application.Features.Budgets.Commands.DeleteBudget;
using SpendWise.Application.Features.Budgets.Commands.UpdateBudget;
using SpendWise.Application.Features.Budgets.Queries.GetBudgetById;
using SpendWise.Application.Features.Budgets.Queries.GetBudgets;
using SpendWise.Application.Features.Goals.Commands.ContributeGoal;
using SpendWise.Application.Features.Goals.Commands.CreateGoal;
using SpendWise.Application.Features.Goals.Commands.DeleteGoal;
using SpendWise.Application.Features.Goals.Commands.UpdateGoal;
using SpendWise.Application.Features.Goals.Commands.WithdrawGoal;
using SpendWise.Application.Features.Goals.Queries.GetGoalById;
using SpendWise.Application.Features.Goals.Queries.GetGoals;
using SpendWise.Application.Features.RecurringTransactions.Commands.CreateRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.DeleteRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.UpdateRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.PauseRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.ResumeRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.RecordRecurringOccurrence;
using SpendWise.Application.Features.RecurringTransactions.Queries.GetRecurringTransactionById;
using SpendWise.Application.Features.RecurringTransactions.Queries.GetRecurringTransactions;
using SpendWise.Application.Features.TransactionRules.Commands.CreateTransactionRule;
using SpendWise.Application.Features.TransactionRules.Commands.DeleteTransactionRule;
using SpendWise.Application.Features.TransactionRules.Commands.UpdateTransactionRule;
using SpendWise.Application.Features.TransactionRules.Queries.GetTransactionRuleById;
using SpendWise.Application.Features.TransactionRules.Queries.GetTransactionRules;
using SpendWise.Application.Planning;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize(Policy = "User")]
[Route("api/budgets")]
public sealed class BudgetsController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<BudgetDto>> List(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct)
    {
        // Lấy UserId từ access token rồi gửi Query sang Application qua MediatR.
        var userId = UserId();
        var query = new GetBudgetsQuery(userId, year, month);

        return await mediator.Send(query, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<BudgetDto> Get(Guid id, CancellationToken ct)
    {
        // Query luôn kèm UserId để Handler kiểm tra quyền sở hữu.
        return await mediator.Send(new GetBudgetByIdQuery(UserId(), id), ct);
    }

    [HttpPost]
    public async Task<ActionResult<BudgetDto>> Create(
        CreateBudgetRequest request,
        CancellationToken ct)
    {
        // Gửi Command tạo mới và trả địa chỉ tài nguyên vừa tạo.
        var result = await mediator.Send(new CreateBudgetCommand(UserId(), request), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut]
    public async Task<BudgetDto> CreateOrUpdateCompatible(
        CreateBudgetRequest request,
        CancellationToken ct)
    {
        // Giữ endpoint PUT cũ để frontend hiện tại không bị phá vỡ.
        var userId = UserId();
        var existing = (await mediator.Send(
                new GetBudgetsQuery(userId, request.Year, request.Month),
                ct))
            .SingleOrDefault(budget => budget.CategoryId == request.CategoryId);

        // Endpoint tương thích chỉ điều phối sang Create hoặc Update rõ nghĩa.
        return existing is null
            ? await mediator.Send(new CreateBudgetCommand(userId, request), ct)
            : await mediator.Send(
                new UpdateBudgetCommand(
                    userId,
                    existing.Id,
                    new UpdateBudgetRequest(request.LimitAmount, request.AlertsEnabled, request.Rollover, request.SecurityPin)),
                ct);
    }

    [HttpPut("{id:guid}")]
    public async Task<BudgetDto> Update(
        Guid id,
        UpdateBudgetRequest request,
        CancellationToken ct)
    {
        // Chuyển request sửa ngân sách thành Command cho Handler xử lý.
        return await mediator.Send(new UpdateBudgetCommand(UserId(), id, request), ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, DeleteBudgetRequest request, CancellationToken ct)
    {
        // DeleteBudget chỉ xóa cấu hình ngân sách, không xóa giao dịch liên quan.
        var userId = UserId();
        var command = new DeleteBudgetCommand(userId, id, request.SecurityPin);
        await mediator.Send(command, ct);

        return NoContent();
    }

    private Guid UserId()
    {
        // UserId luôn lấy từ phiên đăng nhập, không lấy từ request body.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}

[ApiController]
[Authorize(Policy = "User")]
[Route("api/goals")]
public sealed class GoalsController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<GoalDto>> List(CancellationToken ct)
    {
        // Lấy toàn bộ mục tiêu thuộc người dùng hiện tại.
        var userId = UserId();
        var query = new GetGoalsQuery(userId);

        return await mediator.Send(query, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<GoalDto> Get(Guid id, CancellationToken ct)
    {
        // Handler kiểm tra Goal thuộc UserId trước khi trả dữ liệu.
        return await mediator.Send(new GetGoalByIdQuery(UserId(), id), ct);
    }

    [HttpPost]
    public async Task<GoalDto> Create(
        CreateGoalRequest request,
        CancellationToken ct)
    {
        // Gửi dữ liệu tạo mục tiêu sang Application qua MediatR.
        var userId = UserId();
        var command = new CreateGoalCommand(userId, request);

        return await mediator.Send(command, ct);
    }

    [HttpPut("{id:guid}")]
    public async Task<GoalDto> Update(
        Guid id,
        UpdateGoalRequest request,
        CancellationToken ct)
    {
        // UpdateGoal chỉ sửa metadata và số tiền mục tiêu, không sửa tiền đã phân bổ.
        return await mediator.Send(new UpdateGoalCommand(UserId(), id, request), ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, DeleteGoalRequest request, CancellationToken ct)
    {
        // Handler sẽ từ chối nếu Goal còn tiền hoặc đã có lịch sử tài chính.
        await mediator.Send(new DeleteGoalCommand(UserId(), id, request.SecurityPin), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/contributions")]
    [EnableRateLimiting("financial")]
    public async Task<GoalDto> Contribute(
        Guid id,
        GoalMovementRequest request,
        CancellationToken ct)
    {
        // Tiền Goal chỉ tăng qua nghiệp vụ đóng góp có kiểm soát.
        var userId = UserId();
        var command = new ContributeGoalCommand(userId, id, request);

        return await mediator.Send(command, ct);
    }

    [HttpPost("{id:guid}/withdrawals")]
    [EnableRateLimiting("financial")]
    public async Task<GoalDto> Withdraw(
        Guid id,
        GoalMovementRequest request,
        CancellationToken ct)
    {
        // Tiền Goal chỉ giảm qua nghiệp vụ rút có kiểm soát.
        var userId = UserId();
        var command = new WithdrawGoalCommand(userId, id, request);

        return await mediator.Send(command, ct);
    }

    private Guid UserId()
    {
        // Không tin UserId do client tự gửi.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}

[ApiController]
[Authorize]
[Route("api/recurring-transactions")]
public sealed class RecurringTransactionsController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<RecurringDto>> List(CancellationToken ct)
    {
        // Lấy các định nghĩa định kỳ của người dùng hiện tại.
        var userId = UserId();
        var query = new GetRecurringTransactionsQuery(userId);

        return await mediator.Send(query, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<RecurringDto> Get(Guid id, CancellationToken ct)
    {
        // Query kèm UserId để bảo vệ quyền sở hữu.
        return await mediator.Send(new GetRecurringTransactionByIdQuery(UserId(), id), ct);
    }

    [HttpPost]
    public async Task<RecurringDto> Create(
        CreateRecurringRequest request,
        CancellationToken ct)
    {
        // Tạo lịch chạy mới; Handler kiểm tra ví và danh mục thuộc phạm vi hợp lệ.
        var userId = UserId();
        var command = new CreateRecurringTransactionCommand(userId, request);

        return await mediator.Send(command, ct);
    }

    [HttpPut("{id:guid}")]
    public async Task<RecurringDto> Update(
        Guid id,
        UpdateRecurringRequest request,
        CancellationToken ct)
    {
        // Chỉ sửa cấu hình cho lần chạy tương lai.
        return await mediator.Send(new UpdateRecurringTransactionCommand(UserId(), id, request), ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        // Xóa lịch định kỳ nhưng giữ nguyên các giao dịch đã được tạo.
        await mediator.Send(new DeleteRecurringTransactionCommand(UserId(), id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/pause")]
    public Task<RecurringDto> Pause(Guid id, CancellationToken ct) =>
        mediator.Send(new PauseRecurringTransactionCommand(UserId(), id), ct);

    [HttpPost("{id:guid}/resume")]
    public Task<RecurringDto> Resume(Guid id, CancellationToken ct) =>
        mediator.Send(new ResumeRecurringTransactionCommand(UserId(), id), ct);

    [HttpPost("{id:guid}/occurrences/{occurrenceId:guid}/record")]
    [EnableRateLimiting("financial")]
    public Task<SpendWise.Application.Finance.TransactionDto> Record(
        Guid id,
        Guid occurrenceId,
        RecordRecurringOccurrenceRequest request,
        CancellationToken ct) =>
        mediator.Send(new RecordRecurringOccurrenceCommand(UserId(), id, occurrenceId, request), ct);

    private Guid UserId()
    {
        // UserId được lấy từ access token đã xác thực.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}

[ApiController]
[Authorize]
[Route("api/transaction-rules")]
public sealed class TransactionRulesController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<TransactionRuleDto>> List(CancellationToken ct)
    {
        // Lấy các quy tắc phân loại thuộc người dùng hiện tại.
        var userId = UserId();
        var query = new GetTransactionRulesQuery(userId);

        return await mediator.Send(query, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<TransactionRuleDto> Get(Guid id, CancellationToken ct)
    {
        // Query chỉ trả quy tắc nếu UserId là chủ sở hữu.
        return await mediator.Send(new GetTransactionRuleByIdQuery(UserId(), id), ct);
    }

    [HttpPost]
    public async Task<TransactionRuleDto> Create(
        CreateTransactionRuleRequest request,
        CancellationToken ct)
    {
        // Gửi Command tạo quy tắc sang Application.
        var userId = UserId();
        var command = new CreateTransactionRuleCommand(userId, request);

        return await mediator.Send(command, ct);
    }

    [HttpPut("{id:guid}")]
    public async Task<TransactionRuleDto> Update(
        Guid id,
        UpdateTransactionRuleRequest request,
        CancellationToken ct)
    {
        // Handler kiểm tra quyền sở hữu và danh mục đích trước khi lưu.
        return await mediator.Send(new UpdateTransactionRuleCommand(UserId(), id, request), ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        // Transaction Rule là cấu hình nên có thể xóa an toàn.
        await mediator.Send(new DeleteTransactionRuleCommand(UserId(), id), ct);
        return NoContent();
    }

    private Guid UserId()
    {
        // Không nhận UserId từ body để tránh sửa dữ liệu của người khác.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}
