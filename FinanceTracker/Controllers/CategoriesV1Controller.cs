using Asp.Versioning;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Categories.Commands.CreateCategory;
using FinanceTracker.Application.Features.Categories.Queries.GetCategories;
using FinanceTracker.Application.Features.Categories.Queries.GetCategoryById;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Controllers
{
    [ApiVersion("1.0")]
    [ApiController]
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

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponseDto<CategoryResponseDto>>> GetCategoryById(Guid id)
        {
            var category = await _sender.Send(new GetCategoryByIdQuery(id));
            if (category == null)
                return NotFound(ApiResponseDto<CategoryResponseDto>.Fail("Category not found."));

            return Ok(ApiResponseDto<CategoryResponseDto>.Ok(category));
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<List<CategoryResponseDto>>>> GetCategories([FromQuery] global::FinanceTracker.Domain.Entities.CategoryType? type)
        {
            var categories = await _sender.Send(new GetCategoriesQuery(type));
            return Ok(ApiResponseDto<List<CategoryResponseDto>>.Ok(categories));
        }
    }
}