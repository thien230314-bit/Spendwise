using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixedFundV2_Stage3_ProposalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContributionDurationHours",
                table: "V1ActivityProposals",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<string>(
                name: "NewName",
                table: "V1ActivityProposals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContributionDurationHours",
                table: "V1ActivityProposals");

            migrationBuilder.DropColumn(
                name: "NewName",
                table: "V1ActivityProposals");
        }
    }
}
