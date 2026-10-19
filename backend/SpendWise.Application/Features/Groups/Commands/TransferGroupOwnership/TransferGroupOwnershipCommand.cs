using MediatR;

namespace SpendWise.Application.Features.Groups.Commands.TransferGroupOwnership;

// Command chuyển quyền sở hữu nhóm cho một thành viên hợp lệ.

public sealed record TransferGroupOwnershipCommand(
    Guid OwnerId,
    Guid GroupId,
    Guid MemberUserId) : IRequest;
