using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixedFundV2_Stage1_Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContributionDurationHours",
                table: "V1Funds",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<DateTime>(
                name: "Deadline",
                table: "V1FundObligations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedCommitmentVersion",
                table: "V1FundMemberships",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContributionDeadline",
                table: "V1FundMemberships",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrustCommitmentAcceptedAt",
                table: "V1FundMemberships",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContributionDurationHours",
                table: "V1Funds");

            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "V1FundObligations");

            migrationBuilder.DropColumn(
                name: "AcceptedCommitmentVersion",
                table: "V1FundMemberships");

            migrationBuilder.DropColumn(
                name: "ContributionDeadline",
                table: "V1FundMemberships");

            migrationBuilder.DropColumn(
                name: "TrustCommitmentAcceptedAt",
                table: "V1FundMemberships");

            migrationBuilder.DropColumn(
                name: "IsReplacement",
                table: "V1FundInvitations");

            migrationBuilder.DropColumn(
                name: "TargetSeat",
                table: "V1FundInvitations");
        }
    }
}
