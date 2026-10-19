using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Wallets.Queries.GetWallets;

// Handler lấy các ví thuộc người dùng hiện tại.

public sealed class GetWalletsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetWalletsQuery, IReadOnlyList<WalletDto>>
{
    public async Task<IReadOnlyList<WalletDto>> Handle(
        GetWalletsQuery query,
        CancellationToken cancellationToken)
    {
        // Chỉ lấy các ví thuộc người dùng và ưu tiên ví mặc định trong kết quả.
        var wallets = await db.Wallets
            .AsNoTracking()
            .Where(wallet => wallet.OwnerUserId == query.UserId)
            .OrderByDescending(wallet => wallet.IsDefault)
            .ThenBy(wallet => wallet.CreatedAt)
            .ToListAsync(cancellationToken);

        return wallets.Select(WalletMappings.ToDto).ToList();
    }
}
