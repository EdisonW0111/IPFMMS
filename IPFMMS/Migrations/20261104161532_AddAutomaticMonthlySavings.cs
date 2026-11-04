using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticMonthlySavings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutomaticSavingDay",
                table: "SavingsGoals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AutomaticSavingsBaseline",
                table: "SavingsGoals",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAutomaticSavingDate",
                table: "SavingsGoals",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SavingsAutomaticAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SavingsGoalId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    CreditApplied = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsAutomaticAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsAutomaticAttempts_SavingsGoals_SavingsGoalId",
                        column: x => x.SavingsGoalId,
                        principalTable: "SavingsGoals",
                        principalColumn: "SavingsGoalId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAutomaticAttempts_SavingsGoalId_DueDate",
                table: "SavingsAutomaticAttempts",
                columns: new[] { "SavingsGoalId", "DueDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavingsAutomaticAttempts");

            migrationBuilder.DropColumn(
                name: "AutomaticSavingDay",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "AutomaticSavingsBaseline",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "NextAutomaticSavingDate",
                table: "SavingsGoals");
        }
    }
}
