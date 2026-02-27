using System.ComponentModel.DataAnnotations;
using FinanceTracker.Domain;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Dtos
{
    public class CreateCategoryDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public CategoryType CategoryType { get; set; }
    }
}