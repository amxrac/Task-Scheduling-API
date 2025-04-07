using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task_Scheduling_API.Migrations
{
    /// <inheritdoc />
    public partial class AddModifiedAtFieldToScheduledTaskModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "ScheduledTasks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ScheduledTasks");
        }
    }
}
