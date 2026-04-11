using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OfficialLedger.Data;

#nullable disable

namespace OfficialLedger.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260411100000_AddSeasonDates")]
    public partial class AddSeasonDates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Season",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1));

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Season",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1));

            migrationBuilder.Sql(@"
                UPDATE [Season]
                SET
                    [StartDate] = CASE [Name]
                        WHEN '2024 Baseball Season' THEN '2024-01-15'
                        WHEN '2025 Baseball Season' THEN '2025-01-15'
                        WHEN '2026 Baseball Season' THEN '2026-01-15'
                        ELSE [StartDate]
                    END,
                    [EndDate] = CASE [Name]
                        WHEN '2024 Baseball Season' THEN '2024-03-14'
                        WHEN '2025 Baseball Season' THEN '2025-03-14'
                        WHEN '2026 Baseball Season' THEN '2026-03-14'
                        ELSE [EndDate]
                    END;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Season");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Season");
        }
    }
}
