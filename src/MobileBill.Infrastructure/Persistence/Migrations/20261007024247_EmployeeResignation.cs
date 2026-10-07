using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeResignation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResignationReason",
                table: "Employees",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ResignedOn",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ResignedOn",
                table: "Employees",
                column: "ResignedOn");

            // Employees already resigned (inactive, with an EmployeeResigned audit entry) get the date from their
            // latest resignation, and the reason from a number they released to the SIM Pool, or "Resigned".
            migrationBuilder.Sql("""
                    UPDATE e
                    SET e.ResignedOn = r.ResignedOn,
                        e.ResignationReason = COALESCE((SELECT TOP 1 LEFT(m.StatusReason, 250) FROM MobileAccounts m
                                                        WHERE m.EmployeeId = e.Id AND m.PooledOn = r.ResignedOn AND m.StatusReason IS NOT NULL), N'Resigned')
                    FROM Employees e
                    CROSS APPLY (SELECT TOP 1 TRY_CONVERT(date, JSON_VALUE(a.AfterDataJson, '$.ResignedOn')) AS ResignedOn
                                 FROM AuditLogs a
                                 WHERE a.EntityName = N'Employee' AND a.EntityId = e.Id AND a.Action = N'EmployeeResigned'
                                 ORDER BY a.PerformedAt DESC) r
                    WHERE e.IsActive = 0 AND r.ResignedOn IS NOT NULL;
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_ResignedOn",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ResignationReason",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ResignedOn",
                table: "Employees");
        }
    }
}
