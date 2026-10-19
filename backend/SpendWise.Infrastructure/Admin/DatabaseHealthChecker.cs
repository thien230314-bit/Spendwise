using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Infrastructure.Admin;

// Kiểm tra khả năng kết nối SQL Server cho health check.

public sealed class DatabaseHealthChecker(
    SpendWiseDbContext db) : IDatabaseHealthChecker
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        // Kiểm tra SQL Server có phản hồi để health check báo trạng thái chính xác.
        return db.Database.CanConnectAsync(cancellationToken);
    }
}
