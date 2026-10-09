using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficialLedger.Migrations;

public partial class AddExpenseReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExpenseReceipt",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                ExpenseId = table.Column<int>(type: "int", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpenseReceipt", x => x.Id);
                table.ForeignKey(name: "FK_ExpenseReceipt_Expense_ExpenseId", column: x => x.ExpenseId,
                    principalTable: "Expense", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "IX_ExpenseReceipt_ExpenseId", table: "ExpenseReceipt", column: "ExpenseId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "ExpenseReceipt");
}
