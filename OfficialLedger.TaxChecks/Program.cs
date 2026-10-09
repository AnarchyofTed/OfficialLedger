using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using OfficialLedger.Data;
using OfficialLedger.Models;
using OfficialLedger.Tax;

var count = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAILED: " + description);
    count++;
    Console.WriteLine("PASS: " + description);
}
var owner = new ApplicationUser { Id = "owner", FirstName = "Sample", LastName = "Official", UserName = "sample", Email = "sample@example.com" };
var games = new List<Game>
{
    new() { Id = 1, UserId = "owner", SportTypeId = 1, GameDate = new(2025, 12, 31), IsPaid = true, PaidDate = new(2026, 1, 1), FeeAmount = 100m, TravelReimbursement = 10m, PayerName = "League", IncomeKind = "Contractor", MilesDriven = 75m, TravelDate = new(2026, 1, 1), MileageKind = "Business", TravelOrigin = "Home via another work site", LocationName = "Field", TravelPurpose = "Officiate tournament" },
    new() { Id = 2, UserId = "owner", SportTypeId = 1, GameDate = new(2026, 12, 31), IsPaid = true, PaidDate = new(2027, 1, 1), FeeAmount = 150m, PayerName = "League", IncomeKind = "Contractor", MilesDriven = 80m, TravelDate = new(2027, 1, 1) },
    new() { Id = 3, UserId = "owner", SportTypeId = 1, GameDate = new(2026, 5, 5), IsPaid = false, PaidDate = new(2026, 5, 6), FeeAmount = 200m, PayerName = "League", IncomeKind = "Contractor", MilesDriven = 40m, TravelDate = new(2026, 5, 5), MileageKind = "Personal", TravelOrigin = "Home", LocationName = "Field", TravelPurpose = "Commute" },
    new() { Id = 4, UserId = "owner", SportTypeId = 1, GameDate = new(2024, 8, 1), IsPaid = true, FeeAmount = 75m },
    new() { Id = 5, UserId = "owner", SportTypeId = 1, GameDate = new(2026, 6, 6), IsPaid = true, PaidDate = new(2026, 6, 6), FeeAmount = 50m, PayerName = "  =DANGEROUS()", IncomeKind = "Employee", MilesDriven = 10m, Notes = "<script>private-note</script>\ncomma,quote\"" },
    new() { Id = 6, UserId = "other", SportTypeId = 1, GameDate = new(2026, 4, 1), IsPaid = true, PaidDate = new(2026, 4, 1), FeeAmount = 999m, PayerName = "OTHER-USER-SECRET" }
};
var expenses = new List<Expense>
{
    new() { Id = 11, UserId = "owner", ExpenseDate = new(2025, 12, 31), PaidDate = new(2026, 1, 1), Amount = 120m, BusinessUsePercent = 25m, ReimbursedAmount = 20m, Category = "Equipment", Vendor = "Academy", BusinessPurpose = "Equipment" },
    new() { Id = 12, UserId = "owner", ExpenseDate = new(2026, 12, 31), PaidDate = new(2027, 1, 1), Amount = 200m, BusinessUsePercent = 100m },
    new() { Id = 13, UserId = "owner", ExpenseDate = new(2023, 6, 1), Amount = 80m },
    new() { Id = 14, UserId = "owner", ExpenseDate = new(2026, 5, 1), PaidDate = new(2026, 5, 1), Amount = 40m, Vendor = "\t=FORMULA()", BusinessPurpose = "<img src=x onerror=alert(1)>" },
    new() { Id = 15, UserId = "other", ExpenseDate = new(2026, 5, 1), PaidDate = new(2026, 5, 1), Amount = 999m, Vendor = "OTHER-USER-SECRET" },
    new() { Id = 16, UserId = "owner", ExpenseDate = new(2026, 5, 1), PaidDate = new(2026, 5, 1), Amount = .05m, BusinessUsePercent = 10m },
    new() { Id = 17, UserId = "owner", ExpenseDate = new(2026, 5, 1), PaidDate = new(2026, 5, 1), Amount = .05m, BusinessUsePercent = 10m }
};
var metadata = new[] { new TaxReceipt(21, 11, "../same.jpg", "image/jpeg", 6), new TaxReceipt(22, 11, "../same.jpg", "image/jpeg", 6), new TaxReceipt(23, 15, "PRIVATE.jpg", "image/jpeg", 6) };
var profile = new TaxYearProfile { UserId = "owner", TaxYear = 2026, BusinessName = "Sample officiating", PreparerNotes = "Check vehicle method. <b>not markup</b>" };
var report = TaxReport.Build(2026, owner, games, expenses, metadata, profile, new(2026, 10, 9, 7, 0, 0, DateTimeKind.Utc));
Check(report.ReceivedGames.Select(x => x.Id).SequenceEqual([1, 5]), "Received-date boundaries include prior-year work and exclude next-year payments and unpaid flags");
Check(report.TotalReceived == 160m && report.FeesReceived == 150m && report.TravelAllowancesReceived == 10m, "Fees and allowances reconcile to gross recorded receipts");
Check(report.ContractorReceipts == 110m && report.EmployeeReceipts == 50m && report.UnclassifiedReceipts == 0m, "Employee and contractor receipts are separated");
Check(report.WorkGames.Select(x => x.Id).SequenceEqual([3, 5, 2]) && report.OutstandingFees == 200m, "Work-year appendix does not change received totals");
Check(report.UndatedIncomeTotal == 75m && report.UndatedExpenseTotal == 80m, "Unknown payment years from older records remain visible without being assigned to this year");
Check(report.PaidExpenseTotal == 160.10m && report.RecordedBusinessPortion == 30.02m, "Payment dates and per-item midpoint rounding govern the spending and business-portion subtotals");
Check(report.ExpenseReimbursements == 20m && report.PaidExpenseTotal == 160.10m, "Expense reimbursements are separate rather than silently netted");
Check(report.Categories.Sum(x => x.BusinessPortion) == report.RecordedBusinessPortion && report.Categories.Sum(x => x.Amount) == report.PaidExpenseTotal, "Category and itemized totals reconcile");
Check(report.TotalMiles == 125m && report.BusinessMiles == 75m && report.PersonalMiles == 40m && report.UnclassifiedMiles == 10m, "Trip dates, unpaid games, and mileage classifications are handled independently of income dates");
Check(report.Receipts.Count == 2 && report.ReviewItems.Any(x => x.Reason.Contains("take-home pay")), "Only owner receipts enter the report and employee payments receive a reconciliation reminder");
Check(report.ReviewItems.Any(x => x.Reason.Contains("unknown")) && report.ReviewItems.Any(x => x.Reason.Contains("unconfirmed")), "Unknown dates are explicit review items");
Check(TaxReport.BusinessPortion(new Expense { Amount = 100m, BusinessUsePercent = 101m }) == null, "Invalid historic business-use values are not silently treated as deductible");
Check(TaxReport.Build(2025, owner, games, expenses, metadata).TotalReceived == 0m, "A December game received in January is not included in the work year's cash-date receipts");
try { TaxReport.Build(9999, owner, games, expenses, metadata); throw new Exception("Invalid year accepted"); }
catch (ArgumentOutOfRangeException) { Check(true, "Unsupported years rejected without date overflow"); }
Check(TaxReport.Build(2026, owner, [], [], [], new() { UserId = "other", TaxYear = 2026, PreparerNotes = "SECRET" }).Profile.PreparerNotes == null, "A foreign year profile cannot enter an export");

