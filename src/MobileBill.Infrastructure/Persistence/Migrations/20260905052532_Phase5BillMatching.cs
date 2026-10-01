using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5BillMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AllocationEffectiveFromSnapshot",
                table: "MonthlyBills",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "AllocationEffectiveToSnapshot",
                table: "MonthlyBills",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllocationMatchMethod",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeEpfSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNameSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "EntitlementEffectiveFromSnapshot",
                table: "MonthlyBills",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "EntitlementEffectiveToSnapshot",
                table: "MonthlyBills",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntitlementMatchMethod",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MobileNumberSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BillExceptionResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillExceptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobileAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobileNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmployeeEpf = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AllocationEffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    AllocationEffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalExceptionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BillingYear = table.Column<int>(type: "int", nullable: false),
                    BillingMonth = table.Column<int>(type: "int", nullable: false),
                    ResolutionComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ResolvedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillExceptionResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillExceptionResolutions_BillExceptions_BillExceptionId",
                        column: x => x.BillExceptionId,
                        principalTable: "BillExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillExceptionResolutions_BillExceptionId",
                table: "BillExceptionResolutions",
                column: "BillExceptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillExceptionResolutions");

            migrationBuilder.DropColumn(
                name: "AllocationEffectiveFromSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "AllocationEffectiveToSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "AllocationMatchMethod",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "EmployeeEpfSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "EmployeeNameSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "EntitlementEffectiveFromSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "EntitlementEffectiveToSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "EntitlementMatchMethod",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "MobileNumberSnapshot",
                table: "MonthlyBills");
        }
    }
}
