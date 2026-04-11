using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OfficialLedger.Data;

#nullable disable

namespace OfficialLedger.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260411113000_AddFootballSeason")]
    public partial class AddFootballSeason : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [Season] WHERE [Name] = '2025 Football Season')
                BEGIN
                    INSERT INTO [Season] ([Name], [StartDate], [EndDate])
                    VALUES ('2025 Football Season', '2025-12-12', '2026-03-21');
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [Season] WHERE [Name] = '2025 Football Season';");
        }
    }
}
