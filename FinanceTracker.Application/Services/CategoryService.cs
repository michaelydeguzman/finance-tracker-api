using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Categories;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;

namespace FinanceTracker.Application.Services
{
    /// <summary>
    /// The category write rules. Under households a member can edit and delete a housemate's
    /// categories, so each rule here protects records the caller may not own.
    /// </summary>
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _repository;

        public CategoryService(ICategoryRepository repository)
        {
            _repository = repository;
        }

        public async Task<CategoryCommandResult> AddCategoryAsync(Category category, CancellationToken cancellationToken = default)
        {
            if (await _repository.NameTakenAsync(category.UserId, category.CategoryType, category.Name, null, cancellationToken))
                return NameTaken(category.CategoryType, category.Name);

            var created = await _repository.AddAsync(category, cancellationToken);
            return CategoryCommandResult.Success(CategoryResponseDto.FromEntity(created));
        }

        public async Task<Category?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _repository.GetByIdAsync(id, cancellationToken);
        }

        public async Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _repository.GetAllAsync(cancellationToken);
        }

        public async Task<List<Category>> GetByTypeAsync(CategoryType type, CancellationToken cancellationToken = default)
        {
            return await _repository.GetByTypeAsync(type, cancellationToken);
        }

        /// <summary>
        /// Refuses while anything is filed under the category. Deleting used to cascade to its
        /// transactions, so one member could erase a housemate's history with no warning.
        /// </summary>
        public async Task<CategoryCommandResult> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var category = await _repository.GetByIdAsync(id, cancellationToken);
            if (category is null)
                return CategoryCommandResult.NotFound();

            var usage = await _repository.GetUsageAsync(id, cancellationToken);
            if (usage.InUse)
                return CategoryCommandResult.Conflict(
                    $"'{category.Name}' is used by {Describe(usage)}. " +
                    "Move them to another category before deleting it.");

            return await _repository.DeleteAsync(id, cancellationToken)
                ? CategoryCommandResult.Success()
                : CategoryCommandResult.NotFound();
        }

        /// <summary>
        /// Renames only. The type is fixed at creation: flipping Expense to Income would
        /// silently turn every transaction filed under the category into earnings.
        /// </summary>
        public async Task<CategoryCommandResult> UpdateCategoryAsync(
            Guid id,
            string name,
            CategoryType categoryType,
            CancellationToken cancellationToken = default)
        {
            var category = await _repository.GetByIdAsync(id, cancellationToken);
            if (category is null)
                return CategoryCommandResult.NotFound();

            if (category.CategoryType != categoryType)
                return CategoryCommandResult.Invalid(
                    "A category's type cannot be changed. Create a new category of the other type instead.");

            // The owner's names, not the caller's: that is what the unique index compares.
            if (await _repository.NameTakenAsync(category.UserId, category.CategoryType, name, category.Id, cancellationToken))
                return NameTaken(category.CategoryType, name);

            category.Name = name;

            var updated = await _repository.UpdateAsync(category, cancellationToken);
            return updated is null
                ? CategoryCommandResult.NotFound()
                : CategoryCommandResult.Success(CategoryResponseDto.FromEntity(updated));
        }

        private static CategoryCommandResult NameTaken(CategoryType type, string name)
            => CategoryCommandResult.Conflict(
                $"There is already an {type.ToString().ToLowerInvariant()} category called '{name}'.");

        private static string Describe(CategoryUsage usage)
        {
            var parts = new List<string>();
            if (usage.Transactions > 0)
                parts.Add(Count(usage.Transactions, "transaction"));
            if (usage.RecurringTransactions > 0)
                parts.Add(Count(usage.RecurringTransactions, "recurring transaction"));

            return string.Join(" and ", parts);
        }

        private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
    }
}
