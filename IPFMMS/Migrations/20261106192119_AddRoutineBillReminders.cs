using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutineBillReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "billDueDate",
                table: "Notifications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "routineBillId",
                table: "Notifications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "billDueDate",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "routineBillId",
                table: "Notifications");
        }
    }
}
