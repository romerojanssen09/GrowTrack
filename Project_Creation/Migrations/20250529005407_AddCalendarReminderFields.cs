using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Creation.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarReminderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "CalendarTasks",
                newName: "UpdatedAt");

            migrationBuilder.AddColumn<bool>(
                name: "HasReminder",
                table: "CalendarTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReminderSent",
                table: "CalendarTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderMinutesBefore",
                table: "CalendarTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderSent",
                table: "CalendarTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasReminder",
                table: "CalendarTasks");

            migrationBuilder.DropColumn(
                name: "LastReminderSent",
                table: "CalendarTasks");

            migrationBuilder.DropColumn(
                name: "ReminderMinutesBefore",
                table: "CalendarTasks");

            migrationBuilder.DropColumn(
                name: "ReminderSent",
                table: "CalendarTasks");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "CalendarTasks",
                newName: "CompletedAt");
        }
    }
}
