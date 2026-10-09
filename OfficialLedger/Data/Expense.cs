using System.ComponentModel.DataAnnotations;

namespace OfficialLedger.Models;

public class Expense
{
    public int Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    [Required, MaxLength(32)] public string Category { get; set; } = "Other";
    public decimal Amount { get; set; }
    [MaxLength(200)] public string? Vendor { get; set; }
    [MaxLength(500)] public string? BusinessPurpose { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    public List<ExpenseReceipt> Receipts { get; set; } = [];
}

