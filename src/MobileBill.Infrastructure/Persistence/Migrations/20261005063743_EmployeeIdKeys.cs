using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    // EPF becomes unique per factory (Factory + EPF), and mobile allocations link to employees by Id instead of EPF.
    // Hand-written so existing allocations keep their holder: the Id is copied from the EPF match before the EPF
    // link is removed, and the migration stops if any allocation cannot be matched.
    /// <inheritdoc />
    public partial class EmployeeIdKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "MobileAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE m SET m.[EmployeeId] = e.[Id]
                FROM [MobileAccounts] m
                INNER JOIN [Employees] e ON e.[EPF] = m.[EmployeeEpf];
                """);
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [MobileAccounts] WHERE [EmployeeId] IS NULL)
                    THROW 50001, 'EmployeeIdKeys: some mobile allocations could not be matched to an employee by EPF.', 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "EmployeeId",
                table: "MobileAccounts",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_MobileAccounts_Employees_EmployeeEpf",
                table: "MobileAccounts");

            migrationBuilder.DropIndex(
                name: "IX_MobileAccounts_EmployeeEpf",
                table: "MobileAccounts");

            migrationBuilder.DropColumn(
                name: "EmployeeEpf",
                table: "MobileAccounts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Employees_EPF",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_FactoryCode",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_MobileAccounts_EmployeeId",
                table: "MobileAccounts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EPF",
                table: "Employees",
                column: "EPF");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FactoryCode_EPF",
                table: "Employees",
                columns: new[] { "FactoryCode", "EPF" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MobileAccounts_Employees_EmployeeId",
                table: "MobileAccounts",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // So rows inserted directly with SQL (for example a bulk load) get an Id without listing one; the app supplies its own.
            migrationBuilder.Sql("ALTER TABLE [Employees] ADD CONSTRAINT [DF_Employees_Id] DEFAULT NEWID() FOR [Id];");
            migrationBuilder.Sql("ALTER TABLE [MobileAccounts] ADD CONSTRAINT [DF_MobileAccounts_Id] DEFAULT NEWID() FOR [Id];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [MobileAccounts] DROP CONSTRAINT [DF_MobileAccounts_Id];");
            migrationBuilder.Sql("ALTER TABLE [Employees] DROP CONSTRAINT [DF_Employees_Id];");

            migrationBuilder.DropForeignKey(
                name: "FK_MobileAccounts_Employees_EmployeeId",
                table: "MobileAccounts");

            migrationBuilder.DropIndex(
                name: "IX_MobileAccounts_EmployeeId",
                table: "MobileAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Employees_EPF",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_FactoryCode_EPF",
                table: "Employees");

            // Fails if the same EPF now exists in two factories; those must be resolved before going back.
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Employees_EPF",
                table: "Employees",
                column: "EPF");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FactoryCode",
                table: "Employees",
                column: "FactoryCode");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeEpf",
                table: "MobileAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE m SET m.[EmployeeEpf] = e.[EPF]
                FROM [MobileAccounts] m
                INNER JOIN [Employees] e ON e.[Id] = m.[EmployeeId];
                """);

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

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "MobileAccounts");

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
    }
}
