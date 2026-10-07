using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficialLedger.Migrations;

public partial class AddSignupEmailsAndAddress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Address", table: "AspNetUsers",
            type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.CreateTable(
            name: "SignupEmails",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SignupEmails", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_SignupEmails_NormalizedEmail", table: "SignupEmails",
            column: "NormalizedEmail", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SignupEmails");
        migrationBuilder.DropColumn(name: "Address", table: "AspNetUsers");
    }
}
