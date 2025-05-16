using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierBOId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BOId",
                table: "Supplier2",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BOId",
                table: "Supplier2");
        }
    }
}
