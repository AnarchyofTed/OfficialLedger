using System.ComponentModel.DataAnnotations;

namespace OfficialLedger.Models;

public class Game
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int? SeasonId { get; set; }
    public Season? Season { get; set; }

    public int? LeagueId { get; set; }
    public League? League { get; set; }

    public DateTime GameDate { get; set; }

    public int SportTypeId { get; set; }
    public SportType? SportType { get; set; }
    public string LocationName { get; set; } = string.Empty;

    public decimal FeeAmount { get; set; }
    public decimal MilesDriven { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    [MaxLength(200)] public string? PayerName { get; set; }
    [MaxLength(16)] public string IncomeKind { get; set; } = "Review";
    public decimal TravelReimbursement { get; set; }
    public DateTime? TravelDate { get; set; }
    [MaxLength(200)] public string? TravelOrigin { get; set; }
    [MaxLength(500)] public string? TravelPurpose { get; set; }
    [MaxLength(16)] public string MileageKind { get; set; } = "Review";

    public string? Notes { get; set; }
}

