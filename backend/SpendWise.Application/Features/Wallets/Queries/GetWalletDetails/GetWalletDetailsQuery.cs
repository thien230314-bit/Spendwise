using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Wallets.Queries.GetWalletDetails;

// Query lấy chi tiết một ví sau khi kiểm tra quyền sở hữu.

public sealed record GetWalletDetailsQuery(
    Guid UserId,
    Guid WalletId) : IRequest<WalletDto>;
