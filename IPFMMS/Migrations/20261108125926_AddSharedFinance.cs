using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharedFinances",
                columns: table => new
                {
                    sharedFinanceId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sharedFinanceName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    financeType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    createdDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ownerId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedFinances", x => x.sharedFinanceId);
                    table.ForeignKey(
                        name: "FK_SharedFinances_Users_ownerId",
                        column: x => x.ownerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SharedBudgets",
                columns: table => new
                {
                    sharedBudgetId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sharedFinanceId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    budgetAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    budgetMonth = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedBudgets", x => x.sharedBudgetId);
                    table.ForeignKey(
                        name: "FK_SharedBudgets_SharedFinances_sharedFinanceId",
                        column: x => x.sharedFinanceId,
                        principalTable: "SharedFinances",
                        principalColumn: "sharedFinanceId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SharedFinanceMembers",
                columns: table => new
                {
                    sharedFinanceId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    userId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    memberRole = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    invitationStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    invitedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    joinedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    invitationVersion = table.Column<int>(type: "int", nullable: false),
                    emailStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emailAttemptedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    emailSentDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedFinanceMembers", x => new { x.sharedFinanceId, x.userId });
                    table.ForeignKey(
                        name: "FK_SharedFinanceMembers_SharedFinances_sharedFinanceId",
                        column: x => x.sharedFinanceId,
                        principalTable: "SharedFinances",
                        principalColumn: "sharedFinanceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SharedFinanceMembers_Users_userId",
                        column: x => x.userId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SharedFinanceTransactions",
                columns: table => new
                {
                    sharedTransactionId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sharedFinanceId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    userId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expenseCategoryId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    transactionType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    transactionDate = table.Column<DateTime>(type: "date", nullable: false),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expenseId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    paymentMethod = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    createdDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    submissionKey = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedFinanceTransactions", x => x.sharedTransactionId);
                    table.ForeignKey(
                        name: "FK_SharedFinanceTransactions_ExpenseCategories_expenseCategoryId",
                        column: x => x.expenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "ExpenseCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SharedFinanceTransactions_Expenses_expenseId",
                        column: x => x.expenseId,
                        principalTable: "Expenses",
                        principalColumn: "ExpenseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SharedFinanceTransactions_SharedFinances_sharedFinanceId",
                        column: x => x.sharedFinanceId,
                        principalTable: "SharedFinances",
                        principalColumn: "sharedFinanceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SharedFinanceTransactions_Users_userId",
                        column: x => x.userId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SharedSavingsGoals",
                columns: table => new
                {
                    sharedSavingsGoalId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sharedFinanceId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    targetAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedSavingsGoals", x => x.sharedSavingsGoalId);
                    table.ForeignKey(
                        name: "FK_SharedSavingsGoals_SharedFinances_sharedFinanceId",
                        column: x => x.sharedFinanceId,
                        principalTable: "SharedFinances",
                        principalColumn: "sharedFinanceId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SharedBudgets_sharedFinanceId_budgetMonth",
                table: "SharedBudgets",
                columns: new[] { "sharedFinanceId", "budgetMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedFinanceMembers_userId_invitationStatus",
                table: "SharedFinanceMembers",
                columns: new[] { "userId", "invitationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedFinances_ownerId_status",
                table: "SharedFinances",
                columns: new[] { "ownerId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedFinanceTransactions_expenseCategoryId",
                table: "SharedFinanceTransactions",
                column: "expenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedFinanceTransactions_expenseId",
                table: "SharedFinanceTransactions",
                column: "expenseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedFinanceTransactions_sharedFinanceId_transactionDate",
                table: "SharedFinanceTransactions",
                columns: new[] { "sharedFinanceId", "transactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedFinanceTransactions_userId_submissionKey",
                table: "SharedFinanceTransactions",
                columns: new[] { "userId", "submissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedSavingsGoals_sharedFinanceId",
                table: "SharedSavingsGoals",
                column: "sharedFinanceId",
                unique: true);
            migrationBuilder.Sql(@"INSERT INTO `ExpenseCategories` (`ExpenseCategoryId`,`CategoryName`,`Description`,`IsActive`,`DisplayOrder`,`CreatedDate`)
                SELECT 'EC_SHARED_SAVINGS','Shared Savings','Shared savings allocations, not spending',1,11,UTC_TIMESTAMP(6)
                WHERE NOT EXISTS (SELECT 1 FROM `ExpenseCategories` WHERE `CategoryName` = 'Shared Savings');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharedBudgets");

            migrationBuilder.DropTable(
                name: "SharedFinanceMembers");

            migrationBuilder.DropTable(
                name: "SharedFinanceTransactions");

            migrationBuilder.DropTable(
                name: "SharedSavingsGoals");

            migrationBuilder.DropTable(
                name: "SharedFinances");
        }
    }
}
