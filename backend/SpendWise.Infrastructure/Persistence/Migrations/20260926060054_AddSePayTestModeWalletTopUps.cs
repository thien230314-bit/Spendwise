using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSePayTestModeWalletTopUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankConnections_Provider_ExternalAccountId",
                table: "BankConnections");

            migrationBuilder.DropIndex(
                name: "IX_BankLinkSessions_Provider_ExternalLinkTokenXid",
                table: "BankLinkSessions");

            migrationBuilder.AddColumn<string>(
                name: "AccountEnvironment",
                table: "BankLinkSessions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "BankHub");

            migrationBuilder.AddColumn<string>(
                name: "AccountEnvironment",
                table: "BankConnections",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "BankHub");

            migrationBuilder.CreateTable(
                name: "WalletTopUps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LedgerTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinancialTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTopUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTopUps_BankConnections_BankConnectionId",
                        column: x => x.BankConnectionId,
                        principalTable: "BankConnections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletTopUps_FinancialTransactions_FinancialTransactionId",
                        column: x => x.FinancialTransactionId,
                        principalTable: "FinancialTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletTopUps_LedgerTransactions_LedgerTransactionId",
                        column: x => x.LedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletTopUps_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletTopUps_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_Provider_AccountEnvironment_ExternalAccountId",
                table: "BankConnections",
                columns: new[] { "Provider", "AccountEnvironment", "ExternalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankLinkSessions_Provider_AccountEnvironment_ExternalLinkTokenXid",
                table: "BankLinkSessions",
                columns: new[] { "Provider", "AccountEnvironment", "ExternalLinkTokenXid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_BankConnectionId",
                table: "WalletTopUps",
                column: "BankConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_FinancialTransactionId",
                table: "WalletTopUps",
                column: "FinancialTransactionId",
                unique: true,
                filter: "[FinancialTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_LedgerTransactionId",
                table: "WalletTopUps",
                column: "LedgerTransactionId",
                unique: true,
                filter: "[LedgerTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_ProviderTransactionId",
                table: "WalletTopUps",
                column: "ProviderTransactionId",
                unique: true,
                filter: "[ProviderTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_Reference",
                table: "WalletTopUps",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_UserId",
                table: "WalletTopUps",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_WalletId",
                table: "WalletTopUps",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletTopUps");

            migrationBuilder.DropIndex(
                name: "IX_BankConnections_Provider_AccountEnvironment_ExternalAccountId",
                table: "BankConnections");

            migrationBuilder.DropIndex(
                name: "IX_BankLinkSessions_Provider_AccountEnvironment_ExternalLinkTokenXid",
                table: "BankLinkSessions");

            migrationBuilder.DropColumn(
                name: "AccountEnvironment",
                table: "BankLinkSessions");

            migrationBuilder.DropColumn(
                name: "AccountEnvironment",
                table: "BankConnections");

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_Provider_ExternalAccountId",
                table: "BankConnections",
                columns: new[] { "Provider", "ExternalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankLinkSessions_Provider_ExternalLinkTokenXid",
                table: "BankLinkSessions",
                columns: new[] { "Provider", "ExternalLinkTokenXid" },
                unique: true);
        }
    }
}
