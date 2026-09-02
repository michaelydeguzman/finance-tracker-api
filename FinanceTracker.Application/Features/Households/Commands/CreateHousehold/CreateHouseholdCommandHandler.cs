using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.CreateHousehold;

public sealed class CreateHouseholdCommandHandler
    : IRequestHandler<CreateHouseholdCommand, HouseholdResult<HouseholdResponseDto>>
{
    private readonly IHouseholdRepository _households;
    private readonly IUserRepository _users;
    private readonly ICurrentUserAccessor _currentUser;

    public CreateHouseholdCommandHandler(
        IHouseholdRepository households,
        IUserRepository users,
        ICurrentUserAccessor currentUser)
    {
        _households = households;
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<HouseholdResult<HouseholdResponseDto>> Handle(
        CreateHouseholdCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var name = request.Dto.Name?.Trim() ?? string.Empty;

        if (name.Length == 0)
            return HouseholdResult<HouseholdResponseDto>.Invalid("A household name is required.");

        var user = await _users.GetByIdAsync(userId, cancellationToken);

        if (user is null)
            return HouseholdResult<HouseholdResponseDto>.NotFound("Your account could not be found.");

        if (user.HouseholdId is not null)
        {
            return HouseholdResult<HouseholdResponseDto>.Conflict(
                "You are already in a household. Leave it before creating another.");
        }

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _households.AddAsync(household, cancellationToken);

        // The creator joins the household they just made — there is no such thing as an
        // empty one, and an owner outside their own household could not see its records.
        user.HouseholdId = household.Id;

        // Everything they have entered so far comes with them, so the household starts with
        // the founder's history rather than looking empty until the next transaction.
        await _households.ReassignRecordsAsync(userId, household.Id, cancellationToken);

        await _households.SaveChangesAsync(cancellationToken);

        var members = await _households.GetMembersAsync(household.Id, cancellationToken);

        return HouseholdResult<HouseholdResponseDto>.Success(
            HouseholdResponseDto.FromEntity(household, members, userId));
    }
}
