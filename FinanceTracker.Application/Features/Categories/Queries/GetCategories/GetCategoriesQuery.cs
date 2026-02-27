using FinanceTracker.Domain.Entities;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Queries.GetCategories;

public sealed record GetCategoriesQuery(CategoryType? Type) : IRequest<List<Category>>;
