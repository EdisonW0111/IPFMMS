using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetRepeatSeriesDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeriesVersion",
                table: "BudgetSchedules",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "BudgetScheduleId",
                table: "Budgets",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "RepeatSeriesVersion",
                table: "Budgets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BudgetRepeatSkips",
                columns: table => new
                {
                    BudgetScheduleId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SeriesVersion = table.Column<int>(type: "int", nullable: false),
                    BudgetMonth = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetRepeatSkips", x => new { x.BudgetScheduleId, x.SeriesVersion, x.BudgetMonth });
                    table.ForeignKey(
                        name: "FK_BudgetRepeatSkips_BudgetSchedules_BudgetScheduleId",
                        column: x => x.BudgetScheduleId,
                        principalTable: "BudgetSchedules",
                        principalColumn: "BudgetScheduleId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_BudgetScheduleId_RepeatSeriesVersion_BudgetMonth",
                table: "Budgets",
                columns: new[] { "BudgetScheduleId", "RepeatSeriesVersion", "BudgetMonth" });

            migrationBuilder.AddForeignKey(
                name: "FK_Budgets_BudgetSchedules_BudgetScheduleId",
                table: "Budgets",
                column: "BudgetScheduleId",
                principalTable: "BudgetSchedules",
                principalColumn: "BudgetScheduleId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Budgets_BudgetSchedules_BudgetScheduleId",
                table: "Budgets");

            migrationBuilder.DropTable(
                name: "BudgetRepeatSkips");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_BudgetScheduleId_RepeatSeriesVersion_BudgetMonth",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "SeriesVersion",
                table: "BudgetSchedules");

            migrationBuilder.DropColumn(
                name: "BudgetScheduleId",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "RepeatSeriesVersion",
                table: "Budgets");
        }
    }
}
