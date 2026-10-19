using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V1FundContributionsAndObligations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "V1FundActivities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "V1FundContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    ObligationRevision = table.Column<long>(type: "bigint", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1FundContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1FundContributions_LedgerTransactions_LedgerTransactionId",
                        column: x => x.LedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundContributions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundContributions_V1FundMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "V1FundMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundContributions_V1Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "V1Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundContributions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1FundObligations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Seat = table.Column<int>(type: "int", nullable: false),
                    RequiredVnd = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1FundObligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1FundObligations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundObligations_V1FundMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "V1FundMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundObligations_V1Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "V1Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1FundTreasuries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LedgerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    TargetVnd = table.Column<long>(type: "bigint", nullable: true),
                    MembershipSignature = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1FundTreasuries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1FundTreasuries_LedgerAccounts_LedgerAccountId",
                        column: x => x.LedgerAccountId,
                        principalTable: "LedgerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundTreasuries_V1Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "V1Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_V1FundContributions_FundId",
                table: "V1FundContributions",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundContributions_LedgerTransactionId",
                table: "V1FundContributions",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundContributions_MembershipId",
                table: "V1FundContributions",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundContributions_UserId_IdempotencyKey",
                table: "V1FundContributions",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1FundContributions_WalletId",
                table: "V1FundContributions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundObligations_FundId_Revision_MembershipId",
                table: "V1FundObligations",
                columns: new[] { "FundId", "Revision", "MembershipId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1FundObligations_MembershipId",
                table: "V1FundObligations",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundObligations_UserId",
                table: "V1FundObligations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundTreasuries_FundId",
                table: "V1FundTreasuries",
                column: "FundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1FundTreasuries_LedgerAccountId",
                table: "V1FundTreasuries",
                column: "LedgerAccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "V1FundContributions");

            migrationBuilder.DropTable(
                name: "V1FundObligations");

            migrationBuilder.DropTable(
                name: "V1FundTreasuries");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "V1FundActivities");
        }
    }
}
