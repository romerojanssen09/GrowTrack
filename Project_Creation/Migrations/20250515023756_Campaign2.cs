using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class Campaign2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasReplied",
                table: "Campaign",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MessageId",
                table: "Campaign",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyContent",
                table: "Campaign",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplyDate",
                table: "Campaign",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasReplied",
                table: "Campaign");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "Campaign");

            migrationBuilder.DropColumn(
                name: "ReplyContent",
                table: "Campaign");

            migrationBuilder.DropColumn(
                name: "ReplyDate",
                table: "Campaign");
        }
    }
}