var html = TaxReportExporter.Html(report);
Check(html.Contains("&lt;script&gt;private-note&lt;/script&gt;") && !html.Contains("<script>private-note"), "Worksheet HTML encodes game notes");
Check(html.Contains("&lt;img src=x onerror=alert(1)&gt;") && html.Contains("&lt;b&gt;not markup&lt;/b&gt;"), "Worksheet HTML encodes expense descriptions and preparer notes");
Check(!html.Contains("OTHER-USER-SECRET") && !html.Contains("PRIVATE.jpg"), "Worksheet does not leak other users' records");
Check(html.Contains("Unknown") || html.Contains("unknown payment year"), "Worksheet retains the unknown-year appendix");
var originalCulture = CultureInfo.CurrentCulture;
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
var csv = TaxReportExporter.CsvFiles(report);
CultureInfo.CurrentCulture = originalCulture;
Check(csv["income.csv"].Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()), "CSV includes UTF-8 BOM for spreadsheet compatibility");
Check(Encoding.UTF8.GetString(csv["income.csv"]).Contains("\"'=DANGEROUS()\""), "CSV formula injection is neutralized in payer names");
var formulaReport = TaxReport.Build(2026, owner, games, expenses, metadata, new TaxYearProfile { UserId = "owner", TaxYear = 2026, BusinessName = "  =FORMULA()" });
Check(Encoding.UTF8.GetString(TaxReportExporter.CsvFiles(formulaReport)["summary.csv"]).Contains("\"'  =FORMULA()\""), "CSV formula injection is neutralized even after leading spaces");
Check(Encoding.UTF8.GetString(csv["expenses.csv"]).Contains("\"'\t=FORMULA()\"") && Encoding.UTF8.GetString(csv["summary.csv"]).Contains("\"160.1\""), "CSV neutralizes leading-tab formulas and uses invariant decimal values");
Check(Encoding.UTF8.GetString(csv["income.csv"]).Contains("comma,quote\"\""), "CSV escapes commas, quotes, and multiline fields");
Check(!TaxReportExporter.ReceiptPath(metadata[0]).Split('/').Contains("..") && TaxReportExporter.ReceiptPath(metadata[0]) != TaxReportExporter.ReceiptPath(metadata[1]), "Archive filenames resist traversal and preserve duplicate-name receipts");

