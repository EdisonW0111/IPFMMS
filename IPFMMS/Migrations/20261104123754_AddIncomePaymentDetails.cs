using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomePaymentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Incomes",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "Incomes",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "Incomes");
        }
    }
}
