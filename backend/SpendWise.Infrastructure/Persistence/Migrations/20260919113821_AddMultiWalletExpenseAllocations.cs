using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiWalletExpenseAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_Wallets_WalletId",
                table: "FinancialTransactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "OriginalWalletId",
                table: "TransactionCorrections",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CorrectedWalletId",
                table: "TransactionCorrections",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletId",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateTable(
                name: "TransactionCorrectionWalletAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionCorrectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsOriginal = table.Column<bool>(type: "bit", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionCorrectionWalletAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionCorrectionWalletAllocations_TransactionCorrections_TransactionCorrectionId",
                        column: x => x.TransactionCorrectionId,
                        principalTable: "TransactionCorrections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionCorrectionWalletAllocations_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionWalletAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionWalletAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionWalletAllocations_FinancialTransactions_FinancialTransactionId",
                        column: x => x.FinancialTransactionId,
                        principalTable: "FinancialTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionWalletAllocations_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrectionWalletAllocations_TransactionCorrectionId_IsOriginal_WalletId",
                table: "TransactionCorrectionWalletAllocations",
                columns: new[] { "TransactionCorrectionId", "IsOriginal", "WalletId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrectionWalletAllocations_WalletId",
                table: "TransactionCorrectionWalletAllocations",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionWalletAllocations_FinancialTransactionId_WalletId",
                table: "TransactionWalletAllocations",
                columns: new[] { "FinancialTransactionId", "WalletId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionWalletAllocations_WalletId",
                table: "TransactionWalletAllocations",
                column: "WalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_Wallets_WalletId",
                table: "FinancialTransactions",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_Wallets_WalletId",
                table: "FinancialTransactions");

            migrationBuilder.DropTable(
                name: "TransactionCorrectionWalletAllocations");

            migrationBuilder.DropTable(
                name: "TransactionWalletAllocations");

            migrationBuilder.AlterColumn<Guid>(
                name: "OriginalWalletId",
                table: "TransactionCorrections",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CorrectedWalletId",
                table: "TransactionCorrections",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletId",
                table: "FinancialTransactions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_Wallets_WalletId",
                table: "FinancialTransactions",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
