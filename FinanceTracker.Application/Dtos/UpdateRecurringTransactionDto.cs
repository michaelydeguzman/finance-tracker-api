using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Dtos;

/// <summary>
/// A full replacement of a template's editable fields. Status is deliberately absent —
/// pause, resume and cancel are their own endpoints so that an ordinary edit cannot change
/// whether the worker is generating.
/// </summary>
public sealed class UpdateRecurringTransactionDto
{
    [Required]
    [MaxLength(250)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid FrequencyId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}
