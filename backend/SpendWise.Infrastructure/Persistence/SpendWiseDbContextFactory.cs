using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SpendWise.Infrastructure.Services;

namespace SpendWise.Infrastructure.Persistence;

// Tạo DbContext cho công cụ EF bằng đúng cấu hình thiết kế.

public sealed class SpendWiseDbContextFactory : IDesignTimeDbContextFactory<SpendWiseDbContext>
{
    public SpendWiseDbContext CreateDbContext(string[] args)
    {
        // Biến riêng cho EF cho phép kiểm tra database tạm mà không đụng database thiết kế mặc định.
        var connectionString = Environment.GetEnvironmentVariable("SPENDWISE_EF_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=SpendWiseDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new SpendWiseDbContext(options, new SystemClock());
    }
}
