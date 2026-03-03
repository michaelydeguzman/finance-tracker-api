using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Commands.CreateCategory;

public sealed record CreateCategoryCommand(CreateCategoryDto Dto) : IRequest<CategoryResponseDto>;
