using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetSchedules",
                columns: table => new
                {
                    BudgetScheduleId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpenseCategoryId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NextBudgetMonth = table.Column<DateTime>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BudgetType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BudgetAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    BudgetPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetSchedules", x => x.BudgetScheduleId);
                    table.ForeignKey(
                        name: "FK_BudgetSchedules_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "ExpenseCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetSchedules_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSchedules_ExpenseCategoryId",
                table: "BudgetSchedules",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSchedules_UserId_ExpenseCategoryId",
                table: "BudgetSchedules",
                columns: new[] { "UserId", "ExpenseCategoryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetSchedules");
        }
    }
}
