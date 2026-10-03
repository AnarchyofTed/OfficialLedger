namespace OfficialLedger.Models;

/// <summary>Filters an already user-scoped set of games for report summaries.</summary>
public static class ReportGameFilter
{
    public static List<Game> Apply(IEnumerable<Game> games, string filter, IEnumerable<int> availableSeasonIds)
    {
        if (filter == "all") return games.ToList();

        if (filter.StartsWith("year:", StringComparison.Ordinal) &&
            int.TryParse(filter.AsSpan(5), out var year) && year >= 1 && year <= 9999)
        {
            return games.Where(x => x.GameDate.Year == year).ToList();
        }

        if (filter.StartsWith("season:", StringComparison.Ordinal) &&
            int.TryParse(filter.AsSpan(7), out var seasonId) && availableSeasonIds.Contains(seasonId))
        {
            return games.Where(x => x.SeasonId == seasonId).ToList();
        }

        return [];
    }
}
