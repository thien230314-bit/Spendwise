using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Commands.ProcessLifecycleEvent;

// Xử lý lifecycle event đã được Controller xác thực bằng X-Secret-Key.
public sealed record ProcessLifecycleEventCommand(
    SePayLifecycleEventRequest Request) : IRequest<IntegrationAcknowledgementDto>;
