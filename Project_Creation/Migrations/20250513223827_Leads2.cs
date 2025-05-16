using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class Leads2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessOwners");

            migrationBuilder.DropTable(
                name: "Chatmates");

            migrationBuilder.DropTable(
                name: "PasswordDto");

            migrationBuilder.DropTable(
                name: "Registers");

            migrationBuilder.AddColumn<int>(
                name: "LeadsId",
                table: "Products2",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InterestedIn",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Products2_LeadsId",
                table: "Products2",
                column: "LeadsId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products2_Leads_LeadsId",
                table: "Products2",
                column: "LeadsId",
                principalTable: "Leads",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products2_Leads_LeadsId",
                table: "Products2");

            migrationBuilder.DropIndex(
                name: "IX_Products2_LeadsId",
                table: "Products2");

            migrationBuilder.DropColumn(
                name: "LeadsId",
                table: "Products2");

            migrationBuilder.AlterColumn<string>(
                name: "InterestedIn",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessOwners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BusinessName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessOwners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chatmates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentUser = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCurrentUserSender = table.Column<bool>(type: "bit", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    LastMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastMessageTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnreadCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chatmates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PasswordDto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfirmPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordDto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Registers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessPermitPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyBackground = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DTIReqistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsAllowedToMarketPlace = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumberOfBusinessYearsOperation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScopeOfBusiness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserRole = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registers", x => x.Id);
                });
        }
    }
}
