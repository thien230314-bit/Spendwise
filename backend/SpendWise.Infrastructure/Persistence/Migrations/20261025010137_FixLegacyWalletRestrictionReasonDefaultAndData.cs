using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixLegacyWalletRestrictionReasonDefaultAndData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RestrictionReason",
                table: "Wallets",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            // Cập nhật dữ liệu legacy: ví Active và Closed có RestrictionReason rỗng được chuyển về 'None'.
            // Nếu có ví legacy đang Restricted mà thiếu lý do, gán 'FinancialRestriction' để bảo toàn trạng thái khóa an toàn.
            migrationBuilder.Sql(@"
                UPDATE [Wallets]
                SET [RestrictionReason] = 'None'
                WHERE [RestrictionReason] = '' OR [RestrictionReason] IS NULL;

                UPDATE [Wallets]
                SET [RestrictionReason] = 'FinancialRestriction'
                WHERE [Status] = 'Restricted' AND ([RestrictionReason] = '' OR [RestrictionReason] = 'None' OR [RestrictionReason] IS NULL);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_ProviderTransactionId",
                table: "WalletWithdrawals",
                column: "ProviderTransactionId",
                unique: true,
                filter: "[ProviderTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletWithdrawals_ProviderTransactionId",
                table: "WalletWithdrawals");

            migrationBuilder.AlterColumn<string>(
                name: "RestrictionReason",
                table: "Wallets",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldDefaultValue: "None");
        }
    }
}
