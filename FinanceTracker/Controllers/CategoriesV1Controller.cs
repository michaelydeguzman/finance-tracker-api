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

namespace FinanceTracker.Controllers
{
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
        public async Task<ActionResult<ApiResponseDto<CategoryResponseDto>>> AddCategory([FromBody] CreateCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponseDto<CategoryResponseDto>.Fail("Invalid request payload."));

            var response = await _sender.Send(new CreateCategoryCommand(dto));
            return CreatedAtAction(nameof(GetCategoryById), new { id = response.Id }, ApiResponseDto<CategoryResponseDto>.Ok(response));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponseDto<CategoryResponseDto>>> UpdateCategory(Guid id, [FromBody] UpdateCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponseDto<CategoryResponseDto>.Fail("Invalid request payload."));

            var updated = await _sender.Send(new UpdateCategoryCommand(id, dto));
            if (updated is null)
                return NotFound(ApiResponseDto<CategoryResponseDto>.Fail("Category not found."));

            return Ok(ApiResponseDto<CategoryResponseDto>.Ok(updated));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponseDto<string>>> DeleteCategory(Guid id)
        {
            var deleted = await _sender.Send(new DeleteCategoryCommand(id));
            if (!deleted)
                return NotFound(ApiResponseDto<string>.Fail("Category not found."));

            return Ok(ApiResponseDto<string>.Ok("Category deleted successfully."));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponseDto<CategoryResponseDto>>> GetCategoryById(Guid id)
        {
            var category = await _sender.Send(new GetCategoryByIdQuery(id));
            if (category == null)
                return NotFound(ApiResponseDto<CategoryResponseDto>.Fail("Category not found."));

            return Ok(ApiResponseDto<CategoryResponseDto>.Ok(category));
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<List<CategoryResponseDto>>>> GetCategories([FromQuery] CategoryType? categoryType)
        {
            var categories = await _sender.Send(new GetCategoriesQuery(categoryType));
            return Ok(ApiResponseDto<List<CategoryResponseDto>>.Ok(categories));
        }
    }
}