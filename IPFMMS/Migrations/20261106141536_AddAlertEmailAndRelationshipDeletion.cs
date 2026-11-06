using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertEmailAndRelationshipDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "parentHiddenDate",
                table: "ParentStudentLinks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "studentHiddenDate",
                table: "ParentStudentLinks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "emailAttemptedDate",
                table: "Notifications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "emailSentDate",
                table: "Notifications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emailStatus",
                table: "Notifications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotRequested")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "parentHiddenDate",
                table: "ParentStudentLinks");

            migrationBuilder.DropColumn(
                name: "studentHiddenDate",
                table: "ParentStudentLinks");

            migrationBuilder.DropColumn(
                name: "emailAttemptedDate",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "emailSentDate",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "emailStatus",
                table: "Notifications");
        }
    }
}
