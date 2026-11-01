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
            migrationBuilder.DropColumn(
                name: "ReplacementWindowExpiresAt",
                table: "V1Broadcasts");

            migrationBuilder.DropColumn(
                name: "IsReplacement",
                table: "GroupInvites");

            migrationBuilder.DropColumn(
                name: "TargetSeat",
                table: "GroupInvites");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplacementWindowExpiresAt",
                table: "V1FundTreasuries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReplacement",
                table: "V1FundInvitations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TargetSeat",
                table: "V1FundInvitations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContributionDurationHours",
                table: "V1ActivityProposals",
                type: "int",
                nullable: false,
                defaultValue: 0);

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
                name: "ReplacementWindowExpiresAt",
                table: "V1FundTreasuries");

            migrationBuilder.DropColumn(
                name: "IsReplacement",
                table: "V1FundInvitations");

            migrationBuilder.DropColumn(
                name: "TargetSeat",
                table: "V1FundInvitations");

            migrationBuilder.DropColumn(
                name: "ContributionDurationHours",
                table: "V1ActivityProposals");

            migrationBuilder.DropColumn(
                name: "NewName",
                table: "V1ActivityProposals");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplacementWindowExpiresAt",
                table: "V1Broadcasts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReplacement",
                table: "GroupInvites",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TargetSeat",
                table: "GroupInvites",
                type: "int",
                nullable: true);
        }
    }
}
