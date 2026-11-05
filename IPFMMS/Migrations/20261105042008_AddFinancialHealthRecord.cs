using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialHealthRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialHealthRecords",
                columns: table => new
                {
                    healthRecordId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    userId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    savingsRatioScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    budgetAdherenceScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    spendingStabilityScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    healthScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    rating = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recordMonth = table.Column<DateTime>(type: "date", nullable: false),
                    calculatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    formulaVersion = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialHealthRecords", x => x.healthRecordId);
                    table.ForeignKey(
                        name: "FK_FinancialHealthRecords_Users_userId",
                        column: x => x.userId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialHealthRecords_userId_recordMonth",
                table: "FinancialHealthRecords",
                columns: new[] { "userId", "recordMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialHealthRecords");
        }
    }
}
