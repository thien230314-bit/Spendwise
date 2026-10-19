using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Wallets.Queries.GetWallets;

// Query lấy các ví thuộc người dùng hiện tại.

public sealed record GetWalletsQuery(Guid UserId) : IRequest<IReadOnlyList<WalletDto>>;
