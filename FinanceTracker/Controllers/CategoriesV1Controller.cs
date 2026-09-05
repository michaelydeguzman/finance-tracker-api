using Asp.Versioning;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Categories.Commands.CreateCategory;
using FinanceTracker.Application.Features.Categories.Commands.DeleteCategory;
using FinanceTracker.Application.Features.Categories.Commands.UpdateCategory;
using FinanceTracker.Application.Features.Categories.Queries.GetCategories;
using FinanceTracker.Application.Features.Categories.Queries.GetCategoryById;
using FinanceTracker.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/categories")]
public class CategoriesV1Controller : ControllerBase
{
    private readonly ISender _sender;

    public CategoriesV1Controller(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<CategoryResponseDto>>> AddCategory(
        [FromBody] CreateCategoryDto dto,
        CancellationToken cancellationToken = default)
    {
        var response = await _sender.Send(new CreateCategoryCommand(dto), cancellationToken);
        return CreatedAtAction(nameof(GetCategoryById), new { id = response.Id }, ApiResponseDto<CategoryResponseDto>.Ok(response));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponseDto<CategoryResponseDto>>> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryDto dto,
        CancellationToken cancellationToken = default)
    {
        var updated = await _sender.Send(new UpdateCategoryCommand(id, dto), cancellationToken);
        if (updated is null)
            return NotFound(ApiResponseDto<CategoryResponseDto>.Fail("Category not found."));

        return Ok(ApiResponseDto<CategoryResponseDto>.Ok(updated));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponseDto<string>>> DeleteCategory(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _sender.Send(new DeleteCategoryCommand(id), cancellationToken);
        if (!deleted)
            return NotFound(ApiResponseDto<string>.Fail("Category not found."));

        return Ok(ApiResponseDto<string>.Ok("Category deleted successfully."));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponseDto<CategoryResponseDto>>> GetCategoryById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _sender.Send(new GetCategoryByIdQuery(id), cancellationToken);
        if (category == null)
            return NotFound(ApiResponseDto<CategoryResponseDto>.Fail("Category not found."));

        return Ok(ApiResponseDto<CategoryResponseDto>.Ok(category));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<CategoryResponseDto>>>> GetCategories(
        [FromQuery] CategoryType? categoryType,
        CancellationToken cancellationToken = default)
    {
        var categories = await _sender.Send(new GetCategoriesQuery(categoryType), cancellationToken);
        return Ok(ApiResponseDto<List<CategoryResponseDto>>.Ok(categories));
    }
}
