using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OfficialLedger.Components.Pages;
using OfficialLedger.Data;
using OfficialLedger.Models;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static bool Valid(object model) => Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true);

var season = new AddSeason.SeasonInputModel
{
    Name = "Spring League", StartDate = new DateTime(2027, 1, 1), EndDate = new DateTime(2027, 5, 31), SportImage = "baseball"
};
Check(Valid(season), "Valid season rejected.");
season.EndDate = season.StartDate;
Check(Valid(season), "Single-day season rejected.");
season.StartDate = new DateTime(2026, 12, 1);
season.EndDate = new DateTime(2027, 3, 1);
Check(Valid(season), "Cross-year season rejected.");
season.EndDate = season.StartDate.Value.AddDays(-1);
Check(!Valid(season), "Reversed dates accepted.");
season.EndDate = season.StartDate;
season.Name = "   ";
Check(!Valid(season), "Blank name accepted.");
season.Name = "Spring League";
season.SportImage = "../../file";
Check(!Valid(season), "Unknown image accepted.");
Check(SeasonImages.Path("../../file") == "/images/sports/other.svg", "Image fallback is unsafe.");

var game = new AddGame.AddGameInputModel { SportTypeId = 1, LocationName = "Field" };
Check(!Valid(game), "Game with no season accepted.");
game.SeasonId = 1;
Check(Valid(game), "Game with a season rejected.");

using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer("Server=localhost;Database=SeasonChecks;Integrated Security=true;TrustServerCertificate=true").Options);
var gameModel = db.Model.FindEntityType(typeof(Game))!;
Check(gameModel.FindProperty(nameof(Game.SeasonId))!.IsNullable, "Legacy games must retain nullable season assignments.");
Check(gameModel.GetForeignKeys().Single(x => x.PrincipalEntityType.ClrType == typeof(Season)).DeleteBehavior == DeleteBehavior.Restrict,
    "Deleting a season must not cascade-delete games.");
var seasonModel = db.Model.FindEntityType(typeof(Season))!;
Check(seasonModel.FindProperty(nameof(Season.UserId))!.GetMaxLength() == 450, "Season user ID mapping is incorrect.");
Check(seasonModel.FindProperty(nameof(Season.SportImage))!.GetMaxLength() == 32, "Season image mapping is incorrect.");
Console.WriteLine("Season validation and EF model checks passed.");
