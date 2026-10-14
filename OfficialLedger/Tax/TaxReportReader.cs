using Microsoft.EntityFrameworkCore;
using OfficialLedger.Data;

namespace OfficialLedger.Tax;

public sealed class TaxReportReader(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<int>> GetYearsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var dates = await db.Games.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.GameDate, x.PaidDate, x.TravelDate }).ToListAsync(cancellationToken);
        var expenseDates = await db.Expenses.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.ExpenseDate, x.PaidDate }).ToListAsync(cancellationToken);
        var savedYears = await db.TaxYearProfiles.Where(x => x.UserId == userId).Select(x => x.TaxYear).ToListAsync(cancellationToken);
        return dates.SelectMany(x => new[] { x.GameDate.Year, x.PaidDate?.Year ?? x.GameDate.Year, x.TravelDate?.Year ?? x.GameDate.Year })
            .Concat(expenseDates.SelectMany(x => new[] { x.ExpenseDate.Year, x.PaidDate?.Year ?? x.ExpenseDate.Year }))
            .Concat(savedYears).Append(DateTime.Today.Year).Append(DateTime.Today.Year - 1)
            .Where(x => x is >= 1900 and <= 9998).Distinct().OrderByDescending(x => x).ToList();
    }

    public async Task<TaxReport?> ReadAsync(string userId, int year, CancellationToken cancellationToken = default)
    {
        if (year is < 1900 or > 9998) throw new ArgumentOutOfRangeException(nameof(year));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var owner = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (owner == null) return null;
        var games = await db.Games.AsNoTracking().Where(x => x.UserId == userId).Include(x => x.SportType).Include(x => x.Season).ToListAsync(cancellationToken);
        var expenses = await db.Expenses.AsNoTracking().Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        // Metadata only: receipt bytes are fetched one at a time when a packet is downloaded.
        var receipts = await db.ExpenseReceipts.AsNoTracking()
            .Where(x => x.Expense.UserId == userId && (x.Expense.PaidDate == null || (x.Expense.PaidDate >= start && x.Expense.PaidDate < end)))
            .Select(x => new TaxReceipt(x.Id, x.ExpenseId, x.FileName, x.ContentType, x.SizeBytes)).ToListAsync(cancellationToken);
        var profile = await db.TaxYearProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.TaxYear == year, cancellationToken);
        return TaxReport.Build(year, owner, games, expenses, receipts, profile);
    }
}
