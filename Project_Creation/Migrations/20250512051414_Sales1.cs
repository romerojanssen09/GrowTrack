using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class Sales1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleItem_Products2_ProductId",
                table: "SaleItem");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItem_Sales_SaleId",
                table: "SaleItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SaleItem",
                table: "SaleItem");

            migrationBuilder.RenameTable(
                name: "SaleItem",
                newName: "SaleItems");

            migrationBuilder.RenameIndex(
                name: "IX_SaleItem_SaleId",
                table: "SaleItems",
                newName: "IX_SaleItems_SaleId");

            migrationBuilder.RenameIndex(
                name: "IX_SaleItem_ProductId",
                table: "SaleItems",
                newName: "IX_SaleItems_ProductId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SaleItems",
                table: "SaleItems",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_Products2_ProductId",
                table: "SaleItems",
                column: "ProductId",
                principalTable: "Products2",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_Sales_SaleId",
                table: "SaleItems",
                column: "SaleId",
                principalTable: "Sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_Products2_ProductId",
                table: "SaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_Sales_SaleId",
                table: "SaleItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SaleItems",
                table: "SaleItems");

            migrationBuilder.RenameTable(
                name: "SaleItems",
                newName: "SaleItem");

            migrationBuilder.RenameIndex(
                name: "IX_SaleItems_SaleId",
                table: "SaleItem",
                newName: "IX_SaleItem_SaleId");

            migrationBuilder.RenameIndex(
                name: "IX_SaleItems_ProductId",
                table: "SaleItem",
                newName: "IX_SaleItem_ProductId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SaleItem",
                table: "SaleItem",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItem_Products2_ProductId",
                table: "SaleItem",
                column: "ProductId",
                principalTable: "Products2",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItem_Sales_SaleId",
                table: "SaleItem",
                column: "SaleId",
                principalTable: "Sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
