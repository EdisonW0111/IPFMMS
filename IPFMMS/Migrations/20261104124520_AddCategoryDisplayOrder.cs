using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "IncomeCategories",
                type: "int",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "ExpenseCategories",
                type: "int",
                nullable: false,
                defaultValue: 1000);
            migrationBuilder.Sql(@"UPDATE `IncomeCategories`
            SET `DisplayOrder` = CASE LOWER(`CategoryName`)
                WHEN 'salary' THEN 1
                WHEN 'allowance' THEN 2
                WHEN 'scholarship' THEN 3
                WHEN 'part-time job' THEN 4
                WHEN 'freelance' THEN 5
                WHEN 'investment returns' THEN 6
                WHEN 'gifts' THEN 7
                WHEN 'other income' THEN 8
                ELSE 1000 END
            WHERE `DisplayOrder` = 1000;");

            migrationBuilder.Sql(@"UPDATE `ExpenseCategories`
            SET `DisplayOrder` = CASE LOWER(`CategoryName`)
                WHEN 'food & drinks' THEN 1
                WHEN 'transportation' THEN 2
                WHEN 'education' THEN 3
                WHEN 'shopping' THEN 4
                WHEN 'entertainment' THEN 5
                WHEN 'bills & utilities' THEN 6
                WHEN 'healthcare' THEN 7
                WHEN 'loan interest & fees' THEN 8
                WHEN 'other expenses' THEN 9
                ELSE 1000 END
            WHERE `DisplayOrder` = 1000;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "IncomeCategories");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "ExpenseCategories");
        }
    }
}
