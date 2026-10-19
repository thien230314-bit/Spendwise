using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Categories.Commands.ArchiveCategory;
using SpendWise.Application.Features.Categories.Commands.CreateCategory;
using SpendWise.Application.Features.Categories.Commands.UpdateCategory;
using SpendWise.Application.Features.Categories.Queries.GetCategoryById;
using SpendWise.Application.Features.Categories.Queries.GetCategories;
using SpendWise.Application.Finance;

namespace SpendWise.API.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<CategoryDto>> List(
        [FromQuery] string? kind,
        [FromQuery] bool includeArchived,
        CancellationToken ct)
    {
        // Lấy UserId từ access token rồi gửi Query qua MediatR.
        var userId = UserId();
        var query = new GetCategoriesQuery(userId, kind, includeArchived);

        return await mediator.Send(query, ct);
    }

    [HttpGet("{categoryId:guid}")]
    public async Task<CategoryDto> Get(Guid categoryId, CancellationToken ct)
    {
        // Handler chỉ trả danh mục hệ thống hoặc danh mục của người dùng hiện tại.
        return await mediator.Send(new GetCategoryByIdQuery(UserId(), categoryId), ct);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(
        CreateCategoryRequest request,
        CancellationToken ct)
    {
        // Controller chỉ tạo Command; quy tắc trùng tên được xử lý ở Handler.
        var userId = UserId();
        var command = new CreateCategoryCommand(userId, request);
        var result = await mediator.Send(command, ct);

        return Created($"/api/categories/{result.Id}", result);
    }

    [HttpPut("{categoryId:guid}")]
    public async Task<CategoryDto> Update(
        Guid categoryId,
        UpdateCategoryRequest request,
        CancellationToken ct)
    {
        // Handler cho phép sửa metadata/loại khi không xung đột với lịch định kỳ đang chạy.
        return await mediator.Send(new UpdateCategoryCommand(UserId(), categoryId, request), ct);
    }

    [HttpDelete("{categoryId:guid}")]
    public async Task<IActionResult> Archive(
        Guid categoryId,
        CancellationToken ct)
    {
        // Danh mục đã được tham chiếu sẽ archive; danh mục chưa dùng có thể xóa hẳn.
        var userId = UserId();
        var command = new ArchiveCategoryCommand(userId, categoryId);
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    private Guid UserId()
    {
        // UserId luôn lấy từ phiên đăng nhập đã xác thực.
        return currentUser.UserId ?? throw new UnauthorizedAccessException();
    }
}
