using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations;

/// <summary>
/// Production EF no longer maps the retired financial modules.
/// The migration is intentionally empty so their physical history tables are preserved.
/// </summary>
public partial class RetireLegacyFinancialRuntimeMappings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Không DropTable: dữ liệu legacy chỉ được lưu trữ, không còn được runtime hiện tại sử dụng.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Không cần tạo lại bảng vì Up chưa xóa các bảng lịch sử.
    }
}
