using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignLockedFrontendContractAndFinalAiHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "RecurringTransactions",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "Categories",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            // Gán key tất định cho dữ liệu cũ trước khi tạo unique index.
            migrationBuilder.Sql(
                """
                UPDATE [Categories]
                SET [Key] = CASE
                    WHEN [IsSystem] = 1 THEN N'legacy-system:' + LOWER(REPLACE(CONVERT(varchar(36), [Id]), '-', ''))
                    ELSE N'custom:' + LOWER(REPLACE(CONVERT(varchar(36), [Id]), '-', ''))
                END;

                -- Danh mục hệ thống cũ vẫn được giữ để bảo toàn FK lịch sử, nhưng không còn dùng cho giao dịch mới.
                UPDATE [Categories] SET [IsActive] = 0 WHERE [IsSystem] = 1;

                UPDATE [RecurringTransactions]
                SET [Title] = LEFT(
                    CASE WHEN NULLIF(LTRIM(RTRIM([Description])), N'') IS NULL
                        THEN N'Recurring transaction'
                        ELSE [Description]
                    END,
                    160);

                -- Chỉ hai cờ này hiện có runtime gate thật trong Application.
                DELETE FROM [FeatureFlags]
                WHERE [Name] NOT IN (N'P2PTransfer', N'AIAssistant');

                UPDATE [FeatureFlags]
                SET [Description] = N'Controls current BankDirect person-to-person payments.'
                WHERE [Name] = N'P2PTransfer';

                UPDATE [FeatureFlags]
                SET [Description] = N'Controls the current AI assistant and allowlisted tools.'
                WHERE [Name] = N'AIAssistant';
                """);

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Color", "CreatedAt", "Icon", "IsActive", "IsSystem", "Key", "Kind", "Name", "OwnerUserId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "#10B981", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Briefcase", true, true, "categories.salary", "Income", "Lương & Thưởng", null, null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "#0D9488", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Send", true, true, "categories.p2p", "Income", "Chuyển khoản", null, null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "#3B82F6", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "TrendingUp", true, true, "categories.investments", "Income", "Đầu tư & Sinh lời", null, null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "#6B7280", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Plus", true, true, "categories.income_other", "Income", "Thu nhập khác", null, null },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "#F59E0B", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Utensils", true, true, "categories.food", "Expense", "Ăn uống", null, null },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "#EC4899", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ShoppingBag", true, true, "categories.shopping", "Expense", "Mua sắm", null, null },
                    { new Guid("10000000-0000-0000-0000-000000000007"), "#0EA5E9", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Car", true, true, "categories.transport", "Expense", "Đi lại", null, null },
                    { new Guid("10000000-0000-0000-0000-000000000008"), "#8B5CF6", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Tv", true, true, "categories.entertainment", "Expense", "Giải trí", null, null },
                    { new Guid("10000000-0000-0000-0000-000000000009"), "#14B8A6", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Home", true, true, "categories.utilities", "Expense", "Hóa đơn & Nhà ở", null, null },
                    { new Guid("10000000-0000-0000-0000-00000000000a"), "#6366F1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GraduationCap", true, true, "categories.education", "Expense", "Giáo dục & Học tập", null, null },
                    { new Guid("10000000-0000-0000-0000-00000000000b"), "#10B981", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Target", true, true, "categories.savings_goals", "Expense", "Mục tiêu tiết kiệm", null, null },
                    { new Guid("10000000-0000-0000-0000-00000000000c"), "#6B7280", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Tag", true, true, "categories.expense_other", "Expense", "Chi tiêu khác", null, null }
                });

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "RecurringTransactions",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "Categories",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Key",
                table: "Categories",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_Key",
                table: "Categories");

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000a"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000b"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000c"));

            migrationBuilder.DropColumn(
                name: "Title",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "Categories");
        }
    }
}
