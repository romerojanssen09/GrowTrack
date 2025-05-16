using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignToo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SenderId",
                table: "Campaign",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Campaign_SenderId",
                table: "Campaign",
                column: "SenderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Campaign_Users_SenderId",
                table: "Campaign",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Campaign_Users_SenderId",
                table: "Campaign");

            migrationBuilder.DropIndex(
                name: "IX_Campaign_SenderId",
                table: "Campaign");

            migrationBuilder.DropColumn(
                name: "SenderId",
                table: "Campaign");
        }
    }
}
