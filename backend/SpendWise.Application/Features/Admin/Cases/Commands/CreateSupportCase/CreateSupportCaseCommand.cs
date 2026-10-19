using MediatR;
using SpendWise.Application.Admin;

namespace SpendWise.Application.Features.Admin.Cases.Commands.CreateSupportCase;

// Command mở yêu cầu hỗ trợ gắn với người dùng và nội dung cần xử lý.

public sealed record CreateSupportCaseCommand(
    Guid UserId,
    CreateSupportCaseRequest Request) : IRequest<CaseDto>;
