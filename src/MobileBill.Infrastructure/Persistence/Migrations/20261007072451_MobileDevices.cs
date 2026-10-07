using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MobileDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Imei1 = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Imei2 = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PurchaseCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WarrantyUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Supplier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StatusSince = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrentEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileDevices", x => x.Id);
                    table.CheckConstraint("CK_MobileDevices_PurchaseCost", "[PurchaseCost] >= 0");
                    table.ForeignKey(
                        name: "FK_MobileDevices_Employees_CurrentEmployeeId",
                        column: x => x.CurrentEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IssueNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReturnedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ReturnCondition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReturnReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReturnNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReplacementDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecoverableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceIssues_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceIssues_MobileDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "MobileDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceIssues_MobileDevices_ReplacementDeviceId",
                        column: x => x.ReplacementDeviceId,
                        principalTable: "MobileDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceIssues_DeviceId_IssuedOn",
                table: "DeviceIssues",
                columns: new[] { "DeviceId", "IssuedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceIssues_EmployeeId",
                table: "DeviceIssues",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceIssues_ReplacementDeviceId",
                table: "DeviceIssues",
                column: "ReplacementDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MobileDevices_AssetTag",
                table: "MobileDevices",
                column: "AssetTag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileDevices_CurrentEmployeeId",
                table: "MobileDevices",
                column: "CurrentEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_MobileDevices_Imei1",
                table: "MobileDevices",
                column: "Imei1",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileDevices_Imei2",
                table: "MobileDevices",
                column: "Imei2",
                unique: true,
                filter: "[Imei2] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MobileDevices_Status",
                table: "MobileDevices",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceIssues");

            migrationBuilder.DropTable(
                name: "MobileDevices");
        }
    }
}
