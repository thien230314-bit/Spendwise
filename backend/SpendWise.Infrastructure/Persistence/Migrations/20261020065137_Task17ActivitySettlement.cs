using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Task17ActivitySettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(name: "StartedRevision", table: "V1ActivityProposals", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<long>(
                name: "RefundedVnd",
                table: "V1FundMemberships",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CarryOverVnd",
                table: "V1ActivityVoters",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAt",
                table: "V1ActivityProposals",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "StartedRevision", table: "V1ActivityProposals");
            migrationBuilder.DropColumn(
                name: "RefundedVnd",
                table: "V1FundMemberships");

            migrationBuilder.DropColumn(
                name: "CarryOverVnd",
                table: "V1ActivityVoters");

            migrationBuilder.DropColumn(
                name: "SettledAt",
                table: "V1ActivityProposals");
        }
    }
}
