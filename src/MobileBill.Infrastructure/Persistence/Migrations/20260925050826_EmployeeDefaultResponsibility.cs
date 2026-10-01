using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeDefaultResponsibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultResponsibility",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE e SET e.DefaultResponsibility = x.Responsibility
                FROM Employees e
                CROSS APPLY (SELECT TOP 1 mb.Responsibility FROM MonthlyBills mb
                             WHERE mb.EmployeeId = e.Id AND mb.Responsibility IS NOT NULL
                             ORDER BY mb.AssessedAt DESC) x;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultResponsibility",
                table: "Employees");
        }
    }
}
