using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutineBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoutineBills",
                columns: table => new
                {
                    billId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    userId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expenseCategoryId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    billName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    billType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    dueDate = table.Column<DateTime>(type: "date", nullable: false),
                    frequency = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    paymentMethod = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reminderPeriod = table.Column<int>(type: "int", nullable: false),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    createdDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    recurrenceAnchorDay = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineBills", x => x.billId);
                    table.ForeignKey(
                        name: "FK_RoutineBills_ExpenseCategories_expenseCategoryId",
                        column: x => x.expenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "ExpenseCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoutineBills_Users_userId",
                        column: x => x.userId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RoutineBillPayments",
                columns: table => new
                {
                    paymentId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    billId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expenseId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dueDate = table.Column<DateTime>(type: "date", nullable: false),
                    paidDate = table.Column<DateTime>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    paymentMethod = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    billNameSnapshot = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    createdDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineBillPayments", x => x.paymentId);
                    table.ForeignKey(
                        name: "FK_RoutineBillPayments_Expenses_expenseId",
                        column: x => x.expenseId,
                        principalTable: "Expenses",
                        principalColumn: "ExpenseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoutineBillPayments_RoutineBills_billId",
                        column: x => x.billId,
                        principalTable: "RoutineBills",
                        principalColumn: "billId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RoutineBillPayments_billId_dueDate",
                table: "RoutineBillPayments",
                columns: new[] { "billId", "dueDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutineBillPayments_expenseId",
                table: "RoutineBillPayments",
                column: "expenseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutineBills_expenseCategoryId",
                table: "RoutineBills",
                column: "expenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutineBills_userId_status_dueDate",
                table: "RoutineBills",
                columns: new[] { "userId", "status", "dueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoutineBillPayments");

            migrationBuilder.DropTable(
                name: "RoutineBills");
        }
    }
}
