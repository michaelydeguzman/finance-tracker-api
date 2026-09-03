using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.RenameHousehold;

public sealed class RenameHouseholdCommandHandler
    : IRequestHandler<RenameHouseholdCommand, HouseholdResult<HouseholdResponseDto>>
{
    private readonly IHouseholdRepository _households;
    private readonly ICurrentUserAccessor _currentUser;

    public RenameHouseholdCommandHandler(IHouseholdRepository households, ICurrentUserAccessor currentUser)
    {
        _households = households;
        _currentUser = currentUser;
    }

    public async Task<HouseholdResult<HouseholdResponseDto>> Handle(
        RenameHouseholdCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var name = request.Dto.Name?.Trim() ?? string.Empty;

        if (name.Length == 0)
            return HouseholdResult<HouseholdResponseDto>.Invalid("A household name is required.");

        var household = await _households.GetForUserAsync(userId, cancellationToken);

        if (household is null)
            return HouseholdResult<HouseholdResponseDto>.NotFound("You are not in a household.");

        if (household.OwnerUserId != userId)
            return HouseholdResult<HouseholdResponseDto>.Forbidden();

        household.Name = name;
        await _households.SaveChangesAsync(cancellationToken);

        var members = await _households.GetMembersAsync(household.Id, cancellationToken);

        return HouseholdResult<HouseholdResponseDto>.Success(
            HouseholdResponseDto.FromEntity(household, members, userId));
    }
}
