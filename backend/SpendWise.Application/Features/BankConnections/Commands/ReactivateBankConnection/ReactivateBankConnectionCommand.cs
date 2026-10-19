using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankConnections.Commands.ReactivateBankConnection;

// Tạo Hosted Link khôi phục một connection đang Inactive.
public sealed record ReactivateBankConnectionCommand(
    Guid UserId,
    Guid BankConnectionId,
    CreateBankLinkRequest Request) : IRequest<BankLinkSessionDto>;
