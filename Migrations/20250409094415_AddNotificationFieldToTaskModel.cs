using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task_Scheduling_API.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationFieldToTaskModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ScheduledTasks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "NotificationSent",
                table: "ScheduledTasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NotificationSentAt",
                table: "ScheduledTasks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "NotificationSent",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "NotificationSentAt",
                table: "ScheduledTasks");
        }
    }
}
