using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Wallets.Queries.GetWalletDetails;

// Handler lấy chi tiết một ví sau khi kiểm tra quyền sở hữu.

public sealed class GetWalletDetailsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetWalletDetailsQuery, WalletDto>
{
    public async Task<WalletDto> Handle(
        GetWalletDetailsQuery query,
        CancellationToken cancellationToken)
    {
        // Tìm một ví theo cả WalletId và OwnerUserId để bảo vệ quyền sở hữu.
        var wallet = await db.Wallets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == query.WalletId
                    && candidate.OwnerUserId == query.UserId,
                cancellationToken)
            ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");

        return WalletMappings.ToDto(wallet);
    }
}
