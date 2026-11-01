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
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ConfirmedObligationVnd",
                table: "V1ActivityVoters",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedDurationHours",
                table: "V1ActivityVoters",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "V1ActivityVoters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_V1ActivityProposals_FundId",
                table: "V1ActivityProposals");

            migrationBuilder.CreateIndex(
                name: "IX_V1ActivityProposals_FundId",
                table: "V1ActivityProposals",
                column: "FundId",
                unique: true,
                filter: "[State] IN ('Voting', 'ApprovedAwaitingSettlement', 'AwaitingReplacement')");

            migrationBuilder.DropIndex(
                name: "IX_V1ActivityVoters_ProposalId_UserId",
                table: "V1ActivityVoters");

            migrationBuilder.CreateIndex(
                name: "IX_V1ActivityVoters_ProposalId_MembershipId",
                table: "V1ActivityVoters",
                columns: new[] { "ProposalId", "MembershipId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_V1ActivityVoters_ProposalId_MembershipId",
                table: "V1ActivityVoters");

            migrationBuilder.CreateIndex(
                name: "IX_V1ActivityVoters_ProposalId_UserId",
                table: "V1ActivityVoters",
                columns: new[] { "ProposalId", "UserId" },
                unique: true);

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "V1ActivityVoters");

            migrationBuilder.DropColumn(
                name: "ConfirmedDurationHours",
                table: "V1ActivityVoters");

            migrationBuilder.DropColumn(
                name: "ConfirmedObligationVnd",
                table: "V1ActivityVoters");

            migrationBuilder.DropColumn(
                name: "NewName",
                table: "V1ActivityProposals");

            migrationBuilder.DropIndex(
                name: "IX_V1ActivityProposals_FundId",
                table: "V1ActivityProposals");

            migrationBuilder.CreateIndex(
                name: "IX_V1ActivityProposals_FundId",
                table: "V1ActivityProposals",
                column: "FundId",
                unique: true,
                filter: "[State] IN ('Voting', 'ApprovedAwaitingSettlement')");

            migrationBuilder.DropColumn(
                name: "ContributionDurationHours",
                table: "V1ActivityProposals");
        }
    }
}
