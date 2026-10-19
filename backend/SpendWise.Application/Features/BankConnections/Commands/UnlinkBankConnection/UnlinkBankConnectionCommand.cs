using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankConnections.Commands.UnlinkBankConnection;

// Yêu cầu provider mở Hosted Link hủy liên kết, chưa xóa dữ liệu cục bộ.
public sealed record UnlinkBankConnectionCommand(
    Guid UserId,
    Guid BankConnectionId,
    CreateBankLinkRequest Request,
    bool IsDevelopment = true) : IRequest<BankLinkSessionDto>;
