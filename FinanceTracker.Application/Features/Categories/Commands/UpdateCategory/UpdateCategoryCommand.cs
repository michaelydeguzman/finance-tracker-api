using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Commands.UpdateCategory;

public sealed record UpdateCategoryCommand(Guid Id, UpdateCategoryDto Dto) : IRequest<CategoryResponseDto?>;
