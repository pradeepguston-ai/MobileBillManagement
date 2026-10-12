using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeNameWithInitials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameWithInitials",
                table: "Employees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameWithInitials",
                table: "Employees");
        }
    }
}
