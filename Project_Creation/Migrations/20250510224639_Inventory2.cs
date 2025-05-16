using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class Inventory2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_Products_ProductId",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductImages_Products_ProductId",
                table: "ProductImages");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_ProductId",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Categories",
                newName: "CategoryName");

            migrationBuilder.AddColumn<int>(
                name: "Supplier2SupplierID",
                table: "ProductImages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Supplier2SupplierID",
                table: "InventoryTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BOId",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Products2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BOId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplierId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SKU = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuantityInStock = table.Column<int>(type: "int", nullable: false),
                    ReorderLevel = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SellingPrice = table.Column<int>(type: "int", nullable: true),
                    Supplier2SupplierID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products2", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products2_Supplier2_Supplier2SupplierID",
                        column: x => x.Supplier2SupplierID,
                        principalTable: "Supplier2",
                        principalColumn: "SupplierID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_Supplier2SupplierID",
                table: "ProductImages",
                column: "Supplier2SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ProductId",
                table: "InventoryTransactions",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_Supplier2SupplierID",
                table: "InventoryTransactions",
                column: "Supplier2SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_Products2_Supplier2SupplierID",
                table: "Products2",
                column: "Supplier2SupplierID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Products2_ProductId",
                table: "InventoryTransactions",
                column: "ProductId",
                principalTable: "Products2",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Supplier2_Supplier2SupplierID",
                table: "InventoryTransactions",
                column: "Supplier2SupplierID",
                principalTable: "Supplier2",
                principalColumn: "SupplierID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_Products2_ProductId",
                table: "ProductImages",
                column: "ProductId",
                principalTable: "Products2",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_Supplier2_Supplier2SupplierID",
                table: "ProductImages",
                column: "Supplier2SupplierID",
                principalTable: "Supplier2",
                principalColumn: "SupplierID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_Products2_ProductId",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_Supplier2_Supplier2SupplierID",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductImages_Products2_ProductId",
                table: "ProductImages");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductImages_Supplier2_Supplier2SupplierID",
                table: "ProductImages");

            migrationBuilder.DropTable(
                name: "Products2");

            migrationBuilder.DropIndex(
                name: "IX_ProductImages_Supplier2SupplierID",
                table: "ProductImages");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_ProductId",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_Supplier2SupplierID",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "Supplier2SupplierID",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "Supplier2SupplierID",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "BOId",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "CategoryName",
                table: "Categories",
                newName: "Name");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessOwnerId = table.Column<int>(type: "int", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    BOId = table.Column<int>(type: "int", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayFeatured = table.Column<bool>(type: "bit", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    MarketplaceDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MarketplacePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantityInStock = table.Column<int>(type: "int", nullable: false),
                    ReorderLevel = table.Column<int>(type: "int", nullable: true),
                    SKU = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_BusinessOwners_BusinessOwnerId",
                        column: x => x.BusinessOwnerId,
                        principalTable: "BusinessOwners",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Products_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ProductId",
                table: "InventoryTransactions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessOwnerId",
                table: "Products",
                column: "BusinessOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SupplierId",
                table: "Products",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Products_ProductId",
                table: "InventoryTransactions",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_Products_ProductId",
                table: "ProductImages",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
