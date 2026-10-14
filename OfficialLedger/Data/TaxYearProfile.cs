using System.ComponentModel.DataAnnotations;

namespace OfficialLedger.Models;

public class TaxYearProfile
{
    public int Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = string.Empty;
    [Range(1900, 9998)] public int TaxYear { get; set; }
    [MaxLength(200)] public string? BusinessName { get; set; }
    [MaxLength(4000)] public string? PreparerNotes { get; set; }
}
