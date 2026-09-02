using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Dtos.Responses;

public sealed class HouseholdMemberDto
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public bool IsOwner { get; init; }

    /// <summary>
    /// True for the person the response was built for, so the UI can label the row rather
    /// than making the browser compare ids it would otherwise have no reason to hold.
    /// </summary>
    public bool IsYou { get; init; }

    public static HouseholdMemberDto FromEntity(User member, Guid ownerUserId, Guid viewerUserId) => new()
    {
        UserId = member.Id,
        Email = member.Email,
        DisplayName = member.DisplayName,
        IsOwner = member.Id == ownerUserId,
        IsYou = member.Id == viewerUserId
    };
}

public sealed class HouseholdInvitationDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public string HouseholdName { get; init; } = string.Empty;
    public string InvitedEmail { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }

    public static HouseholdInvitationDto FromEntity(HouseholdInvitation invitation, string householdName) => new()
    {
        Id = invitation.Id,
        HouseholdId = invitation.HouseholdId,
        HouseholdName = householdName,
        InvitedEmail = invitation.InvitedEmail,
        Status = invitation.Status.ToString(),
        CreatedAt = invitation.CreatedAt,
        ExpiresAt = invitation.ExpiresAt
    };
}

public sealed class HouseholdResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid OwnerUserId { get; init; }

    /// <summary>Whether the caller may rename, invite, remove members, or delete.</summary>
    public bool IsOwner { get; init; }

    public DateTime CreatedAt { get; init; }

    public IReadOnlyList<HouseholdMemberDto> Members { get; init; } = Array.Empty<HouseholdMemberDto>();

    /// <summary>
    /// Offers this household has out that nobody has answered yet. Only ever populated for
    /// the owner: the addresses a household has approached are not every member's business.
    /// </summary>
    public IReadOnlyList<HouseholdInvitationDto> PendingInvitations { get; init; } =
        Array.Empty<HouseholdInvitationDto>();

    public static HouseholdResponseDto FromEntity(
        Household household,
        IEnumerable<User> members,
        Guid viewerUserId,
        IEnumerable<HouseholdInvitation>? pendingInvitations = null) => new()
    {
        Id = household.Id,
        Name = household.Name,
        OwnerUserId = household.OwnerUserId,
        IsOwner = household.OwnerUserId == viewerUserId,
        CreatedAt = household.CreatedAt,
        Members = members
            .Select(member => HouseholdMemberDto.FromEntity(member, household.OwnerUserId, viewerUserId))
            .ToList(),
        PendingInvitations = (pendingInvitations ?? Enumerable.Empty<HouseholdInvitation>())
            .Select(invitation => HouseholdInvitationDto.FromEntity(invitation, household.Name))
            .ToList()
    };
}
