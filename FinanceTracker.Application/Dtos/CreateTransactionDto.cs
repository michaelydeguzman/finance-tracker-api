using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Dtos;

public sealed class CreateTransactionDto
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

    // CreatedBy is deliberately absent. It used to arrive in the request body, defaulting
    // to a literal the browser chose, which made the audit label anything the caller cared
    // to claim. It is now taken from the authenticated principal instead.
}
