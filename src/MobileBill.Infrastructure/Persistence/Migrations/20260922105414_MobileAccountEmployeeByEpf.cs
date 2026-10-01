using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MobileAccountEmployeeByEpf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MobileAccounts_Employees_EmployeeId",
                table: "MobileAccounts");

            migrationBuilder.DropIndex(
                name: "IX_MobileAccounts_EmployeeId",
                table: "MobileAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Employees_EPF",
                table: "Employees");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeEpf",
                table: "MobileAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE ma SET ma.EmployeeEpf = e.EPF
                FROM MobileAccounts ma
                JOIN Employees e ON ma.EmployeeId = e.Id;
                """);

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "MobileAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "EmployeeEpf",
                table: "MobileAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Employees_EPF",
                table: "Employees",
                column: "EPF");

            migrationBuilder.CreateIndex(
                name: "IX_MobileAccounts_EmployeeEpf",
                table: "MobileAccounts",
                column: "EmployeeEpf");

            migrationBuilder.AddForeignKey(
                name: "FK_MobileAccounts_Employees_EmployeeEpf",
                table: "MobileAccounts",
                column: "EmployeeEpf",
                principalTable: "Employees",
                principalColumn: "EPF",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MobileAccounts_Employees_EmployeeEpf",
                table: "MobileAccounts");

            migrationBuilder.DropIndex(
                name: "IX_MobileAccounts_EmployeeEpf",
                table: "MobileAccounts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Employees_EPF",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "EmployeeEpf",
                table: "MobileAccounts");

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "MobileAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_MobileAccounts_EmployeeId",
                table: "MobileAccounts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EPF",
                table: "Employees",
                column: "EPF",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MobileAccounts_Employees_EmployeeId",
                table: "MobileAccounts",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
