using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAiAssistantActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiConversations_UserId_CreatedAt",
                table: "AiConversations");

            migrationBuilder.AddColumn<Guid>(
                name: "ContextGroupId",
                table: "AiConversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextType",
                table: "AiConversations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "AiConversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AiActionProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    SafeSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiActionProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiActionProposals_AiConversations_AiConversationId",
                        column: x => x.AiConversationId,
                        principalTable: "AiConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AiAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAttachments_AiConversations_AiConversationId",
                        column: x => x.AiConversationId,
                        principalTable: "AiConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiAttachments_AiMessages_AiMessageId",
                        column: x => x.AiMessageId,
                        principalTable: "AiMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_UserId_IsPinned_UpdatedAt",
                table: "AiConversations",
                columns: new[] { "UserId", "IsPinned", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiActionProposals_AiConversationId",
                table: "AiActionProposals",
                column: "AiConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionProposals_OwnerUserId_Status_ExpiresAt",
                table: "AiActionProposals",
                columns: new[] { "OwnerUserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAttachments_AiConversationId",
                table: "AiAttachments",
                column: "AiConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAttachments_AiMessageId",
                table: "AiAttachments",
                column: "AiMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAttachments_OwnerUserId_CreatedAt",
                table: "AiAttachments",
                columns: new[] { "OwnerUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiActionProposals");

            migrationBuilder.DropTable(
                name: "AiAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AiConversations_UserId_IsPinned_UpdatedAt",
                table: "AiConversations");

            migrationBuilder.DropColumn(
                name: "ContextGroupId",
                table: "AiConversations");

            migrationBuilder.DropColumn(
                name: "ContextType",
                table: "AiConversations");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "AiConversations");

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_UserId_CreatedAt",
                table: "AiConversations",
                columns: new[] { "UserId", "CreatedAt" });
        }
    }
}
