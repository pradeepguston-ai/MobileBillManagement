using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7BillReviewSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CallingNameSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryIdSnapshot",
                table: "MonthlyBills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "CategoryNameSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentIdSnapshot",
                table: "MonthlyBills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "DepartmentNameSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DesignationIdSnapshot",
                table: "MonthlyBills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "DesignationNameSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FactoryIdSnapshot",
                table: "MonthlyBills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "FactoryNameSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CallingNameSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "CategoryIdSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "CategoryNameSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DepartmentIdSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DepartmentNameSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DesignationIdSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DesignationNameSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "FactoryIdSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "FactoryNameSnapshot",
                table: "MonthlyBills");
        }
    }
}
