using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficialLedger.Migrations;

public partial class AddExpenses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Expense",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ExpenseDate = table.Column<DateTime>(type: "date", nullable: false),
                Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Vendor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                BusinessPurpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Expense", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_Expense_UserId_ExpenseDate", table: "Expense", columns: new[] { "UserId", "ExpenseDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Expense");
}
