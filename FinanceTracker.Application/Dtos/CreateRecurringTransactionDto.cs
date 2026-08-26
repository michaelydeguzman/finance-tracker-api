using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Dtos;

/// <summary>
/// A new recurring template.
///
/// There is no <c>status</c> field: a template is always created Active, and moving it out
/// of that state is a transition endpoint rather than a body the caller can post. There is
/// no <c>userId</c> either — ownership comes from the bearer token, never the request.
/// </summary>
public sealed class CreateRecurringTransactionDto
{
    [Required]
    [MaxLength(250)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Maps to <c>RecurringTransaction.DefaultAmount</c> — the amount each generated transaction carries.</summary>
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid FrequencyId { get; set; }

    /// <summary>The schedule's anchor. Also the earliest date an occurrence can fall on.</summary>
    [Required]
    public DateTime StartDate { get; set; }

    /// <summary>Optional generation boundary. Null means the template runs indefinitely.</summary>
    public DateTime? EndDate { get; set; }
}
