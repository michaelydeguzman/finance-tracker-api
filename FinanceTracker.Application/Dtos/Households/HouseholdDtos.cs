using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Dtos.Households;

public sealed class CreateHouseholdDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public sealed class RenameHouseholdDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public sealed class InviteHouseholdMemberDto
{
    [Required]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;
}
