using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OfficialLedger.Data;

namespace OfficialLedger.Tax;

public static class TaxReportEndpoints
{
    public static IApplicationBuilder UseTaxReportPrivacy(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/tax-center")) PrivateHeaders(context);
        await next(context);
    });

    public static IEndpointRouteBuilder MapTaxReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tax-center").RequireAuthorization();
        group.MapGet("/print.js", (HttpContext context) =>
        {
            PrivateHeaders(context);
            return Results.Text(TaxReportExporter.PrintScript, "text/javascript", Encoding.UTF8);
        });
        group.MapGet("/{year:int}/worksheet", async (int year, ClaimsPrincipal user, TaxReportReader reader, HttpContext context, CancellationToken ct) =>
        {
            PrivateHeaders(context);
            if (year is < 1900 or > 9998) return Results.BadRequest("Choose a valid calendar year.");
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var report = await reader.ReadAsync(userId, year, ct);
            if (report == null) return Results.NotFound();
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'";
            return Results.Content(TaxReportExporter.Html(report), "text/html", Encoding.UTF8);
        });
        group.MapGet("/{year:int}/export/{file}", async (int year, string file, ClaimsPrincipal user, TaxReportReader reader, HttpContext context, CancellationToken ct) =>
        {
            PrivateHeaders(context);
            if (year is < 1900 or > 9998) return Results.BadRequest("Choose a valid calendar year.");
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var report = await reader.ReadAsync(userId, year, ct);
            if (report == null) return Results.NotFound();
            var files = TaxReportExporter.CsvFiles(report);
            return files.TryGetValue(file, out var content) ? Results.File(content, "text/csv; charset=utf-8", $"BlueLedger-{year}-{file}") : Results.NotFound();
        });
        group.MapGet("/{year:int}/packet", async (int year, bool? receipts, ClaimsPrincipal user, TaxReportReader reader,
            IDbContextFactory<ApplicationDbContext> factory, HttpContext context, CancellationToken ct) =>
        {
            PrivateHeaders(context);
            if (year is < 1900 or > 9998) return Results.BadRequest("Choose a valid calendar year.");
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var report = await reader.ReadAsync(userId, year, ct);
            if (report == null) return Results.NotFound();
            var stream = await CreatePacketAsync(report, receipts == true, factory, ct);
            return Results.File(stream, "application/zip", $"BlueLedger-{year}-CPA-packet.zip");
        });
        return endpoints;
    }

    private static void PrivateHeaders(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.XContentTypeOptions = "nosniff";
    }

    public static async Task<FileStream> CreatePacketAsync(TaxReport report, bool includeReceipts,
        IDbContextFactory<ApplicationDbContext> factory, CancellationToken cancellationToken = default)
    {
        // Spool to a temporary file instead of holding an entire year of receipt images in memory.
        // The response disposes this stream and DeleteOnClose removes the temporary file.
        var path = Path.Combine(Path.GetTempPath(), $"blueledger-tax-{Guid.NewGuid():N}.tmp");
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose | FileOptions.SequentialScan);
        try
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
            {
                var receiptPaths = new Dictionary<int, string>();
                if (includeReceipts && report.Receipts.Count > 0)
                {
                    await using var db = await factory.CreateDbContextAsync(cancellationToken);
                    foreach (var receipt in report.Receipts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var content = await db.ExpenseReceipts.AsNoTracking()
                            .Where(x => x.Id == receipt.Id && x.ExpenseId == receipt.ExpenseId && x.Expense.UserId == report.Owner.Id)
                            .Select(x => x.Content).SingleOrDefaultAsync(cancellationToken);
                        if (content == null) continue; // Concurrently deleted receipts remain explicitly unavailable in the manifest.
                        var receiptPath = TaxReportExporter.ReceiptPath(receipt);
                        await WriteEntry(zip, receiptPath, content, CompressionLevel.NoCompression, cancellationToken);
                        receiptPaths[receipt.Id] = receiptPath;
                    }
                }
                foreach (var file in TaxReportExporter.CsvFiles(report))
                    await WriteEntry(zip, file.Key, file.Value, CompressionLevel.Fastest, cancellationToken);
                await WriteEntry(zip, "receipt-index.csv", TaxReportExporter.ReceiptManifest(report, receiptPaths, includeReceipts), CompressionLevel.Fastest, cancellationToken);
                await WriteEntry(zip, "CPA-worksheet.html", Encoding.UTF8.GetBytes(TaxReportExporter.Html(report, true, includeReceipts, receiptPaths)), CompressionLevel.Fastest, cancellationToken);
                await WriteEntry(zip, "worksheet.js", Encoding.UTF8.GetBytes(TaxReportExporter.PrintScript), CompressionLevel.Fastest, cancellationToken);
                var readme = $"BlueLedger {report.Year} CPA packet\nGenerated {report.GeneratedAtUtc:u}\n\nOpen CPA-worksheet.html in a browser, then select Print / save PDF.\nThe CSV files contain itemized records, summaries, and review reminders.\n" +
                    (includeReceipts ? "Original receipts are in receipts/. receipt-index.csv links each file to its expense.\n" : "Receipt attachments were not included. receipt-index.csv lists available attachments.\n") +
                    "\nUnknown payment years are in unassigned-income.csv and unassigned-expenses.csv; they are excluded from annual totals.\n\n" + TaxReportExporter.ScopeNote + "\n";
                await WriteEntry(zip, "README.txt", Encoding.UTF8.GetBytes(readme), CompressionLevel.Fastest, cancellationToken);
            }
            stream.Position = 0;
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private static async Task WriteEntry(ZipArchive archive, string name, byte[] content, CompressionLevel compression, CancellationToken ct)
    {
        var entry = archive.CreateEntry(name, compression);
        await using var output = entry.Open();
        await output.WriteAsync(content, ct);
    }
}
