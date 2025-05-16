using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class AddProductForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleItem_Products2_ProductId",
                table: "SaleItem");

            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ProductId",
                table: "Sales",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItem_Products2_ProductId",
                table: "SaleItem",
                column: "ProductId",
                principalTable: "Products2",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Products2_ProductId",
                table: "Sales",
                column: "ProductId",
                principalTable: "Products2",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleItem_Products2_ProductId",
                table: "SaleItem");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Products2_ProductId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_ProductId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Sales");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItem_Products2_ProductId",
                table: "SaleItem",
                column: "ProductId",
                principalTable: "Products2",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
