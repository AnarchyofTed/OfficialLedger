using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OfficialLedger.Data;

namespace OfficialLedger;

public static class ReceiptEndpoints
{
    public static IEndpointRouteBuilder MapReceiptEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/expenses/receipts/{id:int}", async (
            int id, bool? download, ClaimsPrincipal user, ApplicationDbContext db,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

            var receipt = await db.ExpenseReceipts.AsNoTracking()
                .Where(x => x.Id == id && x.Expense.UserId == userId)
                .Select(x => new { x.FileName, x.ContentType, x.Content })
                .SingleOrDefaultAsync(cancellationToken);
            if (receipt is null) return Results.NotFound();

            context.Response.Headers.CacheControl = "no-store, private";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Content-Security-Policy"] = "sandbox";
            var forceDownload = download == true || (!ReceiptFiles.CanPreview(receipt.ContentType) && receipt.ContentType != "application/pdf");
            return Results.File(receipt.Content, receipt.ContentType,
                fileDownloadName: forceDownload ? receipt.FileName : null);
        }).RequireAuthorization();
        return endpoints;
    }
}
