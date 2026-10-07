using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MobilePackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PackageCodeSnapshot",
                table: "MonthlyBills",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PackageId",
                table: "MobileAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MobilePackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MonthlyRental = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalWithTax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultCreditLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobilePackages", x => x.Id);
                    table.CheckConstraint("CK_MobilePackages_DefaultCreditLimit", "[DefaultCreditLimit] >= 0");
                    table.CheckConstraint("CK_MobilePackages_MonthlyRental", "[MonthlyRental] >= 0");
                    table.CheckConstraint("CK_MobilePackages_TotalWithTax", "[TotalWithTax] >= 0");
                    table.ForeignKey(
                        name: "FK_MobilePackages_TelecomProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "TelecomProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileAccounts_PackageId",
                table: "MobileAccounts",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePackages_ProviderId_Code",
                table: "MobilePackages",
                columns: new[] { "ProviderId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MobileAccounts_MobilePackages_PackageId",
                table: "MobileAccounts",
                column: "PackageId",
                principalTable: "MobilePackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The first package, for the Dialog provider, and every active allocation on its 700.00 rental.
            // Skipped where there is no Dialog provider (for example a new, empty database).
            migrationBuilder.Sql("""
                DECLARE @provider uniqueidentifier = (SELECT TOP 1 Id FROM TelecomProviders WHERE Name = N'Dialog' ORDER BY Code);
                IF @provider IS NOT NULL AND NOT EXISTS (SELECT 1 FROM MobilePackages WHERE ProviderId = @provider AND Code = N'PPU23_700')
                BEGIN
                    DECLARE @package uniqueidentifier = NEWID();
                    INSERT INTO MobilePackages (Id, Code, ProviderId, Description, MonthlyRental, TotalWithTax, DefaultCreditLimit, IsActive, CreatedBy)
                    VALUES (@package, N'PPU23_700', @provider, N'Unlimited any network calls & 5 GB data per month', 700.00, 940.00, 1000.00, 1, N'system');
                    UPDATE MobileAccounts SET PackageId = @package WHERE IsActive = 1 AND MonthlyRental = 700.00 AND PackageId IS NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MobileAccounts_MobilePackages_PackageId",
                table: "MobileAccounts");

            migrationBuilder.DropTable(
                name: "MobilePackages");

            migrationBuilder.DropIndex(
                name: "IX_MobileAccounts_PackageId",
                table: "MobileAccounts");

            migrationBuilder.DropColumn(
                name: "PackageCodeSnapshot",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "PackageId",
                table: "MobileAccounts");
        }
    }
}
