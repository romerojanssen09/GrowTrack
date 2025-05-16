using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class FixUserRelationshipConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UsersAdditionInfo_Users_UserId",
                table: "UsersAdditionInfo");

            migrationBuilder.RenameColumn(
                name: "message",
                table: "Chats",
                newName: "Message");

            migrationBuilder.AddColumn<int>(
                name: "CurrentUser",
                table: "Chatmates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_UsersAdditionInfo_Users_UserId",
                table: "UsersAdditionInfo",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UsersAdditionInfo_Users_UserId",
                table: "UsersAdditionInfo");

            migrationBuilder.DropColumn(
                name: "CurrentUser",
                table: "Chatmates");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "Chats",
                newName: "message");

            migrationBuilder.AddForeignKey(
                name: "FK_UsersAdditionInfo_Users_UserId",
                table: "UsersAdditionInfo",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