using (var sqlDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True").Options))
{
    var migration = sqlDb.Database.GetMigrations().Single(x => x.EndsWith("_AddTaxCenter"));
    var script = sqlDb.GetService<IMigrator>().GenerateScript("20261009070000_AddProBetaSubscriptionFields", migration);
    Check(script.Contains("CREATE TABLE [TaxYearProfile]") && script.Contains("ADD [PaidDate] date NULL") && script.Contains("[IncomeKind] nvarchar(16)"), "Tax migration is discovered and adds nullable dates with review defaults and saved year profiles");
    Check(!sqlDb.Database.HasPendingModelChanges(), "Migration snapshot matches the runtime model");
}

await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection));
builder.Services.AddScoped<TaxReportReader>();
builder.Services.AddAuthentication("checks").AddScheme<AuthenticationSchemeOptions, CheckAuthentication>("checks", _ => { });
builder.Services.AddAuthorization();
await using var app = builder.Build();
app.UseTaxReportPrivacy();
app.MapTaxReportEndpoints();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    db.Users.AddRange(owner, new ApplicationUser { Id = "other", UserName = "other", Email = "other@example.com" });
    db.SportTypes.Add(new SportType { Id = 1, Name = "Baseball" });
    db.Games.AddRange(games);
    db.Expenses.AddRange(expenses);
    db.TaxYearProfiles.Add(profile);
    byte[] bytes = [0xff, 0xd8, 0xff, 0xe0, 0, 1];
    db.ExpenseReceipts.AddRange(metadata.Select(x => new ExpenseReceipt { Id = x.Id, ExpenseId = x.ExpenseId, FileName = x.FileName, ContentType = x.ContentType, SizeBytes = bytes.Length, Content = bytes, UploadedAtUtc = DateTime.UtcNow }));
    await db.SaveChangesAsync();
    var reader = scope.ServiceProvider.GetRequiredService<TaxReportReader>();
    var loaded = await reader.ReadAsync("owner", 2026);
    Check(loaded!.TotalReceived == report.TotalReceived && loaded.PaidExpenseTotal == report.PaidExpenseTotal && loaded.Profile.PreparerNotes == profile.PreparerNotes, "Relational reader and persisted handoff notes reproduce calculated totals");
    Check((await reader.GetYearsAsync("owner")).Contains(2027), "Year selector includes payment years without games worked that year");
    Check(await reader.ReadAsync("missing", 2026) == null, "Missing users receive no report");
}
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var client = new HttpClient { BaseAddress = new Uri(address) };
using (var anonymous = await client.GetAsync("/tax-center/2026/worksheet")) Check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "Anonymous worksheet access is denied");
using (var anonymous = await client.GetAsync("/tax-center/2026/packet?receipts=true")) Check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "Anonymous packet access is denied");
client.DefaultRequestHeaders.Add("X-Test-User", "owner");
using (var response = await client.GetAsync("/tax-center/2026/worksheet"))
{
    Check(response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentType?.MediaType == "text/html", "Authenticated worksheet endpoint returns printable HTML");
    Check(response.Headers.CacheControl?.NoStore == true && response.Headers.CacheControl.Private, "Tax records use private no-store caching");
    Check(response.Headers.Contains("Content-Security-Policy") && response.Headers.Contains("X-Content-Type-Options"), "Worksheet sets browser security headers");
}
using (var badYear = await client.GetAsync("/tax-center/9999/packet")) Check(badYear.StatusCode == HttpStatusCode.BadRequest, "HTTP export rejects invalid years");
using (var badFile = await client.GetAsync("/tax-center/2026/export/unknown.csv")) Check(badFile.StatusCode == HttpStatusCode.NotFound, "HTTP CSV export rejects unknown filenames");
using (var csvResponse = await client.GetAsync("/tax-center/2026/export/income.csv"))
    Check(csvResponse.StatusCode == HttpStatusCode.OK && csvResponse.Content.Headers.ContentDisposition?.FileNameStar == "BlueLedger-2026-income.csv", "CSV downloads have generated safe filenames");

