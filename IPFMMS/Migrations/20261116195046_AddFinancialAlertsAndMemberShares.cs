using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialAlertsAndMemberShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FinancialEmail",
                table: "UserPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FinancialInApp",
                table: "UserPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "sharePercentage",
                table: "SharedFinanceMembers",
                type: "decimal(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinancialEmail",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "FinancialInApp",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "sharePercentage",
                table: "SharedFinanceMembers");
        }
    }
}
