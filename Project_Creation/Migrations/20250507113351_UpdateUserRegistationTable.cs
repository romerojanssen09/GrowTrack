using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserRegistationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAllowEditBrgyClearance",
                table: "UsersAdditionInfo",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAllowEditBusinessOwnerValidId",
                table: "UsersAdditionInfo",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAllowEditDtiCertPath",
                table: "UsersAdditionInfo",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAllowEditSecCertPath",
                table: "UsersAdditionInfo",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAllowEditBusinessPermitPath",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PasswordDto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmPassword = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordDto", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordDto");

            migrationBuilder.DropColumn(
                name: "IsAllowEditBrgyClearance",
                table: "UsersAdditionInfo");

            migrationBuilder.DropColumn(
                name: "IsAllowEditBusinessOwnerValidId",
                table: "UsersAdditionInfo");

            migrationBuilder.DropColumn(
                name: "IsAllowEditDtiCertPath",
                table: "UsersAdditionInfo");

            migrationBuilder.DropColumn(
                name: "IsAllowEditSecCertPath",
                table: "UsersAdditionInfo");

            migrationBuilder.DropColumn(
                name: "IsAllowEditBusinessPermitPath",
                table: "Users");
        }
    }
}
