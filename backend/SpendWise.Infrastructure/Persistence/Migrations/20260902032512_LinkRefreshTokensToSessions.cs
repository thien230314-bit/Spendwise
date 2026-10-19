using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkRefreshTokensToSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserSessionId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: true);

            // Token cũ chưa có liên kết session: tạo một session an toàn cho từng token.
            migrationBuilder.Sql(
                """
                INSERT INTO [UserSessions]
                    ([Id], [UserId], [DeviceName], [DeviceIdentifier], [IpAddress],
                     [UserAgent], [LastSeenAt], [RevokedAt], [CreatedAt], [UpdatedAt])
                SELECT
                    [Id], [UserId], COALESCE([DeviceName], N'Migrated session'), NULL,
                    [IpAddress], NULL, [CreatedAt], [RevokedAt], [CreatedAt], NULL
                FROM [RefreshTokens];

                UPDATE [RefreshTokens]
                SET [UserSessionId] = [Id];
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserSessionId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserSessionId",
                table: "RefreshTokens",
                column: "UserSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_UserSessions_UserSessionId",
                table: "RefreshTokens",
                column: "UserSessionId",
                principalTable: "UserSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_UserSessions_UserSessionId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserSessionId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "UserSessionId",
                table: "RefreshTokens");

            // Chỉ xóa các session backfill có Id trùng chính token cũ.
            migrationBuilder.Sql(
                """
                DELETE FROM [UserSessions]
                WHERE [Id] IN (SELECT [Id] FROM [RefreshTokens]);
                """);
        }
    }
}
