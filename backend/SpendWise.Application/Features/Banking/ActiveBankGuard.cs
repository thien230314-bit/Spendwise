using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.Banking;

public interface IActiveBankGuard
{
    Task EnsureActiveBankAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveBankAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class ActiveBankGuard(ISpendWiseDbContext db) : IActiveBankGuard
{
    public async Task EnsureActiveBankAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var hasActive = await HasActiveBankAsync(userId, cancellationToken);
        if (!hasActive)
        {
            throw new ConflictException(
                "active_bank_required",
                "Cần có tài khoản ngân hàng đang hoạt động để mở hoặc sử dụng ví SpendWise.");
        }
    }

    public Task<bool> HasActiveBankAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return db.BankConnections.AsNoTracking()
            .AnyAsync(b => b.UserId == userId && b.Status == BankConnectionStatus.Active, cancellationToken);
    }
}
