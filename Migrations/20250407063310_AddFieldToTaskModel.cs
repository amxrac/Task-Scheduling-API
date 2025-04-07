using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task_Scheduling_API.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldToTaskModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "RecurrenceInterval",
                table: "ScheduledTasks",
                type: "interval",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecurrenceInterval",
                table: "ScheduledTasks");
        }
    }
}
