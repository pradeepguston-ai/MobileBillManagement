using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportDistribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportDistributionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FactoryCodes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CategoryCodes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDistributionRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportDistributionRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDistributionRecipients", x => x.Id);
                    table.CheckConstraint("CK_ReportDistributionRecipients_UserOrEmail", "([UserId] IS NULL AND [Email] IS NOT NULL) OR ([UserId] IS NOT NULL AND [Email] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ReportDistributionRecipients_ReportDistributionRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "ReportDistributionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReportDistributionRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportEmailDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FactoryCodes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CategoryCodes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Recipients = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportEmailDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportEmailDeliveries_BillBatches_BillBatchId",
                        column: x => x.BillBatchId,
                        principalTable: "BillBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportEmailDeliveries_ReportDistributionRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "ReportDistributionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDistributionRecipients_RuleId",
                table: "ReportDistributionRecipients",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDistributionRecipients_UserId",
                table: "ReportDistributionRecipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDistributionRules_IsActive",
                table: "ReportDistributionRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReportEmailDeliveries_BillBatchId_CreatedAtUtc",
                table: "ReportEmailDeliveries",
                columns: new[] { "BillBatchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportEmailDeliveries_RuleId",
                table: "ReportEmailDeliveries",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportEmailDeliveries_Status_NextAttemptAt",
                table: "ReportEmailDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportDistributionRecipients");

            migrationBuilder.DropTable(
                name: "ReportEmailDeliveries");

            migrationBuilder.DropTable(
                name: "ReportDistributionRules");
        }
    }
}
