using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OfficialLedger.Data;

#nullable disable

namespace OfficialLedger.Migrations;

public partial class AddUserSeasons : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "UserId", table: "Season", type: "nvarchar(450)", maxLength: 450, nullable: true);
        migrationBuilder.AddColumn<string>(name: "SportImage", table: "Season", type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "baseball");
        migrationBuilder.Sql("UPDATE [Season] SET [SportImage] = 'football' WHERE [Name] = '2025 Football Season';");
        migrationBuilder.AddColumn<int>(name: "SeasonId", table: "Game", type: "int", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Season_UserId", table: "Season", column: "UserId");
        migrationBuilder.CreateIndex(name: "IX_Game_SeasonId", table: "Game", column: "SeasonId");
        migrationBuilder.AddForeignKey(name: "FK_Game_Season_SeasonId", table: "Game", column: "SeasonId", principalTable: "Season", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Game_Season_SeasonId", table: "Game");
        migrationBuilder.DropIndex(name: "IX_Game_SeasonId", table: "Game");
        migrationBuilder.DropIndex(name: "IX_Season_UserId", table: "Season");
        migrationBuilder.DropColumn(name: "SeasonId", table: "Game");
        migrationBuilder.DropColumn(name: "UserId", table: "Season");
        migrationBuilder.DropColumn(name: "SportImage", table: "Season");
    }
}
