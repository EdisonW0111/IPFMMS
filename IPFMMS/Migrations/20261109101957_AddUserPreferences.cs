using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "showInApp",
                table: "Notifications",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Theme = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, defaultValue: "Light")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BillInApp = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    BillEmail = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    MonitoringInApp = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    MonitoringEmail = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    InvitationEmail = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "showInApp",
                table: "Notifications");
        }
    }
}
