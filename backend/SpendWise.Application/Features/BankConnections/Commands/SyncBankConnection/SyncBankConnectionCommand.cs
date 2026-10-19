using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankConnections.Commands.SyncBankConnection;

// Đồng bộ giao dịch ngoài cho connection Active thuộc người dùng.
public sealed record SyncBankConnectionCommand(Guid UserId, Guid BankConnectionId) : IRequest<BankSyncDto>;
