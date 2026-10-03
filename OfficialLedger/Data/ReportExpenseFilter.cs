namespace OfficialLedger.Models;

/// <summary>Filters an already user-scoped expense list by report selection.</summary>
public static class ReportExpenseFilter
{
    public static List<Expense> Apply(IEnumerable<Expense> expenses, string filter, IEnumerable<Season> availableSeasons)
    {
        if (filter == "all") return expenses.ToList();
        if (filter.StartsWith("year:", StringComparison.Ordinal) &&
            int.TryParse(filter.AsSpan(5), out var year) && year >= 1 && year <= 9999)
        {
            return expenses.Where(x => x.ExpenseDate.Year == year).ToList();
        }
        if (filter.StartsWith("season:", StringComparison.Ordinal) &&
            int.TryParse(filter.AsSpan(7), out var seasonId))
        {
            var season = availableSeasons.FirstOrDefault(x => x.Id == seasonId);
            if (season is not null)
                return expenses.Where(x => x.ExpenseDate.Date >= season.StartDate.Date &&
                                           x.ExpenseDate.Date <= season.EndDate.Date).ToList();
        }
        return [];
    }
}