async Task<MemoryStream> GetPacket(bool receipts)
{
    using var response = await client.GetAsync($"/tax-center/2026/packet?receipts={receipts.ToString().ToLowerInvariant()}");
    Check(response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentType?.MediaType == "application/zip", "CPA packet endpoint produces ZIP");
    return new MemoryStream(await response.Content.ReadAsByteArrayAsync());
}
using (var packet = await GetPacket(true))
using (var zip = new ZipArchive(packet))
{
    Check(zip.Entries.Count(x => x.FullName.StartsWith("receipts/")) == 2, "Owner packet includes both original duplicate-name receipts and excludes other users' receipts");
    Check(zip.GetEntry("CPA-worksheet.html") != null && zip.GetEntry("receipt-index.csv") != null && zip.GetEntry("unassigned-income.csv") != null, "CPA packet includes worksheet, receipt manifest, and unassigned records");
    using var receipt = zip.GetEntry(TaxReportExporter.ReceiptPath(metadata[0]))!.Open();
    var output = new MemoryStream();
    await receipt.CopyToAsync(output);
    Check(output.ToArray().SequenceEqual(new byte[] { 0xff, 0xd8, 0xff, 0xe0, 0, 1 }), "Original attachment bytes are preserved");
    using var worksheet = new StreamReader(zip.GetEntry("CPA-worksheet.html")!.Open());
    var packetHtml = await worksheet.ReadToEndAsync();
    Check(packetHtml.Contains("worksheet.js") && packetHtml.Contains(TaxReportExporter.ReceiptPath(metadata[0])) && !packetHtml.Contains("/expenses/receipts/"), "Packet worksheet uses local receipt links and a standalone print button");
}
using (var packet = await GetPacket(false))
using (var zip = new ZipArchive(packet))
    Check(!zip.Entries.Any(x => x.FullName.StartsWith("receipts/")), "Optional attachment exclusion is honored");

client.DefaultRequestHeaders.Remove("X-Test-User");
client.DefaultRequestHeaders.Add("X-Test-User", "other");
using (var otherResponse = await client.GetAsync("/tax-center/2026/worksheet"))
{
    var otherHtml = await otherResponse.Content.ReadAsStringAsync();
    Check(otherHtml.Contains("OTHER-USER-SECRET") && !otherHtml.Contains("private-note") && !otherHtml.Contains("Sample officiating"), "Another authenticated user receives only their own tax records");
}
await app.StopAsync();
Check(!Directory.GetFiles(Path.GetTempPath(), "blueledger-tax-*.tmp").Any(), "Packet temporary files are deleted after download");
Console.WriteLine($"{count} tax checks passed.");

sealed class CheckAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Request.Headers["X-Test-User"].ToString();
        if (string.IsNullOrWhiteSpace(user)) return Task.FromResult(AuthenticateResult.NoResult());
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user), new Claim(ClaimTypes.Name, user)], Scheme.Name)), Scheme.Name)));
    }
}
