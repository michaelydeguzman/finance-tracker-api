using System.ComponentModel.DataAnnotations;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Dtos
{
    public class CreateCategoryDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public CategoryTypes CategoryType { get; set; }
    }
}
