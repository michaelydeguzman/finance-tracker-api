using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Dtos;

public sealed class UpdateTransactionDto
{
    [Required]
    [MaxLength(250)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    public Guid? FrequencyId { get; set; }
}
