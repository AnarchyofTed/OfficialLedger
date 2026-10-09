using System.ComponentModel.DataAnnotations;

namespace OfficialLedger.Models;

public class ExpenseReceipt
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public Expense Expense { get; set; } = null!;
    [Required, MaxLength(200)] public string FileName { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public byte[] Content { get; set; } = [];
}
