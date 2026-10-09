using System.ComponentModel.DataAnnotations;

namespace OfficialLedger.Models;

public class GameTaxInputModel : IValidatableObject
{
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    [StringLength(200)] public string? PayerName { get; set; }
    public string IncomeKind { get; set; } = "Review";
    [Range(typeof(decimal), "0", "100000", ErrorMessage = "Travel reimbursement must be zero or greater.")]
    public decimal TravelReimbursement { get; set; }
    public DateTime? TravelDate { get; set; }
    [StringLength(200)] public string? TravelOrigin { get; set; }
    [StringLength(500)] public string? TravelPurpose { get; set; }
    public string MileageKind { get; set; } = "Review";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!TaxRecordLabels.IncomeKinds.Contains(IncomeKind))
            yield return new ValidationResult("Choose an available income type.", [nameof(IncomeKind)]);
        if (!TaxRecordLabels.MileageKinds.Contains(MileageKind))
            yield return new ValidationResult("Choose an available trip classification.", [nameof(MileageKind)]);
        if (decimal.Round(TravelReimbursement, 2) != TravelReimbursement)
            yield return new ValidationResult("Use no more than two decimal places.", [nameof(TravelReimbursement)]);
    }

    public void ApplyTaxFields(Game game)
    {
        game.PaidDate = IsPaid ? PaidDate?.Date : null;
        game.PayerName = PayerName?.Trim();
        game.IncomeKind = IncomeKind;
        game.TravelReimbursement = TravelReimbursement;
        game.TravelDate = TravelDate?.Date;
        game.TravelOrigin = TravelOrigin?.Trim();
        game.TravelPurpose = TravelPurpose?.Trim();
        game.MileageKind = MileageKind;
    }
}

public static class TaxRecordLabels
{
    public static readonly string[] IncomeKinds = ["Review", "Contractor", "Employee"];
    public static readonly string[] MileageKinds = ["Review", "Business", "Personal"];
    public static string Income(string kind) => kind switch
    {
        "Contractor" => "Independent contractor",
        "Employee" => "Employee / W-2",
        _ => "Needs classification"
    };
    public static string Mileage(string kind) => kind switch
    {
        "Business" => "Business (user classified)",
        "Personal" => "Commuting / personal",
        _ => "Needs classification"
    };
}
