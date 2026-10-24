using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankLinkingUnlinkAndWalletWithdrawal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankConnections_Provider_AccountEnvironment_ExternalAccountId",
                table: "BankConnections");

            migrationBuilder.DropIndex(
                name: "IX_BankConnections_UserId",
                table: "BankConnections");

            migrationBuilder.CreateTable(
                name: "WalletWithdrawals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LedgerTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinancialTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletWithdrawals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletWithdrawals_BankConnections_BankConnectionId",
                        column: x => x.BankConnectionId,
                        principalTable: "BankConnections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletWithdrawals_FinancialTransactions_FinancialTransactionId",
                        column: x => x.FinancialTransactionId,
                        principalTable: "FinancialTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletWithdrawals_LedgerTransactions_LedgerTransactionId",
                        column: x => x.LedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletWithdrawals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletWithdrawals_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_Provider_AccountEnvironment_ExternalAccountId",
                table: "BankConnections",
                columns: new[] { "Provider", "AccountEnvironment", "ExternalAccountId" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_UserId",
                table: "BankConnections",
                column: "UserId",
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_BankConnectionId",
                table: "WalletWithdrawals",
                column: "BankConnectionId",
                unique: true,
                filter: "[Status] IN ('Pending', 'Processing')");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_FinancialTransactionId",
                table: "WalletWithdrawals",
                column: "FinancialTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_LedgerTransactionId",
                table: "WalletWithdrawals",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_Reference",
                table: "WalletWithdrawals",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_UserId",
                table: "WalletWithdrawals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_UserId_IdempotencyKey",
                table: "WalletWithdrawals",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_WalletId",
                table: "WalletWithdrawals",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletWithdrawals");

            migrationBuilder.DropIndex(
                name: "IX_BankConnections_Provider_AccountEnvironment_ExternalAccountId",
                table: "BankConnections");

            migrationBuilder.DropIndex(
                name: "IX_BankConnections_UserId",
                table: "BankConnections");

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_Provider_AccountEnvironment_ExternalAccountId",
                table: "BankConnections",
                columns: new[] { "Provider", "AccountEnvironment", "ExternalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_UserId",
                table: "BankConnections",
                column: "UserId");
        }
    }
}
