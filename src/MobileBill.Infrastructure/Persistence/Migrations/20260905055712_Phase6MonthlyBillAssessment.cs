using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6MonthlyBillAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AssessedAt",
                table: "MonthlyBills",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssessedBy",
                table: "MonthlyBills",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeductionOverrideAmount",
                table: "MonthlyBills",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeductionOverrideAt",
                table: "MonthlyBills",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeductionOverrideBy",
                table: "MonthlyBills",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeductionOverrideReason",
                table: "MonthlyBills",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssessedAt",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "AssessedBy",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DeductionOverrideAmount",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DeductionOverrideAt",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DeductionOverrideBy",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DeductionOverrideReason",
                table: "MonthlyBills");
        }
    }
}
