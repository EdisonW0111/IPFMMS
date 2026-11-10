using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddDebtManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Debts",
                columns: table => new
                {
                    debtId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    userId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    debtName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    debtType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    originalAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    outstandingAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    monthlyRepaymentAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    interestRate = table.Column<decimal>(type: "decimal(7,4)", nullable: false),
                    dueDate = table.Column<DateTime>(type: "date", nullable: false),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    createdDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    openingOutstandingAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Debts", x => x.debtId);
                    table.ForeignKey(
                        name: "FK_Debts_Users_userId",
                        column: x => x.userId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DebtRepayments",
                columns: table => new
                {
                    repaymentId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    debtId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expenseId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    repaymentAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    repaymentDate = table.Column<DateTime>(type: "date", nullable: false),
                    paymentMethod = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    remark = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    createdDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebtRepayments", x => x.repaymentId);
                    table.ForeignKey(
                        name: "FK_DebtRepayments_Debts_debtId",
                        column: x => x.debtId,
                        principalTable: "Debts",
                        principalColumn: "debtId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebtRepayments_Expenses_expenseId",
                        column: x => x.expenseId,
                        principalTable: "Expenses",
                        principalColumn: "ExpenseId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DebtRepayments_debtId_repaymentDate",
                table: "DebtRepayments",
                columns: new[] { "debtId", "repaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DebtRepayments_expenseId",
                table: "DebtRepayments",
                column: "expenseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Debts_userId_status_dueDate",
                table: "Debts",
                columns: new[] { "userId", "status", "dueDate" });
            // Add the portfolio's repayment category without renaming or reactivating existing categories.
            migrationBuilder.Sql(@"INSERT INTO `ExpenseCategories` (`ExpenseCategoryId`,`CategoryName`,`Description`,`IsActive`,`DisplayOrder`,`CreatedDate`)
                SELECT 'EC_DEBT_REPAYMENT','Debt Repayment','Recorded personal debt repayments',1,10,UTC_TIMESTAMP(6)
                WHERE NOT EXISTS (SELECT 1 FROM `ExpenseCategories` WHERE `CategoryName` = 'Debt Repayment');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DebtRepayments");

            migrationBuilder.DropTable(
                name: "Debts");
        }
    }
}
