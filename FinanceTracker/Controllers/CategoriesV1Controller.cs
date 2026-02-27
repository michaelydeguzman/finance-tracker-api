using Microsoft.AspNetCore.Mvc;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Application.Services;
using FinanceTracker.Application.Dtos;
using Asp.Versioning;
using MediatR;
using FinanceTracker.Application.Features.Categories.Queries.GetCategories;

namespace FinanceTracker.Controllers
{
    [ApiVersion("1.0")]
    [ApiController]
    [Route("api/v{version:apiVersion}/categories")]
    public class CategoriesV1Controller : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly ISender _sender;

        public CategoriesV1Controller(ICategoryService categoryService, ISender sender)
        {
            _categoryService = categoryService;
            _sender = sender;
        }

        [HttpPost]
        public async Task<ActionResult<Category>> AddCategory([FromBody] CreateCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                CategoryType = dto.CategoryType,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var created = await _categoryService.AddCategoryAsync(category);
            return CreatedAtAction(nameof(GetCategoryById), new { id = created.Id }, created);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Category>> GetCategoryById(Guid id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null)
                return NotFound();
            return category;
        }

        [HttpGet]
        public async Task<ActionResult<List<Category>>> GetCategories([FromQuery] CategoryType? type)
        {
            var categories = await _sender.Send(new GetCategoriesQuery(type));
            return Ok(categories);
        }
    }
}