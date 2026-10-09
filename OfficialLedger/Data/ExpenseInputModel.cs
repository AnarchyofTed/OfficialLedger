using System.ComponentModel.DataAnnotations;

namespace OfficialLedger.Models;

public class ExpenseInputModel : IValidatableObject
{
    public static readonly string[] Categories =
        ["Meals", "Camps", "Equipment", "Clothing", "Travel", "Lodging", "Registration & dues", "Training", "Other"];

    [Required(ErrorMessage = "Expense date is required.")]
    public DateTime? ExpenseDate { get; set; } = DateTime.Today;
    public DateTime? PaidDate { get; set; }
    [Range(typeof(decimal), "0", "100", ErrorMessage = "Business use must be between 0 and 100%.")]
    public decimal? BusinessUsePercent { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Reimbursement must be zero or greater.")]
    public decimal ReimbursedAmount { get; set; }
    [Required(ErrorMessage = "Choose a category.")]
    public string Category { get; set; } = "Other";
    [Required(ErrorMessage = "Amount is required.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "Enter an amount greater than zero.")]
    public decimal? Amount { get; set; }
    [StringLength(200)] public string? Vendor { get; set; }
    [StringLength(500)] public string? BusinessPurpose { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Categories.Contains(Category))
            yield return new ValidationResult("Choose an available category.", [nameof(Category)]);
        if (Amount.HasValue && decimal.Round(Amount.Value, 2) != Amount.Value)
            yield return new ValidationResult("Use no more than two decimal places.", [nameof(Amount)]);
        if (decimal.Round(ReimbursedAmount, 2) != ReimbursedAmount)
            yield return new ValidationResult("Use no more than two decimal places.", [nameof(ReimbursedAmount)]);
        if (BusinessUsePercent.HasValue && decimal.Round(BusinessUsePercent.Value, 2) != BusinessUsePercent.Value)
            yield return new ValidationResult("Use no more than two decimal places.", [nameof(BusinessUsePercent)]);
    }
}
