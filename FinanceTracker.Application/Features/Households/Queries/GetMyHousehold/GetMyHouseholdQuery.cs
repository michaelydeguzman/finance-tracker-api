using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Queries.GetMyHousehold;

/// <summary>The caller's household, or null when they are on their own.</summary>
public sealed record GetMyHouseholdQuery : IRequest<HouseholdResponseDto?>;
