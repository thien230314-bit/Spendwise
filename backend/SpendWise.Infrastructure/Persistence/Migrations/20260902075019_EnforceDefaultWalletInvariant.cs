using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDefaultWalletInvariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ví không Active không được giữ vai trò default.
            migrationBuilder.Sql(
                """
                UPDATE [Wallets]
                SET [IsDefault] = 0
                WHERE [IsDefault] = 1 AND [Status] <> N'Active';
                """);

            // Nếu dữ liệu cũ có nhiều default, giữ ví tạo sớm nhất theo thứ tự ổn định.
            migrationBuilder.Sql(
                """
                WITH [RankedDefaults] AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY [OwnerUserId]
                               ORDER BY [CreatedAt], [Id]) AS [DefaultOrder]
                    FROM [Wallets]
                    WHERE [IsDefault] = 1
                )
                UPDATE [Wallets]
                SET [IsDefault] = 0
                WHERE [Id] IN
                (
                    SELECT [Id]
                    FROM [RankedDefaults]
                    WHERE [DefaultOrder] > 1
                );
                """);

            // Nếu còn ví Active nhưng chưa có default, chọn ví tạo sớm nhất làm default.
            migrationBuilder.Sql(
                """
                WITH [DefaultCandidates] AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY [OwnerUserId]
                               ORDER BY [CreatedAt], [Id]) AS [CandidateOrder]
                    FROM [Wallets] AS [Candidate]
                    WHERE [Status] = N'Active'
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM [Wallets] AS [ExistingDefault]
                          WHERE [ExistingDefault].[OwnerUserId] = [Candidate].[OwnerUserId]
                            AND [ExistingDefault].[IsDefault] = 1
                      )
                )
                UPDATE [Wallets]
                SET [IsDefault] = 1
                WHERE [Id] IN
                (
                    SELECT [Id]
                    FROM [DefaultCandidates]
                    WHERE [CandidateOrder] = 1
                );
                """);

            migrationBuilder.DropIndex(
                name: "IX_Wallets_OwnerUserId_IsDefault",
                table: "Wallets");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_OwnerUserId",
                table: "Wallets",
                column: "OwnerUserId",
                unique: true,
                filter: "[IsDefault] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Wallets_OwnerUserId",
                table: "Wallets");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_OwnerUserId_IsDefault",
                table: "Wallets",
                columns: new[] { "OwnerUserId", "IsDefault" });
        }
    }
}
