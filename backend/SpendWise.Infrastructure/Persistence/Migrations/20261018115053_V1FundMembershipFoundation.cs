using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V1FundMembershipFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "V1FundCodeCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1FundCodeCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "V1Funds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FounderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnershipRevision = table.Column<long>(type: "bigint", nullable: false),
                    TargetVnd = table.Column<long>(type: "bigint", nullable: true),
                    DissolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1Funds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1Funds_Users_FounderUserId",
                        column: x => x.FounderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1Funds_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1FundActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Event = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1FundActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1FundActivities_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundActivities_Users_SubjectUserId",
                        column: x => x.SubjectUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundActivities_V1Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "V1Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1FundInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1FundInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1FundInvitations_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundInvitations_Users_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundInvitations_V1Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "V1Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1FundMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Seat = table.Column<int>(type: "int", nullable: false),
                    CanSpend = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1FundMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1FundMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1FundMemberships_V1Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "V1Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "V1OwnershipOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedOwnershipRevision = table.Column<long>(type: "bigint", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V1OwnershipOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_V1OwnershipOffers_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1OwnershipOffers_Users_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_V1OwnershipOffers_V1Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "V1Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "V1FundCodeCounters",
                columns: new[] { "Id", "NextValue" },
                values: new object[] { 1, 1L });

            migrationBuilder.CreateIndex(
                name: "IX_V1FundActivities_ActorUserId",
                table: "V1FundActivities",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundActivities_FundId_CreatedAt",
                table: "V1FundActivities",
                columns: new[] { "FundId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_V1FundActivities_SubjectUserId",
                table: "V1FundActivities",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundInvitations_FundId_InvitedUserId",
                table: "V1FundInvitations",
                columns: new[] { "FundId", "InvitedUserId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundInvitations_InvitedByUserId",
                table: "V1FundInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundInvitations_InvitedUserId",
                table: "V1FundInvitations",
                column: "InvitedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundMemberships_FundId_Seat",
                table: "V1FundMemberships",
                columns: new[] { "FundId", "Seat" },
                unique: true,
                filter: "[LeftAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundMemberships_FundId_UserId",
                table: "V1FundMemberships",
                columns: new[] { "FundId", "UserId" },
                unique: true,
                filter: "[LeftAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_V1FundMemberships_UserId_LeftAt",
                table: "V1FundMemberships",
                columns: new[] { "UserId", "LeftAt" });

            migrationBuilder.CreateIndex(
                name: "IX_V1Funds_Code",
                table: "V1Funds",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V1Funds_FounderUserId",
                table: "V1Funds",
                column: "FounderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1Funds_OwnerUserId",
                table: "V1Funds",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1OwnershipOffers_FromUserId",
                table: "V1OwnershipOffers",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_V1OwnershipOffers_FundId",
                table: "V1OwnershipOffers",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_V1OwnershipOffers_ToUserId",
                table: "V1OwnershipOffers",
                column: "ToUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "V1FundActivities");

            migrationBuilder.DropTable(
                name: "V1FundCodeCounters");

            migrationBuilder.DropTable(
                name: "V1FundInvitations");

            migrationBuilder.DropTable(
                name: "V1FundMemberships");

            migrationBuilder.DropTable(
                name: "V1OwnershipOffers");

            migrationBuilder.DropTable(
                name: "V1Funds");
        }
    }
}
