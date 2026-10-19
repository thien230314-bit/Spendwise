using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenSePayTestModeTopUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTopUps_BankConnectionId",
                table: "WalletTopUps");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_BankConnectionId",
                table: "WalletTopUps",
                column: "BankConnectionId",
                unique: true,
                filter: "[Status] IN ('Pending', 'Processing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTopUps_BankConnectionId",
                table: "WalletTopUps");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_BankConnectionId",
                table: "WalletTopUps",
                column: "BankConnectionId");
        }
    }
}
