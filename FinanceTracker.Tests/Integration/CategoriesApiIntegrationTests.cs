using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration;

public class CategoriesApiIntegrationTests : IClassFixture<FinanceTrackerWebApplicationFactory>
{
    private readonly FinanceTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CategoriesApiIntegrationTests(FinanceTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
        ResetDatabase();
    }

    private void ResetDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task PostGetPutDeleteCategory_EndToEnd_ReturnsExpectedResponses()
    {
        var createDto = new CreateCategoryDto { Name = "Salary", CategoryType = CategoryType.Income };
        var postResponse = await _client.PostAsJsonAsync("/api/v1/categories", createDto, HttpJsonOptions.ForApi);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await postResponse.Content.ReadFromJsonAsync<ApiResponseDto<CategoryResponseDto>>(HttpJsonOptions.ForApi);
        var id = created!.Data!.Id;

        var getResponse = await _client.GetAsync($"/api/v1/categories/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateDto = new UpdateCategoryDto { Name = "Salary (net)", CategoryType = CategoryType.Income };
        var putResponse = await _client.PutAsJsonAsync($"/api/v1/categories/{id}", updateDto, HttpJsonOptions.ForApi);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<ApiResponseDto<CategoryResponseDto>>(HttpJsonOptions.ForApi);
        updated!.Data!.Name.Should().Be("Salary (net)");

        var deleteResponse = await _client.DeleteAsync($"/api/v1/categories/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getMissing = await _client.GetAsync($"/api/v1/categories/{id}");
        getMissing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateCategoryAsync(string name, CategoryType type = CategoryType.Expense)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/categories", new CreateCategoryDto { Name = name, CategoryType = type }, HttpJsonOptions.ForApi);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<ApiResponseDto<CategoryResponseDto>>(HttpJsonOptions.ForApi);
        return created!.Data!.Id;
    }

    private async Task<Guid> RecordSpendOnAsync(Guid categoryId)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/transactions",
            new CreateTransactionDto { Name = "Weekly shop", CategoryId = categoryId, Amount = 42m, TransactionDate = DateTime.UtcNow },
            HttpJsonOptions.ForApi);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<ApiResponseDto<TransactionResponseDto>>(HttpJsonOptions.ForApi);
        return created!.Data!.Id;
    }

    private static async Task<string?> MessageOf(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ApiResponseDto<string>>(HttpJsonOptions.ForApi))!.Message;

    [Fact]
    public async Task DeleteCategory_WhileTransactionsUseIt_IsAConflictAndKeepsThem()
    {
        // Transactions -> Categories used to cascade: deleting a category silently deleted
        // every transaction filed under it, a housemate's included.
        var categoryId = await CreateCategoryAsync("Groceries");
        var transactionId = await RecordSpendOnAsync(categoryId);

        var response = await _client.DeleteAsync($"/api/v1/categories/{categoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageOf(response)).Should().Contain("Groceries").And.Contain("1 transaction");
        (await _client.GetAsync($"/api/v1/categories/{categoryId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetStringAsync("/api/v1/transactions")).Should().Contain(transactionId.ToString());
    }

    [Fact]
    public async Task DeleteCategory_WhileOnlyARecurringTemplateUsesIt_IsAConflictNotAServerError()
    {
        // RecurringTransactions -> Categories is Restrict, so this used to reach SQL Server
        // and come back as an unhandled DbUpdateException.
        var categoryId = await CreateCategoryAsync("Rent");
        await _factory.SeedAsync(async context =>
        {
            var category = await context.Categories.IgnoreQueryFilters().SingleAsync(c => c.Id == categoryId);
            var frequency = await context.Frequencies.SingleAsync(f => f.Id == RecurringTemplateFactory.MonthlyFrequencyId);
            var template = RecurringTemplateFactory.Template(
                RecurringTransactionStatus.Active, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), frequency: frequency);
            template.CategoryId = categoryId;
            template.Category = category;
            template.UserId = _factory.DefaultUserId;

            context.RecurringTransactions.Add(template);
            await context.SaveChangesAsync();
        });

        var response = await _client.DeleteAsync($"/api/v1/categories/{categoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageOf(response)).Should().Contain("1 recurring transaction");
    }

    [Fact]
    public async Task CreateCategory_WithANameAlreadyTakenForThatType_IsAConflict()
    {
        await CreateCategoryAsync("Groceries");

        var response = await _client.PostAsJsonAsync(
            "/api/v1/categories", new CreateCategoryDto { Name = "Groceries", CategoryType = CategoryType.Expense }, HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCategory_WithANameTakenOnlyByTheOtherType_Succeeds()
    {
        // The unique index is (UserId, CategoryType, Name): "Gifts" can be both.
        await CreateCategoryAsync("Gifts", CategoryType.Income);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/categories", new CreateCategoryDto { Name = "Gifts", CategoryType = CategoryType.Expense }, HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateCategory_RenamedToANameAlreadyTaken_IsAConflictAndLeavesItUnchanged()
    {
        await CreateCategoryAsync("Groceries");
        var foodId = await CreateCategoryAsync("Food");

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/categories/{foodId}", new UpdateCategoryDto { Name = "Groceries", CategoryType = CategoryType.Expense }, HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var current = await _client.GetFromJsonAsync<ApiResponseDto<CategoryResponseDto>>($"/api/v1/categories/{foodId}", HttpJsonOptions.ForApi);
        current!.Data!.Name.Should().Be("Food");
    }

    [Fact]
    public async Task UpdateCategory_KeepingItsOwnName_IsNotAConflictWithItself()
    {
        var id = await CreateCategoryAsync("Groceries");

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/categories/{id}", new UpdateCategoryDto { Name = "Groceries", CategoryType = CategoryType.Expense }, HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateCategory_ChangingItsType_IsRejectedAndLeavesItUnchanged()
    {
        // Flipping Expense to Income would silently turn every transaction filed under it
        // from spending into earnings.
        var id = await CreateCategoryAsync("Rent");

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/categories/{id}", new UpdateCategoryDto { Name = "Rent", CategoryType = CategoryType.Income }, HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var current = await _client.GetFromJsonAsync<ApiResponseDto<CategoryResponseDto>>($"/api/v1/categories/{id}", HttpJsonOptions.ForApi);
        current!.Data!.CategoryType.Should().Be(CategoryType.Expense);
    }

    [Fact]
    public void TransactionsDoNotCascadeWhenTheirCategoryIsDeleted()
    {
        // The database's half of the guard: if a transaction lands between the in-use check
        // and the delete, SQL Server refuses the delete instead of taking the row with it.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();

        var foreignKey = db.Model.FindEntityType(typeof(Transaction))!
            .GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(Category));

        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<CategoryResponseDto>>>(HttpJsonOptions.ForApi);
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
    }
}
