using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteOperationsSupportNotificationsPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoalMovements_Goals_GoalId",
                table: "GoalMovements");

            migrationBuilder.AddColumn<string>(
                name: "CorrectionGrantReason",
                table: "SupportCases",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CorrectionGranted",
                table: "SupportCases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CorrectionGrantedAt",
                table: "SupportCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrectionGrantedByAdminId",
                table: "SupportCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SupportCases",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestReplyAt",
                table: "SupportCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "SupportCases",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TicketNumber",
                table: "SupportCases",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Goals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategorySnapshot",
                table: "Goals",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "Goals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconName",
                table: "Goals",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalletNameSnapshot",
                table: "GoalMovements",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterpartySnapshot",
                table: "Disputes",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Disputes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestReplyAt",
                table: "Disputes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionAmountSnapshot",
                table: "Disputes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "TransactionOccurredAtSnapshot",
                table: "Disputes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TransactionReferenceSnapshot",
                table: "Disputes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TransactionTitleSnapshot",
                table: "Disputes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Rollover",
                table: "Budgets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CaseHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorDisplayNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyAnnouncements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyAnnouncements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeduplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeduplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NotifyBalanceChanges = table.Column<bool>(type: "bit", nullable: false),
                    NotifyBudgetAlerts = table.Column<bool>(type: "bit", nullable: false),
                    NotifyLargeTransactions = table.Column<bool>(type: "bit", nullable: false),
                    LargeTransactionThreshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NotifyMinBalanceAlert = table.Column<bool>(type: "bit", nullable: false),
                    MinBalanceThreshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemMaintenanceStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemMaintenanceStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseOwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseAttachments_CaseHistories_CaseHistoryId",
                        column: x => x.CaseHistoryId,
                        principalTable: "CaseHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Các hồ sơ Support cũ cần ticket riêng trước khi tạo unique index.
            migrationBuilder.Sql(
                "UPDATE [SupportCases] SET [TicketNumber] = CONCAT('LEGACY-', CONVERT(varchar(36), [Id])) WHERE [TicketNumber] = ''");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_TicketNumber",
                table: "SupportCases",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseAttachments_CaseHistoryId",
                table: "CaseAttachments",
                column: "CaseHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseHistories_CaseKind_CaseId_CreatedAt",
                table: "CaseHistories",
                columns: new[] { "CaseKind", "CaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyAnnouncements_Status",
                table: "EmergencyAnnouncements",
                column: "Status",
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeduplications_UserId_Key",
                table: "NotificationDeduplications",
                columns: new[] { "UserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId_AccountKey",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "AccountKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemMaintenanceStates_IsEnabled",
                table: "SystemMaintenanceStates",
                column: "IsEnabled");

            migrationBuilder.AddForeignKey(
                name: "FK_GoalMovements_Goals_GoalId",
                table: "GoalMovements",
                column: "GoalId",
                principalTable: "Goals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoalMovements_Goals_GoalId",
                table: "GoalMovements");

            migrationBuilder.DropTable(
                name: "CaseAttachments");

            migrationBuilder.DropTable(
                name: "EmergencyAnnouncements");

            migrationBuilder.DropTable(
                name: "NotificationDeduplications");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "SystemMaintenanceStates");

            migrationBuilder.DropTable(
                name: "CaseHistories");

            migrationBuilder.DropIndex(
                name: "IX_SupportCases_TicketNumber",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "CorrectionGrantReason",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "CorrectionGranted",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "CorrectionGrantedAt",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "CorrectionGrantedByAdminId",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "LatestReplyAt",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "TicketNumber",
                table: "SupportCases");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "CategorySnapshot",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "ColorHex",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "IconName",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "WalletNameSnapshot",
                table: "GoalMovements");

            migrationBuilder.DropColumn(
                name: "CounterpartySnapshot",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "LatestReplyAt",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "TransactionAmountSnapshot",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "TransactionOccurredAtSnapshot",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "TransactionReferenceSnapshot",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "TransactionTitleSnapshot",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "Rollover",
                table: "Budgets");

            migrationBuilder.AddForeignKey(
                name: "FK_GoalMovements_Goals_GoalId",
                table: "GoalMovements",
                column: "GoalId",
                principalTable: "Goals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
