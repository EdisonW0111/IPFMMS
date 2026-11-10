using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanCalculations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "calculationMode",
                table: "Debts",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manual")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "completedInstallments",
                table: "Debts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "isExistingLoan",
                table: "Debts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "loanDueDay",
                table: "Debts",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "loanStartDate",
                table: "Debts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "loanTermMonths",
                table: "Debts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rateSpread",
                table: "Debts",
                type: "decimal(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rateType",
                table: "Debts",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Fixed")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "referenceRate",
                table: "Debts",
                type: "decimal(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referenceRateName",
                table: "Debts",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "allocationMethod",
                table: "DebtRepayments",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manual")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "appliedInterestRate",
                table: "DebtRepayments",
                type: "decimal(7,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "interestAmount",
                table: "DebtRepayments",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "periodInterest",
                table: "DebtRepayments",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "principalAmount",
                table: "DebtRepayments",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "scheduledDueDate",
                table: "DebtRepayments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "scheduledPayment",
                table: "DebtRepayments",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DebtRateChanges",
                columns: table => new
                {
                    rateChangeId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    debtId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    effectiveDueDate = table.Column<DateTime>(type: "date", nullable: false),
                    previousRate = table.Column<decimal>(type: "decimal(7,4)", nullable: false),
                    newRate = table.Column<decimal>(type: "decimal(7,4)", nullable: false),
                    referenceRate = table.Column<decimal>(type: "decimal(7,4)", nullable: false),
                    spread = table.Column<decimal>(type: "decimal(7,4)", nullable: false),
                    monthlyPayment = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    createdDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebtRateChanges", x => x.rateChangeId);
                    table.ForeignKey(
                        name: "FK_DebtRateChanges_Debts_debtId",
                        column: x => x.debtId,
                        principalTable: "Debts",
                        principalColumn: "debtId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DebtRateChanges_debtId_effectiveDueDate",
                table: "DebtRateChanges",
                columns: new[] { "debtId", "effectiveDueDate" });
            // Preserve historical cash amounts; populate only the new allocation columns for manual debts.
            migrationBuilder.Sql("UPDATE `DebtRepayments` SET `principalAmount` = `repaymentAmount` WHERE `allocationMethod` = 'Manual';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DebtRateChanges");

            migrationBuilder.DropColumn(
                name: "calculationMode",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "completedInstallments",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "isExistingLoan",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "loanDueDay",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "loanStartDate",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "loanTermMonths",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "rateSpread",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "rateType",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "referenceRate",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "referenceRateName",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "allocationMethod",
                table: "DebtRepayments");

            migrationBuilder.DropColumn(
                name: "appliedInterestRate",
                table: "DebtRepayments");

            migrationBuilder.DropColumn(
                name: "interestAmount",
                table: "DebtRepayments");

            migrationBuilder.DropColumn(
                name: "periodInterest",
                table: "DebtRepayments");

            migrationBuilder.DropColumn(
                name: "principalAmount",
                table: "DebtRepayments");

            migrationBuilder.DropColumn(
                name: "scheduledDueDate",
                table: "DebtRepayments");

            migrationBuilder.DropColumn(
                name: "scheduledPayment",
                table: "DebtRepayments");
        }
    }
}
