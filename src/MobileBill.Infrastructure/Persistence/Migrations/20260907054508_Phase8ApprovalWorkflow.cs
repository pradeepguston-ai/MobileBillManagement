using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileBill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8ApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "ApprovalHistories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "ApprovalHistories",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NewStatus",
                table: "ApprovalHistories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreviousStatus",
                table: "ApprovalHistories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ApprovalHistories",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkflowRole",
                table: "ApprovalHistories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Action",
                table: "ApprovalHistories");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "ApprovalHistories");

            migrationBuilder.DropColumn(
                name: "NewStatus",
                table: "ApprovalHistories");

            migrationBuilder.DropColumn(
                name: "PreviousStatus",
                table: "ApprovalHistories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ApprovalHistories");

            migrationBuilder.DropColumn(
                name: "WorkflowRole",
                table: "ApprovalHistories");
        }
    }
}
