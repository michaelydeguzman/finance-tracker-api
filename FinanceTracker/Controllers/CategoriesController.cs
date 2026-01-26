using Microsoft.AspNetCore.Mvc;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Application.Services;
using FinanceTracker.Application.Dtos;

namespace FinanceTracker.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
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
        public async Task<ActionResult<List<Category>>> GetCategories([FromQuery] CategoryTypes? type)
        {
            if (type.HasValue)
            {
                var filtered = await _categoryService.GetByTypeAsync(type.Value);
                return Ok(filtered);
            }

            var all = await _categoryService.GetAllAsync();
            return Ok(all);
        }
    }
}   