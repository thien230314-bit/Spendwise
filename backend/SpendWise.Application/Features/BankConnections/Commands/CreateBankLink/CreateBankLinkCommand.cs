using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankConnections.Commands.CreateBankLink;

// Tạo Hosted Link cho chính người dùng đang đăng nhập.
public sealed record CreateBankLinkCommand(
    Guid UserId,
    CreateBankLinkRequest Request) : IRequest<BankLinkSessionDto>;
