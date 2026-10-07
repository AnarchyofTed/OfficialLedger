using System.ComponentModel.DataAnnotations;

namespace OfficialLedger.Data;

public class SignupEmail
{
    public int Id { get; set; }
    [Required, MaxLength(256)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(256)] public string NormalizedEmail { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
