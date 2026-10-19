using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteFinancialIntegrityEvidenceCorrectionsRecurring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnchorDay",
                table: "RecurringTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AnchorMonth",
                table: "RecurringTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Giữ ngày/tháng neo của các lịch cũ để tháng ngắn không làm trôi lịch.
            migrationBuilder.Sql(
                """
                UPDATE [RecurringTransactions]
                SET [AnchorDay] = DAY([NextRunAt]),
                    [AnchorMonth] = MONTH([NextRunAt])
                WHERE [AnchorDay] = 0 OR [AnchorMonth] = 0;
                """);

            migrationBuilder.AddColumn<int>(
                name: "CorrectionAllowance",
                table: "FinancialTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CorrectionCount",
                table: "FinancialTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsManual",
                table: "FinancialTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "BankTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurringOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecurringTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringOccurrences_FinancialTransactions_RecordedTransactionId",
                        column: x => x.RecordedTransactionId,
                        principalTable: "FinancialTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringOccurrences_RecurringTransactions_RecurringTransactionId",
                        column: x => x.RecurringTransactionId,
                        principalTable: "RecurringTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectionNumber = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OriginalValuesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CorrectedValuesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OriginalCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectedCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrectedDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalWalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectedWalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CorrectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OriginalCounterpartyName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CorrectedCounterpartyName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    OriginalOccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrectedOccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OriginalLedgerTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReversalLedgerTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectedLedgerTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowanceConsumed = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionCorrections_Categories_CorrectedCategoryId",
                        column: x => x.CorrectedCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionCorrections_Categories_OriginalCategoryId",
                        column: x => x.OriginalCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionCorrections_FinancialTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "FinancialTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionCorrections_LedgerTransactions_CorrectedLedgerTransactionId",
                        column: x => x.CorrectedLedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionCorrections_LedgerTransactions_OriginalLedgerTransactionId",
                        column: x => x.OriginalLedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionCorrections_LedgerTransactions_ReversalLedgerTransactionId",
                        column: x => x.ReversalLedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionCorrections_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionCorrections_Wallets_CorrectedWalletId",
                        column: x => x.CorrectedWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionCorrections_Wallets_OriginalWalletId",
                        column: x => x.OriginalWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    FinancialTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransactionCorrectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceAttachments", x => x.Id);
                    table.CheckConstraint("CK_EvidenceAttachments_OneParent", "[FinancialTransactionId] IS NULL OR [TransactionCorrectionId] IS NULL");
                    table.ForeignKey(
                        name: "FK_EvidenceAttachments_FinancialTransactions_FinancialTransactionId",
                        column: x => x.FinancialTransactionId,
                        principalTable: "FinancialTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceAttachments_TransactionCorrections_TransactionCorrectionId",
                        column: x => x.TransactionCorrectionId,
                        principalTable: "TransactionCorrections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceAttachments_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_CategoryId",
                table: "BankTransactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceAttachments_ContentHash",
                table: "EvidenceAttachments",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceAttachments_FinancialTransactionId",
                table: "EvidenceAttachments",
                column: "FinancialTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceAttachments_OwnerUserId_CreatedAt",
                table: "EvidenceAttachments",
                columns: new[] { "OwnerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceAttachments_TransactionCorrectionId",
                table: "EvidenceAttachments",
                column: "TransactionCorrectionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringOccurrences_RecordedTransactionId",
                table: "RecurringOccurrences",
                column: "RecordedTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringOccurrences_RecurringTransactionId_ScheduledFor",
                table: "RecurringOccurrences",
                columns: new[] { "RecurringTransactionId", "ScheduledFor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringOccurrences_Status_ScheduledFor",
                table: "RecurringOccurrences",
                columns: new[] { "Status", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrections_ActorUserId",
                table: "TransactionCorrections",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrections_CorrectedCategoryId",
                table: "TransactionCorrections",
                column: "CorrectedCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrections_CorrectedLedgerTransactionId",
                table: "TransactionCorrections",
                column: "CorrectedLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrections_CorrectedWalletId",
                table: "TransactionCorrections",
                column: "CorrectedWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrections_OriginalCategoryId",
                table: "TransactionCorrections",
                column: "OriginalCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrections_OriginalLedgerTransactionId",
                table: "TransactionCorrections",
                column: "OriginalLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrections_OriginalWalletId",
                table: "TransactionCorrections",
                column: "OriginalWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrections_ReversalLedgerTransactionId",
                table: "TransactionCorrections",
                column: "ReversalLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCorrections_TransactionId_CorrectionNumber",
                table: "TransactionCorrections",
                columns: new[] { "TransactionId", "CorrectionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_Categories_CategoryId",
                table: "BankTransactions",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_Categories_CategoryId",
                table: "BankTransactions");

            migrationBuilder.DropTable(
                name: "EvidenceAttachments");

            migrationBuilder.DropTable(
                name: "RecurringOccurrences");

            migrationBuilder.DropTable(
                name: "TransactionCorrections");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_CategoryId",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "AnchorDay",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "AnchorMonth",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "CorrectionAllowance",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "CorrectionCount",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "IsManual",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "BankTransactions");
        }
    }
}
