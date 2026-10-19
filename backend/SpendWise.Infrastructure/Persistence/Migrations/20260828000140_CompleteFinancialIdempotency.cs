using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteFinancialIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GoalMovements_GoalId_IdempotencyKey",
                table: "GoalMovements");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "SharedExpenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE [SharedExpenses]
                SET [IdempotencyKey] = CONCAT(N'legacy:', CONVERT(nvarchar(36), [Id]))
                WHERE [IdempotencyKey] = N'';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SharedExpenses_GroupId_IdempotencyKey",
                table: "SharedExpenses",
                columns: new[] { "GroupId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoalMovements_GoalId_Type_IdempotencyKey",
                table: "GoalMovements",
                columns: new[] { "GoalId", "Type", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SharedExpenses_GroupId_IdempotencyKey",
                table: "SharedExpenses");

            migrationBuilder.DropIndex(
                name: "IX_GoalMovements_GoalId_Type_IdempotencyKey",
                table: "GoalMovements");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "SharedExpenses");

            migrationBuilder.CreateIndex(
                name: "IX_GoalMovements_GoalId_IdempotencyKey",
                table: "GoalMovements",
                columns: new[] { "GoalId", "IdempotencyKey" },
                unique: true);
        }
    }
}
