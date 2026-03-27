namespace OfficialLedger.Models;

public class Game
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int? LeagueId { get; set; }
    public League? League { get; set; }

    public DateTime GameDate { get; set; }

    public string Sport { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;

    public decimal FeeAmount { get; set; }
    public decimal MilesDriven { get; set; }

    public bool IsPaid { get; set; }

    public string? Notes { get; set; }
}