using MediatR;

namespace SpendWise.Application.Features.Groups.Commands.CloseGroup;

// Command đóng nhóm theo vòng đời thay vì xóa dữ liệu và lịch sử liên quan.

public sealed record CloseGroupCommand(Guid OwnerId, Guid GroupId) : IRequest;
