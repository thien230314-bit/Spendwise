using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteFinancialSafetyAndWalletWithdrawalReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletWithdrawals_BankConnectionId",
                table: "WalletWithdrawals");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WalletWithdrawals",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "WalletWithdrawals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderResponseData",
                table: "WalletWithdrawals",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationLedgerTransactionId",
                table: "WalletWithdrawals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReservedAt",
                table: "WalletWithdrawals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictionReason",
                table: "Wallets",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_BankConnectionId",
                table: "WalletWithdrawals",
                column: "BankConnectionId",
                unique: true,
                filter: "[Status] IN ('Pending', 'Reserved', 'Processing', 'PendingReconciliation')");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_ReservationLedgerTransactionId",
                table: "WalletWithdrawals",
                column: "ReservationLedgerTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletWithdrawals_LedgerTransactions_ReservationLedgerTransactionId",
                table: "WalletWithdrawals",
                column: "ReservationLedgerTransactionId",
                principalTable: "LedgerTransactions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletWithdrawals_LedgerTransactions_ReservationLedgerTransactionId",
                table: "WalletWithdrawals");

            migrationBuilder.DropIndex(
                name: "IX_WalletWithdrawals_BankConnectionId",
                table: "WalletWithdrawals");

            migrationBuilder.DropIndex(
                name: "IX_WalletWithdrawals_ReservationLedgerTransactionId",
                table: "WalletWithdrawals");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "WalletWithdrawals");

            migrationBuilder.DropColumn(
                name: "ProviderResponseData",
                table: "WalletWithdrawals");

            migrationBuilder.DropColumn(
                name: "ReservationLedgerTransactionId",
                table: "WalletWithdrawals");

            migrationBuilder.DropColumn(
                name: "ReservedAt",
                table: "WalletWithdrawals");

            migrationBuilder.DropColumn(
                name: "RestrictionReason",
                table: "Wallets");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WalletWithdrawals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_BankConnectionId",
                table: "WalletWithdrawals",
                column: "BankConnectionId",
                unique: true,
                filter: "[Status] IN ('Pending', 'Processing')");
        }
    }
}
