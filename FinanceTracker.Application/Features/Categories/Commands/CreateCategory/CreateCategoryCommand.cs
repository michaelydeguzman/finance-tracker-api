using FinanceTracker.Application.Dtos;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Commands.CreateCategory;

public sealed record CreateCategoryCommand(CreateCategoryDto Dto) : IRequest<CategoryCommandResult>;
