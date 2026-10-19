using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminA1OperationalReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "SupportCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_AssignedAdminId_Status_DueAt_CreatedAt",
                table: "SupportCases",
                columns: new[] { "AssignedAdminId", "Status", "DueAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_CreatedAt_Status",
                table: "LedgerTransactions",
                columns: new[] { "CreatedAt", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportCases_AssignedAdminId_Status_DueAt_CreatedAt",
                table: "SupportCases");

            migrationBuilder.DropIndex(
                name: "IX_LedgerTransactions_CreatedAt_Status",
                table: "LedgerTransactions");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "SupportCases");
        }
    }
}
