using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentSectionsAndSubSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SectionCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SectionNameSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubSectionCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubSectionNameSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SectionCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubSectionCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartmentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.UniqueConstraint("AK_Sections_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_Sections_Departments_DepartmentCode",
                        column: x => x.DepartmentCode,
                        principalTable: "Departments",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SectionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubSections", x => x.Id);
                    table.UniqueConstraint("AK_SubSections_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_SubSections_Sections_SectionCode",
                        column: x => x.SectionCode,
                        principalTable: "Sections",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_SectionCode",
                table: "Employees",
                column: "SectionCode");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_SubSectionCode",
                table: "Employees",
                column: "SubSectionCode");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_DepartmentCode",
                table: "Sections",
                column: "DepartmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_SubSections_SectionCode",
                table: "SubSections",
                column: "SectionCode");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Sections_SectionCode",
                table: "Employees",
                column: "SectionCode",
                principalTable: "Sections",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_SubSections_SubSectionCode",
                table: "Employees",
                column: "SubSectionCode",
                principalTable: "SubSections",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Sections_SectionCode",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_SubSections_SubSectionCode",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "SubSections");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropIndex(
                name: "IX_Employees_SectionCode",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_SubSectionCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SectionCodeSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "SectionNameSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "SubSectionCodeSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "SubSectionNameSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "SectionCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SubSectionCode",
                table: "Employees");
        }
    }
}
