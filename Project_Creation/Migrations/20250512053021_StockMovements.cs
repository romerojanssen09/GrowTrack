using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class StockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "InventoryLogs",
                newName: "ReferenceId");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "InventoryLogs",
                newName: "QuantityBefore");

            migrationBuilder.AddColumn<string>(
                name: "MovementType",
                table: "InventoryLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "QuantityAfter",
                table: "InventoryLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLogs_ProductId",
                table: "InventoryLogs",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryLogs_Products2_ProductId",
                table: "InventoryLogs",
                column: "ProductId",
                principalTable: "Products2",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryLogs_Products2_ProductId",
                table: "InventoryLogs");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLogs_ProductId",
                table: "InventoryLogs");

            migrationBuilder.DropColumn(
                name: "MovementType",
                table: "InventoryLogs");

            migrationBuilder.DropColumn(
                name: "QuantityAfter",
                table: "InventoryLogs");

            migrationBuilder.RenameColumn(
                name: "ReferenceId",
                table: "InventoryLogs",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "QuantityBefore",
                table: "InventoryLogs",
                newName: "Quantity");
        }
    }
}
