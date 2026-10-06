using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimPool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPooled",
                table: "MonthlyBills",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DisconnectedOn",
                table: "MobileAccounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PooledOn",
                table: "MobileAccounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "MobileAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Assigned");

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "MobileAccounts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileAccounts_Status",
                table: "MobileAccounts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MobileAccounts_Status",
                table: "MobileAccounts");

            migrationBuilder.DropColumn(
                name: "IsPooled",
                table: "MonthlyBills");

            migrationBuilder.DropColumn(
                name: "DisconnectedOn",
                table: "MobileAccounts");

            migrationBuilder.DropColumn(
                name: "PooledOn",
                table: "MobileAccounts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "MobileAccounts");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "MobileAccounts");
        }
    }
}
