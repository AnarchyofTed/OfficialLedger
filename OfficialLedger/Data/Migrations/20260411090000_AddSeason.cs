using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OfficialLedger.Data;

#nullable disable

namespace OfficialLedger.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260411090000_AddSeason")]
    public partial class AddSeason : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Season",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Season", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO [Season] ([Name]) VALUES
                ('2024 Baseball Season'),
                ('2025 Baseball Season'),
                ('2026 Baseball Season');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Season");
        }
    }
}
