using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Mobile allocations no longer have an effective period. Every drop is guarded so the migration also applies to
    /// databases where these objects were already removed by hand (the trigger, however, is left behind when only the
    /// columns are dropped, and it would reject every insert/update because its body references them).
    /// </remarks>
    public partial class RemoveMobileAccountEffectiveDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.triggers WHERE [name] = 'TR_MobileAccounts_PreventOverlappingActiveAllocations')
                    DROP TRIGGER [dbo].[TR_MobileAccounts_PreventOverlappingActiveAllocations];
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_MobileAccounts_MobileNumber_EffectiveFrom' AND [object_id] = OBJECT_ID('[dbo].[MobileAccounts]'))
                    DROP INDEX [IX_MobileAccounts_MobileNumber_EffectiveFrom] ON [dbo].[MobileAccounts];
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = 'CK_MobileAccounts_EffectiveDates' AND [parent_object_id] = OBJECT_ID('[dbo].[MobileAccounts]'))
                    ALTER TABLE [dbo].[MobileAccounts] DROP CONSTRAINT [CK_MobileAccounts_EffectiveDates];
                """);

            DropColumnIfExists(migrationBuilder, "MobileAccounts", "EffectiveFrom");
            DropColumnIfExists(migrationBuilder, "MobileAccounts", "EffectiveTo");
            DropColumnIfExists(migrationBuilder, "MonthlyBills", "AllocationEffectiveFromSnapshot");
            DropColumnIfExists(migrationBuilder, "MonthlyBills", "AllocationEffectiveToSnapshot");
            DropColumnIfExists(migrationBuilder, "BillExceptionResolutions", "AllocationEffectiveFrom");
            DropColumnIfExists(migrationBuilder, "BillExceptionResolutions", "AllocationEffectiveTo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AllocationEffectiveFromSnapshot",
                table: "MonthlyBills",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "AllocationEffectiveToSnapshot",
                table: "MonthlyBills",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveFrom",
                table: "MobileAccounts",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveTo",
                table: "MobileAccounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AllocationEffectiveFrom",
                table: "BillExceptionResolutions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "AllocationEffectiveTo",
                table: "BillExceptionResolutions",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileAccounts_MobileNumber_EffectiveFrom",
                table: "MobileAccounts",
                columns: new[] { "MobileNumber", "EffectiveFrom" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MobileAccounts_EffectiveDates",
                table: "MobileAccounts",
                sql: "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_MobileAccounts_PreventOverlappingActiveAllocations]
                ON [dbo].[MobileAccounts]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS [incoming]
                        INNER JOIN [dbo].[MobileAccounts] AS [existing]
                            ON [existing].[MobileNumber] = [incoming].[MobileNumber]
                            AND [existing].[Id] <> [incoming].[Id]
                        WHERE [incoming].[IsActive] = 1
                            AND [existing].[IsActive] = 1
                            AND [existing].[EffectiveFrom] <= ISNULL([incoming].[EffectiveTo], CONVERT(date, '9999-12-31'))
                            AND [incoming].[EffectiveFrom] <= ISNULL([existing].[EffectiveTo], CONVERT(date, '9999-12-31'))
                    )
                    BEGIN
                        THROW 50001, 'Active mobile account allocations cannot overlap for the same mobile number.', 1;
                    END
                END
                """);
        }

        private static void DropColumnIfExists(MigrationBuilder migrationBuilder, string table, string column) =>
            migrationBuilder.Sql($"""
                DECLARE @constraintName sysname;
                SELECT @constraintName = [dc].[name]
                FROM sys.default_constraints AS [dc]
                INNER JOIN sys.columns AS [c]
                    ON [c].[object_id] = [dc].[parent_object_id]
                    AND [c].[column_id] = [dc].[parent_column_id]
                WHERE [dc].[parent_object_id] = OBJECT_ID(N'[dbo].[{table}]')
                    AND [c].[name] = N'{column}';

                IF @constraintName IS NOT NULL
                BEGIN
                    DECLARE @dropConstraintSql nvarchar(max) = N'ALTER TABLE [dbo].[{table}] DROP CONSTRAINT ' + QUOTENAME(@constraintName);
                    EXEC sys.sp_executesql @dropConstraintSql;
                END;

                IF COL_LENGTH('[dbo].[{table}]', '{column}') IS NOT NULL
                    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
                """);
    }
}
