using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V1ElectionCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "V1Elections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncumbentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnershipRevision = table.Column<long>(type: "bigint", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    PhaseStartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WinnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PolicyJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    InactivityEvidenceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1Elections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1Elections_Users_IncumbentUserId",
                        column: x => x.IncumbentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1Elections_Users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1Elections_V1Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "V1Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1ElectionBallots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    CandidateUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Support = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1ElectionBallots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1ElectionBallots_Users_CandidateUserId",
                        column: x => x.CandidateUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1ElectionBallots_Users_VoterUserId",
                        column: x => x.VoterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1ElectionBallots_V1Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "V1Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1ElectionCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InRunoff = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1ElectionCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1ElectionCandidates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1ElectionCandidates_V1Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "V1Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1ElectionVoters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1ElectionVoters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1ElectionVoters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1ElectionVoters_V1Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "V1Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1ElectionVoters_V1FundMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "V1FundMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_V1ElectionBallots_CandidateUserId",
                table: "V1ElectionBallots",
                column: "CandidateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1ElectionBallots_ElectionId_Round_VoterUserId",
                table: "V1ElectionBallots",
                columns: new[] { "ElectionId", "Round", "VoterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1ElectionBallots_VoterUserId",
                table: "V1ElectionBallots",
                column: "VoterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1ElectionCandidates_ElectionId_UserId",
                table: "V1ElectionCandidates",
                columns: new[] { "ElectionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1ElectionCandidates_UserId",
                table: "V1ElectionCandidates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1Elections_FundId",
                table: "V1Elections",
                column: "FundId",
                unique: true,
                filter: "[FinishedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_V1Elections_IncumbentUserId",
                table: "V1Elections",
                column: "IncumbentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1Elections_OpenedByUserId",
                table: "V1Elections",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1ElectionVoters_ElectionId_UserId",
                table: "V1ElectionVoters",
                columns: new[] { "ElectionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1ElectionVoters_MembershipId",
                table: "V1ElectionVoters",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_V1ElectionVoters_UserId",
                table: "V1ElectionVoters",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "V1ElectionBallots");

            migrationBuilder.DropTable(
                name: "V1ElectionCandidates");

            migrationBuilder.DropTable(
                name: "V1ElectionVoters");

            migrationBuilder.DropTable(
                name: "V1Elections");
        }
    }
}
