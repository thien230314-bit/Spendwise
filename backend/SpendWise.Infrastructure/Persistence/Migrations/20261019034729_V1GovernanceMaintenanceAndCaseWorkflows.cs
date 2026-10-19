using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V1GovernanceMaintenanceAndCaseWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_V1MaintenancePlans_CreatedByUserId",
                table: "V1MaintenancePlans");

            migrationBuilder.AddColumn<string>(
                name: "CloseSafetyEvidenceId",
                table: "V1MaintenancePlans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmergency",
                table: "V1MaintenancePlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "V1MaintenancePlans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderLateToleranceSeconds",
                table: "V1MaintenancePlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderOffsetsJson",
                table: "V1MaintenancePlans",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ReminderPolicyVersion",
                table: "V1MaintenancePlans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemindersConfiguredAt",
                table: "V1MaintenancePlans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNotes",
                table: "V1MaintenancePlans",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "V1MaintenancePlans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "V1MaintenancePlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "V1MaintenancePlans",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Global");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivityClosedAt",
                table: "V1Funds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnerRespondedAt",
                table: "V1Funds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerResponseCaseId",
                table: "V1Funds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                table: "V1FundActivities",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedAt",
                table: "SupportCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalatedById",
                table: "SupportCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationGuidance",
                table: "SupportCases",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationReason",
                table: "SupportCases",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EscalationReviewedAt",
                table: "SupportCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalationReviewerId",
                table: "SupportCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "V1ActivityProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnershipRevision = table.Column<long>(type: "bigint", nullable: false),
                    TargetVnd = table.Column<long>(type: "bigint", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1ActivityProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1ActivityProposals_Users_ProposedByUserId",
                        column: x => x.ProposedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1ActivityProposals_V1Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "V1Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1Broadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TargetRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AudienceCutoffAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaintenancePlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaintenanceRevision = table.Column<long>(type: "bigint", nullable: true),
                    RecipientLimit = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1Broadcasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1Broadcasts_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1Broadcasts_V1MaintenancePlans_MaintenancePlanId",
                        column: x => x.MaintenancePlanId,
                        principalTable: "V1MaintenancePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1FundRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnershipRevision = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1FundRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1FundRequests_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundRequests_V1Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "V1Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1ActivityVoters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Support = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1ActivityVoters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1ActivityVoters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1ActivityVoters_V1ActivityProposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "V1ActivityProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1ActivityVoters_V1FundMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "V1FundMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_V1MaintenancePlans_CreatedByUserId_OperationKey",
                table: "V1MaintenancePlans",
                columns: new[] { "CreatedByUserId", "OperationKey" },
                unique: true,
                filter: "[OperationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_V1MaintenancePlans_ReviewedByUserId",
                table: "V1MaintenancePlans",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1Funds_OwnerResponseCaseId",
                table: "V1Funds",
                column: "OwnerResponseCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_EscalatedById",
                table: "SupportCases",
                column: "EscalatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_EscalationReviewerId",
                table: "SupportCases",
                column: "EscalationReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_V1ActivityProposals_FundId",
                table: "V1ActivityProposals",
                column: "FundId",
                unique: true,
                filter: "[State] IN ('Voting', 'ApprovedAwaitingSettlement')");

            migrationBuilder.CreateIndex(
                name: "IX_V1ActivityProposals_FundId_IdempotencyKey",
                table: "V1ActivityProposals",
                columns: new[] { "FundId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1ActivityProposals_ProposedByUserId",
                table: "V1ActivityProposals",
                column: "ProposedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1ActivityVoters_MembershipId",
                table: "V1ActivityVoters",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_V1ActivityVoters_ProposalId_UserId",
                table: "V1ActivityVoters",
                columns: new[] { "ProposalId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1ActivityVoters_UserId",
                table: "V1ActivityVoters",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1Broadcasts_CreatedByUserId",
                table: "V1Broadcasts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1Broadcasts_EventKey",
                table: "V1Broadcasts",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1Broadcasts_MaintenancePlanId",
                table: "V1Broadcasts",
                column: "MaintenancePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_V1Broadcasts_State_ScheduledFor",
                table: "V1Broadcasts",
                columns: new[] { "State", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_V1FundRequests_FundId_RequestedByUserId_IdempotencyKey",
                table: "V1FundRequests",
                columns: new[] { "FundId", "RequestedByUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1FundRequests_FundId_State_CreatedAt",
                table: "V1FundRequests",
                columns: new[] { "FundId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_V1FundRequests_RecipientUserId",
                table: "V1FundRequests",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundRequests_RequestedByUserId",
                table: "V1FundRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundRequests_ReviewedByUserId",
                table: "V1FundRequests",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupportCases_Users_EscalatedById",
                table: "SupportCases",
                column: "EscalatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportCases_Users_EscalationReviewerId",
                table: "SupportCases",
                column: "EscalationReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_V1Funds_SupportCases_OwnerResponseCaseId",
                table: "V1Funds",
                column: "OwnerResponseCaseId",
                principalTable: "SupportCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_V1MaintenancePlans_Users_ReviewedByUserId",
                table: "V1MaintenancePlans",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportCases_Users_EscalatedById",
                table: "SupportCases");

            migrationBuilder.DropForeignKey(
                name: "FK_SupportCases_Users_EscalationReviewerId",
                table: "SupportCases");

            migrationBuilder.DropForeignKey(
                name: "FK_V1Funds_SupportCases_OwnerResponseCaseId",
                table: "V1Funds");

            migrationBuilder.DropForeignKey(
                name: "FK_V1MaintenancePlans_Users_ReviewedByUserId",
                table: "V1MaintenancePlans");

            migrationBuilder.DropTable(
                name: "V1ActivityVoters");

            migrationBuilder.DropTable(
                name: "V1Broadcasts");

            migrationBuilder.DropTable(
                name: "V1FundRequests");

            migrationBuilder.DropTable(
                name: "V1ActivityProposals");

            migrationBuilder.DropIndex(
                name: "IX_V1MaintenancePlans_CreatedByUserId_OperationKey",
                table: "V1MaintenancePlans");

            migrationBuilder.DropIndex(
                name: "IX_V1MaintenancePlans_ReviewedByUserId",
                table: "V1MaintenancePlans");

            migrationBuilder.DropIndex(
                name: "IX_V1Funds_OwnerResponseCaseId",
                table: "V1Funds");

            migrationBuilder.DropIndex(
                name: "IX_SupportCases_EscalatedById",
                table: "SupportCases");

            migrationBuilder.DropIndex(
                name: "IX_SupportCases_EscalationReviewerId",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "CloseSafetyEvidenceId",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "IsEmergency",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "ReminderLateToleranceSeconds",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "ReminderOffsetsJson",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "ReminderPolicyVersion",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "RemindersConfiguredAt",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "ReviewNotes",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "V1MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "ActivityClosedAt",
                table: "V1Funds");

            migrationBuilder.DropColumn(
                name: "OwnerRespondedAt",
                table: "V1Funds");

            migrationBuilder.DropColumn(
                name: "OwnerResponseCaseId",
                table: "V1Funds");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "EscalatedById",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "EscalationGuidance",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "EscalationReason",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "EscalationReviewedAt",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "EscalationReviewerId",
                table: "SupportCases");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                table: "V1FundActivities",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1MaintenancePlans_CreatedByUserId",
                table: "V1MaintenancePlans",
                column: "CreatedByUserId");
        }
    }
}
