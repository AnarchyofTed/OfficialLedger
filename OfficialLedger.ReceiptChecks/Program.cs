using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using OfficialLedger;
using OfficialLedger.Components.Shared;
using OfficialLedger.Data;
using OfficialLedger.Models;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAILED: {name}");
    checks++;
    Console.WriteLine($"PASS: {name}");
}
void Reject(byte[] content, string name)
{
    try { ReceiptFiles.DetectContentType(content); }
    catch (InvalidDataException) { Check(true, name); return; }
    throw new Exception($"FAILED: {name}");
}

byte[] jpeg = [0xff, 0xd8, 0xff, 0xe0, 0, 1];
byte[] png = [137, 80, 78, 71, 13, 10, 26, 10];
var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nreceipt");
Check(ReceiptFiles.DetectContentType(jpeg) == "image/jpeg", "JPEG detected from bytes");
Check(ReceiptFiles.DetectContentType(png) == "image/png", "PNG detected from bytes");
Check(ReceiptFiles.DetectContentType(pdf) == "application/pdf", "PDF detected from bytes");
Check(ReceiptFiles.DetectContentType(Encoding.ASCII.GetBytes("RIFF0000WEBP")) == "image/webp", "WebP detected from bytes");
Check(ReceiptFiles.DetectContentType(new byte[] { 0, 0, 0, 20 }.Concat(Encoding.ASCII.GetBytes("ftypheic0000heic")).ToArray()) == "image/heic", "HEIC photo accepted");
Reject([], "Empty attachment rejected");
Reject(Encoding.ASCII.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'/>"), "SVG rejected");
Reject(Encoding.ASCII.GetBytes("<html><script>alert(1)</script></html>"), "HTML rejected");
Reject(new byte[] { 0, 0, 0, 20 }.Concat(Encoding.ASCII.GetBytes("ftypmp420000mp42")).ToArray(), "MP4 rejected even if renamed to JPG");
Reject(Encoding.ASCII.GetBytes("RIFF0000AVI "), "AVI video rejected");
Reject(Encoding.ASCII.GetBytes("PK\u0003\u0004word/document.xml"), "Word documents and ZIPs rejected");
var maxImage = new byte[ReceiptFiles.MaxFileBytes];
jpeg.CopyTo(maxImage, 0);
Check(ReceiptFiles.DetectContentType(maxImage) == "image/jpeg", "Image exactly at 10 MB accepted");
Reject(maxImage.Append((byte)0).ToArray(), "Image over 10 MB rejected");
var maxPdf = new byte[ReceiptFiles.MaxFileBytes + 1];
pdf.CopyTo(maxPdf, 0);
Reject(maxPdf, "PDF over 10 MB rejected");
var upload = new ReceiptUpload("../../receipt.jpg", "video/mp4", jpeg);
var validated = ReceiptFiles.CreateReceipt(upload);
Check(validated.FileName == "receipt.jpg" && validated.ContentType == "image/jpeg", "Filename sanitized and declared MIME ignored");
Check(ReceiptFiles.SafeFileName("C:\\fakepath\\receipt\r\n.jpg") == "receipt.jpg", "Windows path and control characters removed");
ReceiptFiles.ValidateCount(3, 2);
Check(true, "Five receipts allowed across saved and pending files");
try { ReceiptFiles.ValidateCount(3, 3); throw new Exception("Six receipts were accepted"); }
catch (InvalidDataException) { Check(true, "Six receipts rejected at save time"); }

// Verify the SQL Server migration is discoverable and contains the intended table/FK.
using (var sqlDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True").Options))
{
    Check(sqlDb.Database.GetMigrations().Contains("20261009060000_AddExpenseReceipts"), "Receipt migration discovered");
    var script = sqlDb.GetService<IMigrator>().GenerateScript("20261007170000_AddSignupEmailsAndAddress", "20261009060000_AddExpenseReceipts");
    Check(script.Contains("CREATE TABLE [ExpenseReceipt]") && script.Contains("varbinary(max)") && script.Contains("ON DELETE CASCADE"), "SQL Server migration creates durable bytes and cascade FK");
}

// Real HTTP endpoint and relational database: no production app startup or live database.
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
builder.Services.AddAuthentication("checks").AddScheme<AuthenticationSchemeOptions, CheckAuthenticationHandler>("checks", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddRazorComponents();
await using var app = builder.Build();
app.MapReceiptEndpoints();
await using (var scope = app.Services.CreateAsyncScope())
{
    await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
    var editableHtml = await renderer.Dispatcher.InvokeAsync(async () =>
        (await renderer.RenderComponentAsync<ReceiptAttachments>()).ToHtmlString());
    Check(editableHtml.Contains("capture=\"environment\"") && editableHtml.Contains("Choose Receipts") && editableHtml.Contains("10 MB"), "Rendered uploader offers camera, chooser, and size guidance");
    var readOnlyHtml = await renderer.Dispatcher.InvokeAsync(async () =>
        (await renderer.RenderComponentAsync<ReceiptAttachments>(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(ReceiptAttachments.ReadOnly)] = true,
            [nameof(ReceiptAttachments.Existing)] = new List<ReceiptInfo> { new(42, "<receipt>.jpg", "image/jpeg", 50, DateTime.UtcNow) }
        }))).ToHtmlString());
    Check(readOnlyHtml.Contains("/expenses/receipts/42") && readOnlyHtml.Contains("Download") && !readOnlyHtml.Contains("type=\"file\""), "Read-only receipts show protected links without upload controls");
    Check(readOnlyHtml.Contains("&lt;receipt&gt;.jpg"), "Receipt filename is HTML encoded");
}
int expenseId;
int receiptId;
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    var expense = new Expense { UserId = "owner", Category = "Equipment", ExpenseDate = DateTime.Today, Amount = 20,
        Receipts = [ReceiptFiles.CreateReceipt(upload), ReceiptFiles.CreateReceipt(new("receipt.pdf", "application/pdf", pdf))] };
    db.Expenses.Add(expense);
    await db.SaveChangesAsync();
    expenseId = expense.Id;
    receiptId = expense.Receipts[0].Id;
    Check(await db.ExpenseReceipts.CountAsync() == 2, "Expense and multiple receipts saved together");
    db.ChangeTracker.Clear();
    Check((await db.ExpenseReceipts.FindAsync(receiptId))!.Content.SequenceEqual(jpeg), "Receipt bytes survive context reload");
}
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var client = new HttpClient { BaseAddress = new Uri(address) };
Check((await client.GetAsync(ReceiptFiles.ViewUrl(receiptId))).StatusCode == HttpStatusCode.Unauthorized, "Logged-out access blocked");
client.DefaultRequestHeaders.Add("X-Check-User", "other-user");
Check((await client.GetAsync(ReceiptFiles.ViewUrl(receiptId))).StatusCode == HttpStatusCode.NotFound, "Another user's receipt inaccessible");
Check((await client.GetAsync(ReceiptFiles.DownloadUrl(receiptId))).StatusCode == HttpStatusCode.NotFound, "Another user's download inaccessible");
client.DefaultRequestHeaders.Remove("X-Check-User");
client.DefaultRequestHeaders.Add("X-Check-User", "owner");
var view = await client.GetAsync(ReceiptFiles.ViewUrl(receiptId));
Check(view.IsSuccessStatusCode && (await view.Content.ReadAsByteArrayAsync()).SequenceEqual(jpeg), "Owner can retrieve original receipt");
Check(view.Content.Headers.ContentType!.MediaType == "image/jpeg", "View returns validated MIME type");
Check(view.Headers.CacheControl!.NoStore && view.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff", "Receipt response prevents caching and MIME sniffing");
var download = await client.GetAsync(ReceiptFiles.DownloadUrl(receiptId));
Check(download.Content.Headers.ContentDisposition!.DispositionType == "attachment", "Download attachment disposition");
Check((await client.GetAsync(ReceiptFiles.ViewUrl(int.MaxValue))).StatusCode == HttpStatusCode.NotFound, "Missing receipt returns 404");
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // FK cascade also works when receipt data is not loaded.
    db.Expenses.Remove((await db.Expenses.FindAsync(expenseId))!);
    await db.SaveChangesAsync();
    Check(await db.ExpenseReceipts.CountAsync() == 0, "Deleting expense cascades to all receipts");
}
Check((await client.GetAsync(ReceiptFiles.ViewUrl(receiptId))).StatusCode == HttpStatusCode.NotFound, "Deleted receipt cannot be retrieved");
await app.StopAsync();
Console.WriteLine($"{checks} receipt checks passed.");

sealed class CheckAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-Check-User"].ToString();
        if (string.IsNullOrEmpty(userId)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
