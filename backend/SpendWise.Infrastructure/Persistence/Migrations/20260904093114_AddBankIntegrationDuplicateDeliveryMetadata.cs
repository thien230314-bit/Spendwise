using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankIntegrationDuplicateDeliveryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DuplicateDeliveryCount",
                table: "BankIntegrationEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDuplicateReceivedAt",
                table: "BankIntegrationEvents",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DuplicateDeliveryCount",
                table: "BankIntegrationEvents");

            migrationBuilder.DropColumn(
                name: "LastDuplicateReceivedAt",
                table: "BankIntegrationEvents");
        }
    }
}
