using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPFMMS.Migrations
{
    /// <inheritdoc />
    public partial class AddParentLinkRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "acceptedDate",
                table: "ParentStudentLinks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "emailSentDate",
                table: "ParentStudentLinks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "lastEmailAttemptDate",
                table: "ParentStudentLinks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "requestVersion",
                table: "ParentStudentLinks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "requestedDate",
                table: "ParentStudentLinks",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "acceptedDate",
                table: "ParentStudentLinks");

            migrationBuilder.DropColumn(
                name: "emailSentDate",
                table: "ParentStudentLinks");

            migrationBuilder.DropColumn(
                name: "lastEmailAttemptDate",
                table: "ParentStudentLinks");

            migrationBuilder.DropColumn(
                name: "requestVersion",
                table: "ParentStudentLinks");

            migrationBuilder.DropColumn(
                name: "requestedDate",
                table: "ParentStudentLinks");
        }
    }
}
