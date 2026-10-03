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

var game = new AddGame.AddGameInputModel { SportTypeId = 1, GameDate = DateTime.Today };
Check(!Valid(game), "Game with no season accepted.");
game.SeasonId = 1;
Check(Valid(game), "Game with a season and date rejected.");
game.GameDate = null;
Check(!Valid(game), "Game with no date accepted.");
game.GameDate = DateTime.Today;
game.FeeAmount = null;
game.MilesDriven = null;
Check(Valid(game), "Blank location, amount, or miles rejected.");
game.FeeAmount = -1m;
Check(!Valid(game), "Negative amount accepted.");
game.FeeAmount = null;
game.MilesDriven = -1m;
Check(!Valid(game), "Negative miles accepted.");

var editedGame = new EditGame.EditGameInputModel { SeasonId = 1, SportTypeId = 1, GameDate = DateTime.Today };
Check(Valid(editedGame), "Edit requires an optional location.");
editedGame.GameDate = null;
Check(!Valid(editedGame), "Edit with no date accepted.");
editedGame.GameDate = DateTime.Today;
editedGame.FeeAmount = null;
editedGame.MilesDriven = null;
Check(Valid(editedGame), "Edit rejects cleared amount or miles.");
editedGame.SeasonId = null;
Check(!Valid(editedGame), "Edit with no season accepted.");

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

// Calendar-year reports cross season boundaries, while season reports cross calendar years.
var reportGames = new List<Game>
{
    new() { Id = 1, GameDate = new DateTime(2026, 12, 31), SeasonId = 10, FeeAmount = 100, IsPaid = true },
    new() { Id = 2, GameDate = new DateTime(2027, 1, 1), SeasonId = 10, FeeAmount = 200 },
    new() { Id = 3, GameDate = new DateTime(2027, 1, 1), SeasonId = 20, FeeAmount = 300, IsPaid = true },
    new() { Id = 4, GameDate = new DateTime(2027, 6, 1), SeasonId = null, FeeAmount = 400 }
};
int[] reportSeasons = [10, 20, 30];
Check(ReportGameFilter.Apply(reportGames, "all", reportSeasons).Count == 4, "All games filter lost games.");
var yearGames = ReportGameFilter.Apply(reportGames, "year:2027", reportSeasons);
Check(yearGames.Select(x => x.Id).SequenceEqual(new[] { 2, 3, 4 }), "Year filter depends on season or includes another year.");
Check(yearGames.Sum(x => x.FeeAmount) == 900, "Year earnings are wrong.");
var seasonGames = ReportGameFilter.Apply(reportGames, "season:10", reportSeasons);
Check(seasonGames.Select(x => x.Id).SequenceEqual(new[] { 1, 2 }), "Season filter uses dates instead of assignment.");
Check(seasonGames.Sum(x => x.FeeAmount) == 300, "Season earnings are wrong.");
Check(ReportGameFilter.Apply(reportGames, "season:30", reportSeasons).Count == 0, "Empty season contains unrelated games.");
Check(ReportGameFilter.Apply(reportGames, "season:10", new[] { 20 }).Count == 0, "Unavailable season was accepted.");
Check(ReportGameFilter.Apply(reportGames, "year:2025", reportSeasons).Count == 0, "Empty year includes games.");
Check(ReportGameFilter.Apply(reportGames, "invalid", reportSeasons).Count == 0, "Invalid filter exposes all games.");
Console.WriteLine("Report year and season filter checks passed.");

var expenseInput = new ExpenseInputModel { Category = "Meals", Amount = 12.50m, ExpenseDate = DateTime.Today };
Check(Valid(expenseInput), "Valid expense was rejected.");
expenseInput.Amount = null;
Check(!Valid(expenseInput), "Expense without amount accepted.");
expenseInput.Amount = 0;
Check(!Valid(expenseInput), "Zero expense accepted.");
expenseInput.Amount = -5;
Check(!Valid(expenseInput), "Negative expense accepted.");
expenseInput.Amount = 12.345m;
Check(!Valid(expenseInput), "Expense with fractional cents accepted.");
expenseInput.Amount = 12.50m;
expenseInput.ExpenseDate = null;
Check(!Valid(expenseInput), "Expense without date accepted.");
expenseInput.ExpenseDate = DateTime.Today;
expenseInput.Category = "Unknown";
Check(!Valid(expenseInput), "Unknown expense category accepted.");
expenseInput.Category = "Other";
Check(Valid(expenseInput), "Expense with optional vendor and notes rejected.");
var expenseModel = db.Model.FindEntityType(typeof(Expense))!;
Check(expenseModel.FindProperty(nameof(Expense.Amount))!.GetPrecision() == 18 &&
      expenseModel.FindProperty(nameof(Expense.Amount))!.GetScale() == 2, "Expense amount precision is incorrect.");
Check(expenseModel.FindProperty(nameof(Expense.ExpenseDate))!.GetColumnType() == "date", "Expense date storage is incorrect.");
Check(expenseModel.GetIndexes().Any(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "UserId", "ExpenseDate" })),
    "Expense ownership/date index is missing.");
Console.WriteLine("Expense validation and model checks passed.");

var reportingExpenses = new List<Expense>
{
    new() { Id = 1, ExpenseDate = new DateTime(2026, 12, 30), Amount = 10 },
    new() { Id = 2, ExpenseDate = new DateTime(2026, 12, 31), Amount = 20 },
    new() { Id = 3, ExpenseDate = new DateTime(2027, 1, 1), Amount = 30 },
    new() { Id = 4, ExpenseDate = new DateTime(2027, 3, 1, 23, 59, 59), Amount = 40 },
    new() { Id = 5, ExpenseDate = new DateTime(2027, 3, 2), Amount = 50 }
};
var reportingSeasons = new List<Season>
{
    new() { Id = 10, StartDate = new DateTime(2026, 12, 31), EndDate = new DateTime(2027, 3, 1) },
    new() { Id = 20, StartDate = new DateTime(2027, 1, 1), EndDate = new DateTime(2027, 1, 1) }
};
Check(ReportExpenseFilter.Apply(reportingExpenses, "all", reportingSeasons).Count == 5, "All expense filter lost records.");
var yearExpenses = ReportExpenseFilter.Apply(reportingExpenses, "year:2027", reportingSeasons);
Check(yearExpenses.Select(x => x.Id).SequenceEqual(new[] { 3, 4, 5 }) && yearExpenses.Sum(x => x.Amount) == 120,
    "Expense calendar year filter is incorrect.");
var seasonExpenses = ReportExpenseFilter.Apply(reportingExpenses, "season:10", reportingSeasons);
Check(seasonExpenses.Select(x => x.Id).SequenceEqual(new[] { 2, 3, 4 }) && seasonExpenses.Sum(x => x.Amount) == 90,
    "Season expenses must use inclusive start/end dates across years.");
Check(ReportExpenseFilter.Apply(reportingExpenses, "season:20", reportingSeasons).Single().Id == 3,
    "Single-day/overlapping season expense filtering is incorrect.");
Check(ReportExpenseFilter.Apply(reportingExpenses, "season:99", reportingSeasons).Count == 0, "Unavailable expense season accepted.");
Check(ReportExpenseFilter.Apply(reportingExpenses, "invalid", reportingSeasons).Count == 0, "Invalid expense filter includes records.");
Console.WriteLine("Report expense date-range checks passed.");
