using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class FixCalendarUserForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Calendar_Users_UsersId",
                table: "Calendar");

            migrationBuilder.DropIndex(
                name: "IX_Calendar_UsersId",
                table: "Calendar");

            migrationBuilder.DropColumn(
                name: "UsersId",
                table: "Calendar");

            migrationBuilder.CreateIndex(
                name: "IX_Calendar_UserId",
                table: "Calendar",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Calendar_Users_UserId",
                table: "Calendar",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Calendar_Users_UserId",
                table: "Calendar");

            migrationBuilder.DropIndex(
                name: "IX_Calendar_UserId",
                table: "Calendar");

            migrationBuilder.AddColumn<int>(
                name: "UsersId",
                table: "Calendar",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Calendar_UsersId",
                table: "Calendar",
                column: "UsersId");

            migrationBuilder.AddForeignKey(
                name: "FK_Calendar_Users_UsersId",
                table: "Calendar",
                column: "UsersId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
