using MediatR;
using SpendWise.Application.AccountRecovery;

namespace SpendWise.Application.Features.AccountRecovery.Commands.ReviewAccountRecovery;

public sealed record RequestMoreAccountRecoveryInfoCommand(
    Guid AdminId, Guid RequestId, AccountRecoveryAdminActionRequest Request)
    : IRequest<AccountRecoveryDto>;

public sealed record RejectAccountRecoveryCommand(
    Guid AdminId, Guid RequestId, AccountRecoveryAdminActionRequest Request)
    : IRequest<AccountRecoveryDto>;

public sealed record ApproveAccountRecoveryCommand(
    Guid AdminId, Guid RequestId, ApproveAccountRecoveryRequest Request)
    : IRequest<AccountRecoveryApprovalResponse>;
