using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupFundExpenseIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupExpenses_GroupId",
                table: "GroupExpenses");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "GroupExpenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupExpenses_GroupId_IdempotencyKey",
                table: "GroupExpenses",
                columns: new[] { "GroupId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupExpenses_GroupId_IdempotencyKey",
                table: "GroupExpenses");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "GroupExpenses");

            migrationBuilder.CreateIndex(
                name: "IX_GroupExpenses_GroupId",
                table: "GroupExpenses",
                column: "GroupId");
        }
    }
}
