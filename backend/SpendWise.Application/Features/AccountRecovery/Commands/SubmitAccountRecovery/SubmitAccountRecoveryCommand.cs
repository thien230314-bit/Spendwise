using MediatR;
using SpendWise.Application.AccountRecovery;

namespace SpendWise.Application.Features.AccountRecovery.Commands.SubmitAccountRecovery;

public sealed record SubmitAccountRecoveryCommand(SubmitAccountRecoveryRequest Request)
    : IRequest<AccountRecoverySubmissionResponse>;
