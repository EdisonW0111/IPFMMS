using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetScheduleEndMonth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndMonth",
                table: "BudgetSchedules",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndMonth",
                table: "BudgetSchedules");
        }
    }
}
