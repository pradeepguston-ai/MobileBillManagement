using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeReferencesByCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Departments_DepartmentId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Designations_DesignationId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_EmployeeCategories_CategoryId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Factories_FactoryId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Factories_Code",
                table: "Factories");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CategoryId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DesignationId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_FactoryId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeCategories_Code",
                table: "EmployeeCategories");

            migrationBuilder.DropIndex(
                name: "IX_Designations_Code",
                table: "Designations");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Code",
                table: "Departments");

            // Added nullable first so existing rows can be backfilled from the old GUID columns
            // before either column is dropped or the new one is tightened to NOT NULL.
            migrationBuilder.AddColumn<string>(
                name: "CategoryCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartmentCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesignationCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FactoryCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartmentCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesignationCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FactoryCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE e SET e.CategoryCode = c.Code FROM Employees e JOIN EmployeeCategories c ON e.CategoryId = c.Id;
                UPDATE e SET e.DesignationCode = d.Code FROM Employees e JOIN Designations d ON e.DesignationId = d.Id;
                UPDATE e SET e.FactoryCode = f.Code FROM Employees e JOIN Factories f ON e.FactoryId = f.Id;
                UPDATE e SET e.DepartmentCode = dp.Code FROM Employees e JOIN Departments dp ON e.DepartmentId = dp.Id;
                """);

            migrationBuilder.Sql("""
                UPDATE m SET m.CategoryCodeSnapshot = c.Code FROM MonthlyBills m JOIN EmployeeCategories c ON m.CategoryIdSnapshot = c.Id;
                UPDATE m SET m.DesignationCodeSnapshot = d.Code FROM MonthlyBills m JOIN Designations d ON m.DesignationIdSnapshot = d.Id;
                UPDATE m SET m.FactoryCodeSnapshot = f.Code FROM MonthlyBills m JOIN Factories f ON m.FactoryIdSnapshot = f.Id;
                UPDATE m SET m.DepartmentCodeSnapshot = dp.Code FROM MonthlyBills m JOIN Departments dp ON m.DepartmentIdSnapshot = dp.Id;
                """);

            migrationBuilder.DropColumn(
                name: "CategoryIdSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DepartmentIdSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DesignationIdSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "FactoryIdSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DesignationId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FactoryId",
                table: "Employees");

            migrationBuilder.AlterColumn<string>(
                name: "CategoryCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DepartmentCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DesignationCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FactoryCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CategoryCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DepartmentCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DesignationCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FactoryCode",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Factories_Code",
                table: "Factories",
                column: "Code");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EmployeeCategories_Code",
                table: "EmployeeCategories",
                column: "Code");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Designations_Code",
                table: "Designations",
                column: "Code");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Departments_Code",
                table: "Departments",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CategoryCode",
                table: "Employees",
                column: "CategoryCode");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentCode",
                table: "Employees",
                column: "DepartmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DesignationCode",
                table: "Employees",
                column: "DesignationCode");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FactoryCode",
                table: "Employees",
                column: "FactoryCode");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Departments_DepartmentCode",
                table: "Employees",
                column: "DepartmentCode",
                principalTable: "Departments",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Designations_DesignationCode",
                table: "Employees",
                column: "DesignationCode",
                principalTable: "Designations",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_EmployeeCategories_CategoryCode",
                table: "Employees",
                column: "CategoryCode",
                principalTable: "EmployeeCategories",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Factories_FactoryCode",
                table: "Employees",
                column: "FactoryCode",
                principalTable: "Factories",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Departments_DepartmentCode",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Designations_DesignationCode",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_EmployeeCategories_CategoryCode",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Factories_FactoryCode",
                table: "Employees");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Factories_Code",
                table: "Factories");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CategoryCode",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DepartmentCode",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DesignationCode",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_FactoryCode",
                table: "Employees");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EmployeeCategories_Code",
                table: "EmployeeCategories");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Designations_Code",
                table: "Designations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Departments_Code",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "CategoryCodeSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DepartmentCodeSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DesignationCodeSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "FactoryCodeSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "CategoryCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DepartmentCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DesignationCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FactoryCode",
                table: "Employees");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryIdSnapshot",
                table: "MonthlyBills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentIdSnapshot",
                table: "MonthlyBills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DesignationIdSnapshot",
                table: "MonthlyBills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FactoryIdSnapshot",
                table: "MonthlyBills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DesignationId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FactoryId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Factories_Code",
                table: "Factories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CategoryId",
                table: "Employees",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DesignationId",
                table: "Employees",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FactoryId",
                table: "Employees",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCategories_Code",
                table: "EmployeeCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Designations_Code",
                table: "Designations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Departments_DepartmentId",
                table: "Employees",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Designations_DesignationId",
                table: "Employees",
                column: "DesignationId",
                principalTable: "Designations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_EmployeeCategories_CategoryId",
                table: "Employees",
                column: "CategoryId",
                principalTable: "EmployeeCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Factories_FactoryId",
                table: "Employees",
                column: "FactoryId",
                principalTable: "Factories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
