using FinanceTracker.Application.Dtos;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Commands.UpdateCategory;

public sealed record UpdateCategoryCommand(Guid Id, UpdateCategoryDto Dto) : IRequest<CategoryCommandResult>;
