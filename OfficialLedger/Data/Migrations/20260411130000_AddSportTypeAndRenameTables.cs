using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficialLedger.Migrations
{
    public partial class AddSportTypeAndRenameTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Leagues_LeagueId",
                table: "Games");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Games",
                table: "Games");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Leagues",
                table: "Leagues");

            migrationBuilder.RenameTable(
                name: "Games",
                newName: "Game");

            migrationBuilder.RenameTable(
                name: "Leagues",
                newName: "League");

            migrationBuilder.RenameIndex(
                name: "IX_Games_LeagueId",
                table: "Game",
                newName: "IX_Game_LeagueId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Game",
                table: "Game",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_League",
                table: "League",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "SportType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportType", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO SportType (Name)
                SELECT v.Name
                FROM (VALUES
                    ('Baseball'),
                    ('Basketball'),
                    ('Football'),
                    ('Soccer'),
                    ('Softball'),
                    ('Volleyball'),
                    ('Other')
                ) v(Name)
                WHERE NOT EXISTS (SELECT 1 FROM SportType st WHERE st.Name = v.Name);
                """);

            migrationBuilder.AddColumn<int>(
                name: "SportTypeId",
                table: "Game",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO SportType (Name)
                SELECT DISTINCT Sport
                FROM Game
                WHERE Sport IS NOT NULL AND LTRIM(RTRIM(Sport)) <> '';
                """);

            migrationBuilder.Sql("""
                UPDATE g
                SET g.SportTypeId = st.Id
                FROM Game g
                INNER JOIN SportType st ON st.Name = g.Sport;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM SportType WHERE Name = 'Other')
                BEGIN
                    INSERT INTO SportType (Name) VALUES ('Other');
                END

                UPDATE g
                SET g.SportTypeId = st.Id
                FROM Game g
                CROSS JOIN (SELECT TOP 1 Id FROM SportType WHERE Name = 'Other') st
                WHERE g.SportTypeId IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "SportTypeId",
                table: "Game",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Game_SportTypeId",
                table: "Game",
                column: "SportTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Game_League_LeagueId",
                table: "Game",
                column: "LeagueId",
                principalTable: "League",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Game_SportType_SportTypeId",
                table: "Game",
                column: "SportTypeId",
                principalTable: "SportType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "Sport",
                table: "Game");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Game_League_LeagueId",
                table: "Game");

            migrationBuilder.DropForeignKey(
                name: "FK_Game_SportType_SportTypeId",
                table: "Game");

            migrationBuilder.DropTable(
                name: "SportType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Game",
                table: "Game");

            migrationBuilder.DropPrimaryKey(
                name: "PK_League",
                table: "League");

            migrationBuilder.DropIndex(
                name: "IX_Game_SportTypeId",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "SportTypeId",
                table: "Game");

            migrationBuilder.AddColumn<string>(
                name: "Sport",
                table: "Game",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.RenameTable(
                name: "Game",
                newName: "Games");

            migrationBuilder.RenameTable(
                name: "League",
                newName: "Leagues");

            migrationBuilder.RenameIndex(
                name: "IX_Game_LeagueId",
                table: "Games",
                newName: "IX_Games_LeagueId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Games",
                table: "Games",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Leagues",
                table: "Leagues",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Leagues_LeagueId",
                table: "Games",
                column: "LeagueId",
                principalTable: "Leagues",
                principalColumn: "Id");
        }
    }
}
