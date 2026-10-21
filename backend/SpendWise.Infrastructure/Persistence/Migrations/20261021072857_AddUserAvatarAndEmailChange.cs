using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAvatarAndEmailChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingNewEmail",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PendingNewEmail",
                table: "Users");
        }
    }
}
