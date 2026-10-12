using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThirdPartyCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThirdPartyCompanyCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ThirdPartyCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThirdPartyCompanies", x => x.Id);
                    table.UniqueConstraint("AK_ThirdPartyCompanies_Code", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ThirdPartyCompanyCode",
                table: "Employees",
                column: "ThirdPartyCompanyCode");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_ThirdPartyCompanies_ThirdPartyCompanyCode",
                table: "Employees",
                column: "ThirdPartyCompanyCode",
                principalTable: "ThirdPartyCompanies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_ThirdPartyCompanies_ThirdPartyCompanyCode",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "ThirdPartyCompanies");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ThirdPartyCompanyCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ThirdPartyCompanyCode",
                table: "Employees");
        }
    }
}
