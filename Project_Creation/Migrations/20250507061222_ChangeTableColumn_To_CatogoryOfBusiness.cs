using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTableColumn_To_CatogoryOfBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ScopeOfBusiness",
                table: "Users",
                newName: "CategoryOfBusiness");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CategoryOfBusiness",
                table: "Users",
                newName: "ScopeOfBusiness");
        }
    }
}
