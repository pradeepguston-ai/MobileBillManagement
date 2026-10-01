using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations;

public partial class MergeMobileAllocationEntitlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MobileAccounts_MobileNumber",
            table: "MobileAccounts");
        migrationBuilder.DropIndex(
            name: "IX_MobileAccounts_MobileNumber_IsActive",
            table: "MobileAccounts");

        migrationBuilder.AlterColumn<DateOnly>(
            name: "EntitlementEffectiveFromSnapshot",
            table: "MonthlyBills",
            type: "date",
            nullable: true,
            oldClrType: typeof(DateOnly),
            oldType: "date");

        migrationBuilder.AddColumn<decimal>(
            name: "MonthlyCreditLimit",
            table: "MobileAccounts",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "MonthlyRental",
            table: "MobileAccounts",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1
                FROM [dbo].[MobileAccounts] AS [a]
                WHERE (SELECT COUNT(*) FROM [dbo].[MobileEntitlements] AS [e]
                       WHERE [e].[MobileAccountId] = [a].[Id]) <> 1
            )
                THROW 51020, 'Each existing mobile allocation must have exactly one entitlement before merging. Resolve missing or multiple entitlement rows first.', 1;

            IF EXISTS (
                SELECT 1 FROM [dbo].[MobileAccounts]
                WHERE [IsActive] = 1
                GROUP BY [MobileNumber]
                HAVING COUNT(*) > 1
            )
                THROW 51021, 'Active mobile allocation numbers must be unique before merging.', 1;

            UPDATE [a]
            SET [a].[MonthlyCreditLimit] = [e].[MonthlyCreditLimit],
                [a].[MonthlyRental] = [e].[MonthlyRental]
            FROM [dbo].[MobileAccounts] AS [a]
            INNER JOIN [dbo].[MobileEntitlements] AS [e]
                ON [e].[MobileAccountId] = [a].[Id];
            """);

        migrationBuilder.AlterColumn<decimal>(
            name: "MonthlyCreditLimit",
            table: "MobileAccounts",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "decimal(18,2)",
            oldNullable: true);
        migrationBuilder.AlterColumn<decimal>(
            name: "MonthlyRental",
            table: "MobileAccounts",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "decimal(18,2)",
            oldNullable: true);

        migrationBuilder.DropTable(name: "MobileEntitlements");

        migrationBuilder.CreateIndex(
            name: "IX_MobileAccounts_MobileNumber",
            table: "MobileAccounts",
            column: "MobileNumber",
            unique: true,
            filter: "[IsActive] = 1");
        migrationBuilder.AddCheckConstraint(
            name: "CK_MobileAccounts_MonthlyCreditLimit",
            table: "MobileAccounts",
            sql: "[MonthlyCreditLimit] >= 0");
        migrationBuilder.AddCheckConstraint(
            name: "CK_MobileAccounts_MonthlyRental",
            table: "MobileAccounts",
            sql: "[MonthlyRental] >= 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MobileAccounts_MobileNumber",
            table: "MobileAccounts");
        migrationBuilder.DropCheckConstraint(
            name: "CK_MobileAccounts_MonthlyCreditLimit",
            table: "MobileAccounts");
        migrationBuilder.DropCheckConstraint(
            name: "CK_MobileAccounts_MonthlyRental",
            table: "MobileAccounts");

        migrationBuilder.CreateTable(
            name: "MobileEntitlements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MobileAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                MonthlyCreditLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                MonthlyRental = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MobileEntitlements", x => x.Id);
                table.CheckConstraint("CK_MobileEntitlements_EffectiveDates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.ForeignKey(
                    name: "FK_MobileEntitlements_MobileAccounts_MobileAccountId",
                    column: x => x.MobileAccountId,
                    principalTable: "MobileAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(
            name: "IX_MobileEntitlements_IsActive",
            table: "MobileEntitlements",
            column: "IsActive");
        migrationBuilder.CreateIndex(
            name: "IX_MobileEntitlements_MobileAccountId_EffectiveFrom",
            table: "MobileEntitlements",
            columns: new[] { "MobileAccountId", "EffectiveFrom" },
            unique: true);

        migrationBuilder.Sql("""
            INSERT INTO [dbo].[MobileEntitlements]
                ([Id], [MobileAccountId], [CreatedAtUtc], [CreatedBy], [EffectiveFrom],
                 [EffectiveTo], [IsActive], [MonthlyCreditLimit], [MonthlyRental], [UpdatedAtUtc], [UpdatedBy])
            SELECT NEWID(), [Id], [CreatedAtUtc], [CreatedBy], CONVERT(date, '1900-01-01'),
                   NULL, [IsActive], [MonthlyCreditLimit], [MonthlyRental], [UpdatedAtUtc], [UpdatedBy]
            FROM [dbo].[MobileAccounts];
            """);

        migrationBuilder.DropColumn(name: "MonthlyCreditLimit", table: "MobileAccounts");
        migrationBuilder.DropColumn(name: "MonthlyRental", table: "MobileAccounts");
        migrationBuilder.AlterColumn<DateOnly>(
            name: "EntitlementEffectiveFromSnapshot",
            table: "MonthlyBills",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1),
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_MobileAccounts_MobileNumber",
            table: "MobileAccounts",
            column: "MobileNumber");
        migrationBuilder.CreateIndex(
            name: "IX_MobileAccounts_MobileNumber_IsActive",
            table: "MobileAccounts",
            columns: new[] { "MobileNumber", "IsActive" });

        migrationBuilder.Sql("""
            CREATE TRIGGER [dbo].[TR_MobileEntitlements_PreventOverlappingPeriods]
            ON [dbo].[MobileEntitlements]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS
                (
                    SELECT 1
                    FROM inserted AS [incoming]
                    INNER JOIN [dbo].[MobileEntitlements] AS [existing]
                        ON [existing].[MobileAccountId] = [incoming].[MobileAccountId]
                        AND [existing].[Id] <> [incoming].[Id]
                    WHERE [existing].[EffectiveFrom] <= ISNULL([incoming].[EffectiveTo], CONVERT(date, '9999-12-31'))
                        AND [incoming].[EffectiveFrom] <= ISNULL([existing].[EffectiveTo], CONVERT(date, '9999-12-31'))
                )
                    THROW 50002, 'Mobile entitlement periods cannot overlap for the same mobile account.', 1;
            END
            """);
    }
}
