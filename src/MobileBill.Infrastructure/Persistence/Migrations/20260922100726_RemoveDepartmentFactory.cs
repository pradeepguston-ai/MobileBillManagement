using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDepartmentFactory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Factories_FactoryId",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_FactoryId_Code",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "FactoryId",
                table: "Departments");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Departments_Code",
                table: "Departments");

            migrationBuilder.AddColumn<Guid>(
                name: "FactoryId",
                table: "Departments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Departments_FactoryId_Code",
                table: "Departments",
                columns: new[] { "FactoryId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Factories_FactoryId",
                table: "Departments",
                column: "FactoryId",
                principalTable: "Factories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
