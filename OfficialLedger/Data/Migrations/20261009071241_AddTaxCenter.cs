using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficialLedger.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IncomeKind",
                table: "Game",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Review");

            migrationBuilder.AddColumn<string>(
                name: "MileageKind",
                table: "Game",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Review");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidDate",
                table: "Game",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayerName",
                table: "Game",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TravelDate",
                table: "Game",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TravelOrigin",
                table: "Game",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TravelPurpose",
                table: "Game",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TravelReimbursement",
                table: "Game",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BusinessUsePercent",
                table: "Expense",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidDate",
                table: "Expense",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReimbursedAmount",
                table: "Expense",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "TaxYearProfile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PreparerNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxYearProfile", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expense_UserId_PaidDate",
                table: "Expense",
                columns: new[] { "UserId", "PaidDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxYearProfile_UserId_TaxYear",
                table: "TaxYearProfile",
                columns: new[] { "UserId", "TaxYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxYearProfile");

            migrationBuilder.DropIndex(
                name: "IX_Expense_UserId_PaidDate",
                table: "Expense");

            migrationBuilder.DropColumn(
                name: "IncomeKind",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "MileageKind",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "PaidDate",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "PayerName",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "TravelDate",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "TravelOrigin",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "TravelPurpose",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "TravelReimbursement",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "BusinessUsePercent",
                table: "Expense");

            migrationBuilder.DropColumn(
                name: "PaidDate",
                table: "Expense");

            migrationBuilder.DropColumn(
                name: "ReimbursedAmount",
                table: "Expense");
        }
    }
}
