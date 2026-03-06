using System.ComponentModel.DataAnnotations;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Dtos;

public sealed class UpdateCategoryDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public CategoryType CategoryType { get; set; }
}
