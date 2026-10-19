using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Commands.ProcessBankTransactionIpn;

// Xử lý IPN đã được Controller xác thực bằng Authorization: Apikey.
public sealed record ProcessBankTransactionIpnCommand(
    SePayBankTransactionIpnRequest Request) : IRequest<IntegrationAcknowledgementDto>;
