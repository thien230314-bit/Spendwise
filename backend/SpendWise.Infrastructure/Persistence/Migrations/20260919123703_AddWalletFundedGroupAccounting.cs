using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletFundedGroupAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DestinationWalletId",
                table: "GroupRefunds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "GroupRefunds",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LedgerTransactionId",
                table: "GroupRefunds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LedgerTransactionId",
                table: "FundTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WalletId",
                table: "FundTransactions",
                type: "uniqueidentifier",
                nullable: true);

            // GroupContributions đã được tạo từ InitialCreate và được giữ lại khi retire runtime cũ.
            migrationBuilder.DropIndex(
                name: "IX_GroupContributions_GroupId_UserId_IdempotencyKey",
                table: "GroupContributions");

            migrationBuilder.CreateTable(
                name: "GroupExpenseShareAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupExpenseShareAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupExpenseShareAllocations_GroupExpenses_GroupExpenseId",
                        column: x => x.GroupExpenseId,
                        principalTable: "GroupExpenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupExpenseShareAllocations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupFundShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupFundShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupFundShares_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupFundShares_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupRefunds_DestinationWalletId",
                table: "GroupRefunds",
                column: "DestinationWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupRefunds_LedgerTransactionId",
                table: "GroupRefunds",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_LedgerTransactionId",
                table: "FundTransactions",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_WalletId",
                table: "FundTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContributions_GroupId",
                table: "GroupContributions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContributions_LedgerTransactionId",
                table: "GroupContributions",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContributions_UserId_IdempotencyKey",
                table: "GroupContributions",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupContributions_WalletId",
                table: "GroupContributions",
                column: "WalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupContributions_Groups_GroupId",
                table: "GroupContributions",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupContributions_LedgerTransactions_LedgerTransactionId",
                table: "GroupContributions",
                column: "LedgerTransactionId",
                principalTable: "LedgerTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupContributions_Users_UserId",
                table: "GroupContributions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupContributions_Wallets_WalletId",
                table: "GroupContributions",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_GroupExpenseShareAllocations_GroupExpenseId_UserId",
                table: "GroupExpenseShareAllocations",
                columns: new[] { "GroupExpenseId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupExpenseShareAllocations_UserId",
                table: "GroupExpenseShareAllocations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFundShares_GroupId_UserId",
                table: "GroupFundShares",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupFundShares_UserId",
                table: "GroupFundShares",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FundTransactions_LedgerTransactions_LedgerTransactionId",
                table: "FundTransactions",
                column: "LedgerTransactionId",
                principalTable: "LedgerTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FundTransactions_Wallets_WalletId",
                table: "FundTransactions",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupRefunds_LedgerTransactions_LedgerTransactionId",
                table: "GroupRefunds",
                column: "LedgerTransactionId",
                principalTable: "LedgerTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupRefunds_Wallets_DestinationWalletId",
                table: "GroupRefunds",
                column: "DestinationWalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupContributions_Groups_GroupId",
                table: "GroupContributions");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupContributions_LedgerTransactions_LedgerTransactionId",
                table: "GroupContributions");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupContributions_Users_UserId",
                table: "GroupContributions");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupContributions_Wallets_WalletId",
                table: "GroupContributions");
            migrationBuilder.DropForeignKey(
                name: "FK_FundTransactions_LedgerTransactions_LedgerTransactionId",
                table: "FundTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FundTransactions_Wallets_WalletId",
                table: "FundTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupRefunds_LedgerTransactions_LedgerTransactionId",
                table: "GroupRefunds");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupRefunds_Wallets_DestinationWalletId",
                table: "GroupRefunds");

            migrationBuilder.DropTable(
                name: "GroupExpenseShareAllocations");

            migrationBuilder.DropTable(
                name: "GroupFundShares");

            migrationBuilder.DropIndex(
                name: "IX_GroupRefunds_DestinationWalletId",
                table: "GroupRefunds");

            migrationBuilder.DropIndex(
                name: "IX_GroupRefunds_LedgerTransactionId",
                table: "GroupRefunds");

            migrationBuilder.DropIndex(
                name: "IX_FundTransactions_LedgerTransactionId",
                table: "FundTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FundTransactions_WalletId",
                table: "FundTransactions");

            migrationBuilder.DropIndex(
                name: "IX_GroupContributions_GroupId",
                table: "GroupContributions");

            migrationBuilder.DropIndex(
                name: "IX_GroupContributions_LedgerTransactionId",
                table: "GroupContributions");

            migrationBuilder.DropIndex(
                name: "IX_GroupContributions_UserId_IdempotencyKey",
                table: "GroupContributions");

            migrationBuilder.DropIndex(
                name: "IX_GroupContributions_WalletId",
                table: "GroupContributions");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContributions_GroupId_UserId_IdempotencyKey",
                table: "GroupContributions",
                columns: new[] { "GroupId", "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.DropColumn(
                name: "DestinationWalletId",
                table: "GroupRefunds");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "GroupRefunds");

            migrationBuilder.DropColumn(
                name: "LedgerTransactionId",
                table: "GroupRefunds");

            migrationBuilder.DropColumn(
                name: "LedgerTransactionId",
                table: "FundTransactions");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "FundTransactions");
        }
    }
}
